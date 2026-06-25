using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using BuildCv.Api.Contracts;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Credits;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Api.IntegrationTests;

public sealed class RequireCreditsFilterTests : IDisposable
{
    private readonly CreditsTestFactory _factory = new();
    private readonly HttpClient _client;

    public RequireCreditsFilterTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Adapt_without_jwt_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/adapt",
            new { cvText = "x".PadRight(250, 'x'), jobText = "y".PadRight(150, 'y') });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Adapt_with_0_credits_returns_402_with_balance_header()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await DrainCreditsAsync(accessToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/adapt")
        {
            Content = JsonContent.Create(new { cvText = "x".PadRight(250, 'x'), jobText = "y".PadRight(150, 'y') }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        response.Headers.Contains("X-Credit-Balance").Should().BeTrue();
        response.Headers.GetValues("X-Credit-Balance").Single().Should().Be("0");
        response.Headers.Contains("Retry-After").Should().BeTrue();
        response.Headers.GetValues("Retry-After").Single().Should().Be("0");

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("CREDIT/INSUFFICIENT");
    }

    [Fact]
    public async Task Adapt_with_1_credit_returns_200_and_decrements_balance()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        var before = await GetBalance(accessToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/adapt")
        {
            Content = JsonContent.Create(new { cvText = "x".PadRight(250, 'x'), jobText = "y".PadRight(150, 'y') }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await GetBalance(accessToken);
        after.Should().Be(before - 1);
    }

    [Fact]
    public async Task Adapt_idempotency_returns_same_balance_on_replay_with_same_idempotency_key()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var first = await SendAdaptWithKey(accessToken, "idem-key-1");
        var balanceAfterFirst = await GetBalance(accessToken);

        var replay = await SendAdaptWithKey(accessToken, "idem-key-1");
        var balanceAfterReplay = await GetBalance(accessToken);

        if (first.StatusCode == HttpStatusCode.OK)
        {
            replay.StatusCode.Should().Be(HttpStatusCode.OK);
            balanceAfterReplay.Should().Be(balanceAfterFirst);
        }
    }

    [Fact]
    public async Task Adapt_with_different_keys_decrements_balance_each_time()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        var before = await GetBalance(accessToken);

        await SendAdaptWithKey(accessToken, "key-A");
        await SendAdaptWithKey(accessToken, "key-B");

        var after = await GetBalance(accessToken);
        after.Should().Be(before - 2);
    }

    [Fact]
    public async Task Adapt_history_records_consumption_entries()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await SendAdapt(accessToken);

        var response = await SendAuthed(HttpMethod.Get, "/api/v1/credits/history", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
    }

    private async Task<HttpResponseMessage> SendAdapt(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/adapt")
        {
            Content = JsonContent.Create(new { cvText = "x".PadRight(250, 'x'), jobText = "y".PadRight(150, 'y') }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAdaptWithKey(string accessToken, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/adapt")
        {
            Content = JsonContent.Create(new { cvText = "x".PadRight(250, 'x'), jobText = "y".PadRight(150, 'y') }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(request);
    }

    private async Task<int> GetBalance(string accessToken)
    {
        var response = await SendAuthed(HttpMethod.Get, "/api/v1/credits/balance", accessToken);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("balance").GetInt32();
    }

    private async Task DrainCreditsAsync(string accessToken)
    {
        var balance = await GetBalance(accessToken);
        for (var i = 0; i < balance; i++)
        {
            await SendAdapt(accessToken);
        }
    }

    private Task<HttpResponseMessage> SendAuthed(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private async Task<(string AccessToken, Guid UserId)> LoginAndAuthenticate()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/google",
            new OAuthCallbackRequest("test-auth-code"));
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;
        var userId = loginBody.GetProperty("user").GetProperty("userId").GetGuid();
        return (accessToken, userId);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public sealed class CreditsTestFactory : WebApplicationFactory<Program>
{
    static CreditsTestFactory()
    {
        // Environment variables override appsettings.*.json in the standard ASP.NET
        // Core configuration order, so this is the most reliable way to keep these
        // tests self-contained — they don't depend on the developer's local
        // appsettings.Development.json (gitignored, may carry Anthropic/Minimax).
        Environment.SetEnvironmentVariable("Ai__Provider", "Stub");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-hmac-sha256!",
                ["Jwt:Issuer"] = "buildcv-test",
                ["Jwt:Audience"] = "buildcv-test",
                ["Ai:ApiKey"] = "test-key",
                ["Credits:Enabled"] = "true",
            }));

        builder.ConfigureServices(services =>
        {
            var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAuthenticationService));
            if (authDescriptor is not null)
            {
                services.Remove(authDescriptor);
            }

            services.AddSingleton<IAuthenticationService, FakeOAuthAdapter>();
        });

        return base.CreateHost(builder);
    }
}
