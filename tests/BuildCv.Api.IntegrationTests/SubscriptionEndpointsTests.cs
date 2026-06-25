using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Infrastructure.Subscriptions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Api.IntegrationTests;

public sealed class SubscriptionEndpointsTests : IDisposable
{
    [Fact]
    public async Task Post_Returns201_WithValidAuthAndFlag()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: true);
        using var client = factory.CreateClient();

        var (accessToken, _) = await LoginAndAuthenticate(client, factory);

        var response = await SendAuthedAsync(
            client,
            HttpMethod.Post,
            "/api/v1/subscriptions",
            accessToken,
            JsonContent.Create(new { plan = "starter", paymentSourceId = "ps_test_001" }));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "feature flag is on, user has no active subscription, and payment source is valid");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("plan").GetString().Should().Be("starter");
        body.GetProperty("status").GetString().Should().Be("active");
        body.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        body.GetProperty("currentPeriodStart").ValueKind.Should().Be(JsonValueKind.String);
        body.GetProperty("currentPeriodEnd").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Post_Returns503_WhenFeatureFlagDisabled()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: false);
        using var client = factory.CreateClient();

        var (accessToken, _) = await LoginAndAuthenticate(client, factory);

        var response = await SendAuthedAsync(
            client,
            HttpMethod.Post,
            "/api/v1/subscriptions",
            accessToken,
            JsonContent.Create(new { plan = "starter", paymentSourceId = "ps_test_001" }));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            "Art. IX — subscription endpoints MUST be gated by SubscriptionRecurring:Enabled=false returning 503");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("SUBSCRIPTION/DISABLED");
    }

    [Fact]
    public async Task Post_Returns409_WhenUserHasActiveSubscription()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: true);
        using var client = factory.CreateClient();

        var (accessToken, _) = await LoginAndAuthenticate(client, factory);

        var first = await SendAuthedAsync(
            client,
            HttpMethod.Post,
            "/api/v1/subscriptions",
            accessToken,
            JsonContent.Create(new { plan = "starter", paymentSourceId = "ps_test_001" }));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await SendAuthedAsync(
            client,
            HttpMethod.Post,
            "/api/v1/subscriptions",
            accessToken,
            JsonContent.Create(new { plan = "standard", paymentSourceId = "ps_test_002" }));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the same user MUST NOT have two active subscriptions (UNIQUE constraint + state machine)");
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("SUBSCRIPTION/ALREADY_ACTIVE");
    }

    [Fact]
    public async Task GetMe_Returns200_WhenActive()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: true);
        using var client = factory.CreateClient();

        var (accessToken, _) = await LoginAndAuthenticate(client, factory);

        var post = await SendAuthedAsync(
            client,
            HttpMethod.Post,
            "/api/v1/subscriptions",
            accessToken,
            JsonContent.Create(new { plan = "standard", paymentSourceId = "ps_test_get" }));
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await SendAuthedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/subscriptions/me",
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("plan").GetString().Should().Be("standard");
        body.GetProperty("status").GetString().Should().Be("active");
        body.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        body.GetProperty("nextChargeAt").ValueKind.Should().Be(JsonValueKind.String);
        body.TryGetProperty("canceledAt", out var canceled).Should().BeTrue();
        canceled.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetMe_Returns404_WhenNone()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: true);
        using var client = factory.CreateClient();

        var (accessToken, _) = await LoginAndAuthenticate(client, factory);

        var response = await SendAuthedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/subscriptions/me",
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("SUBSCRIPTION/NOT_FOUND");
    }

    [Fact]
    public async Task DeleteMe_Returns200_OnCancel()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: true);
        using var client = factory.CreateClient();

        var (accessToken, _) = await LoginAndAuthenticate(client, factory);

        var post = await SendAuthedAsync(
            client,
            HttpMethod.Post,
            "/api/v1/subscriptions",
            accessToken,
            JsonContent.Create(new { plan = "starter", paymentSourceId = "ps_test_cancel" }));
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await SendAuthedAsync(
            client,
            HttpMethod.Delete,
            "/api/v1/subscriptions/me",
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("canceled");
        body.TryGetProperty("accessUntil", out var accessUntil).Should().BeTrue();
        accessUntil.ValueKind.Should().Be(JsonValueKind.String);

        var fakeProvider = factory.Services.GetRequiredService<ISubscriptionProvider>() as FakeSubscriptionProvider;
        fakeProvider.Should().NotBeNull("test factory MUST replace Wompi with a fake provider to keep tests offline");
        fakeProvider!.CancelCalls.Should().Be(1, "cancel must call Wompi exactly once per active subscription");
    }

    [Fact]
    public async Task Post_Returns401_WithoutJwt()
    {
        await using var factory = new Factory(subscriptionFlagEnabled: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/subscriptions",
            new { plan = "starter", paymentSourceId = "ps_test_noauth" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<(string AccessToken, Guid UserId)> LoginAndAuthenticate(HttpClient client, Factory factory)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/google",
            new { code = "test-auth-code" });
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;
        var userId = loginBody.GetProperty("user").GetProperty("userId").GetGuid();
        return (accessToken, userId);
    }

    private static Task<HttpResponseMessage> SendAuthedAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string accessToken,
        HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = body;
        }
        return client.SendAsync(request);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

public sealed class Factory : WebApplicationFactory<Program>
{
    private readonly bool _subscriptionFlagEnabled;

    public Factory(bool subscriptionFlagEnabled)
    {
        _subscriptionFlagEnabled = subscriptionFlagEnabled;
    }

    public string SigningKey { get; } = "test-signing-key-that-is-long-enough-for-hmac-sha256!";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = "buildcv-test",
                ["Jwt:Audience"] = "buildcv-test",
                ["Ai:ApiKey"] = "test-key",
                ["Credits:Enabled"] = "true",
                ["SubscriptionRecurring:Enabled"] = _subscriptionFlagEnabled ? "true" : "false",
                ["Wompi:Enabled"] = "false",
            }));

        builder.ConfigureServices(services =>
        {
            var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAuthenticationService));
            if (authDescriptor is not null)
            {
                services.Remove(authDescriptor);
            }
            services.AddSingleton<IAuthenticationService, FakeOAuthAdapter>();

            var providerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISubscriptionProvider));
            if (providerDescriptor is not null)
            {
                services.Remove(providerDescriptor);
            }
            services.AddSingleton<ISubscriptionProvider, FakeSubscriptionProvider>();

            services.AddSingleton<SubscriptionFeatureFlag>(_ => new SubscriptionFeatureFlag(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SubscriptionRecurring:Enabled"] = _subscriptionFlagEnabled ? "true" : "false",
                }).Build()));
        });

        return base.CreateHost(builder);
    }
}

public sealed class FakeSubscriptionProvider : ISubscriptionProvider
{
    private int _counter;

    public int CreateCalls { get; private set; }

    public int CancelCalls { get; private set; }

    public Task<string> CreateScheduledChargeAsync(
        string paymentSourceId,
        decimal amountCop,
        string currency,
        DateTime chargeDate,
        CancellationToken ct = default)
    {
        _ = paymentSourceId;
        _ = amountCop;
        _ = currency;
        _ = chargeDate;
        CreateCalls++;
        return Task.FromResult($"fake-charge-{Interlocked.Increment(ref _counter)}");
    }

    public Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct = default)
    {
        _ = chargeId;
        CancelCalls++;
        return Task.FromResult(true);
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        _ = payload;
        _ = signature;
        return false;
    }
}
