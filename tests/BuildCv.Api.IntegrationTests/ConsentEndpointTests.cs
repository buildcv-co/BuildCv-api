using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildCv.Api.Contracts;
using BuildCv.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Api.IntegrationTests;

public sealed class ConsentEndpointTests : IDisposable
{
    private readonly ConsentTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ConsentEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Grant_consent_returns_success()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest("data-access"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("Consent granted");
        body.GetProperty("consentId").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Grant_consent_twice_returns_409()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request1 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest("data-access"));
        await _client.SendAsync(request1);

        var request2 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest("data-access"));
        var response = await _client.SendAsync(request2);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Revoke_consent_returns_success()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var grantRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest("data-access"));
        await _client.SendAsync(grantRequest);

        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent/revoke", accessToken,
            new ConsentRequest("data-access"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("Consent revoked");
    }

    [Fact]
    public async Task Revoke_nonexistent_consent_returns_403()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent/revoke", accessToken,
            new ConsentRequest("nonexistent-purpose"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Data_access_without_consent_returns_403()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task After_grant_data_access_succeeds()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var grantRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest("data-access"));
        await _client.SendAsync(grantRequest);

        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be("fake@example.com");
    }

    [Fact]
    public async Task After_revoke_data_access_blocked()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var grantRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest("data-access"));
        await _client.SendAsync(grantRequest);

        var revokeRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent/revoke", accessToken,
            new ConsentRequest("data-access"));
        await _client.SendAsync(revokeRequest);

        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Privacy_policy_endpoint_returns_policy_without_auth()
    {
        var response = await _client.GetAsync("/api/v1/privacy-policy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("version").GetInt32().Should().Be(1);
        body.GetProperty("content").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private async Task<(string AccessToken, Guid UserId)> LoginAndAuthenticate()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/google",
            new OAuthCallbackRequest("test-auth-code"));
        loginResponse.EnsureSuccessStatusCode();

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;
        var userId = loginBody.GetProperty("user").GetProperty("userId").GetGuid();

        return (accessToken, userId);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public sealed class ConsentTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-hmac-sha256!",
                ["Jwt:Issuer"] = "buildcv-test",
                ["Jwt:Audience"] = "buildcv-test",
                ["Ai:ApiKey"] = "test-key",
            }));

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAuthenticationService));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IAuthenticationService, FakeOAuthAdapter>();
        });

        return base.CreateHost(builder);
    }
}
