using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildCv.Api.Contracts;
using BuildCv.Api.Endpoints;
using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Api.IntegrationTests;

public sealed class AuthEndpointTests(AuthTestWebApplicationFactory factory)
    : IClassFixture<AuthTestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Google_login_returns_access_and_refresh_tokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/google",
            new OAuthCallbackRequest("test-auth-code"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("user").GetProperty("userId").GetGuid().Should().NotBe(Guid.Empty);
        body.GetProperty("user").GetProperty("email").GetString().Should().Be("fake@example.com");
        body.GetProperty("user").GetProperty("name").GetString().Should().Be("Fake User");
        body.GetProperty("user").GetProperty("provider").GetString().Should().Be("google");
    }

    [Fact]
    public async Task LinkedIn_login_returns_access_and_refresh_tokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/linkedin",
            new OAuthCallbackRequest("test-auth-code"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("user").GetProperty("provider").GetString().Should().Be("linkedin");
    }

    [Fact]
    public async Task Auth_me_with_valid_token_returns_user_profile()
    {
        var tokenResponse = await LoginViaGoogle();
        var accessToken = tokenResponse.GetProperty("accessToken").GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().NotBe(Guid.Empty);
        body.GetProperty("email").GetString().Should().Be("fake@example.com");
    }

    [Fact]
    public async Task Auth_me_without_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_me_with_invalid_token_returns_401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_returns_new_tokens()
    {
        var loginResponse = await LoginViaGoogle();
        var refreshToken = loginResponse.GetProperty("refreshToken").GetString()!;

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(refreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var newAccessToken = body.GetProperty("accessToken").GetString();
        var newRefreshToken = body.GetProperty("refreshToken").GetString();
        newAccessToken.Should().NotBeNullOrWhiteSpace();
        newRefreshToken.Should().NotBeNullOrWhiteSpace();
        newRefreshToken.Should().NotBe(refreshToken, "refresh token should be rotated");
    }

    [Fact]
    public async Task Refresh_with_invalid_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest("invalid-refresh-token"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_rotation_old_token_invalidated()
    {
        var loginResponse = await LoginViaGoogle();
        var oldRefreshToken = loginResponse.GetProperty("refreshToken").GetString()!;

        await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(oldRefreshToken));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(oldRefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var loginResponse = await LoginViaGoogle();
        var refreshToken = loginResponse.GetProperty("refreshToken").GetString()!;

        var logoutResponse = await _client.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest(refreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(refreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WebSignup_Returns200_WithUserId_WhenNewProvider()
    {
        var response = await PostWebSignupWithBffKey(
            new WebSignupRequest("google", "g-new-1", "ada@example.com", "Ada"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task WebSignup_Returns400_OnUnknownProvider()
    {
        var response = await PostWebSignupWithBffKey(
            new WebSignupRequest("facebook", "fb-1", "x@y.co", "X"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WebSignup_Returns400_OnInvalidEmail()
    {
        var response = await PostWebSignupWithBffKey(
            new WebSignupRequest("google", "g-1", "not-an-email", "X"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WebSignup_IsIdempotent_SameUserIdOnSecondCall()
    {
        var first = await PostWebSignupWithBffKey(
            new WebSignupRequest("google", "g-idem-1", "idem@example.com", "Idem"));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstUserId = (await first.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("userId").GetGuid();

        var second = await PostWebSignupWithBffKey(
            new WebSignupRequest("google", "g-idem-1", "idem-updated@example.com", "Idem Updated"));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondUserId = (await second.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("userId").GetGuid();

        secondUserId.Should().Be(firstUserId);
    }

    [Fact]
    public async Task WebSignup_Returns401_WithoutBffKey()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/web-signup",
            new WebSignupRequest("google", "g-nokey-1", "nokey@example.com", "NoKey"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WebSignup_Returns401_WithInvalidBffKey()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/web-signup")
        {
            Content = JsonContent.Create(new WebSignupRequest("google", "g-badkey-1", "bad@example.com", "Bad")),
        };
        request.Headers.Add("X-BFF-Key", "definitely-not-the-real-key");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> PostWebSignupWithBffKey(WebSignupRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/web-signup")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-BFF-Key", AuthTestWebApplicationFactory.BffApiKey);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Logout_WithBearerOnlyBody_RevokesAllRefreshTokens_ForUser()
    {
        var loginResponse = await LoginViaGoogle();
        var accessToken = loginResponse.GetProperty("accessToken").GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResponse = await _client.SendAsync(request);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(loginResponse.GetProperty("refreshToken").GetString()!));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshTokenRotation_PreservedAfterRevokeAll()
    {
        var loginResponse = await LoginViaGoogle();
        var firstRefresh = loginResponse.GetProperty("refreshToken").GetString()!;

        var firstRefreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(firstRefresh));
        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondRefresh = (await firstRefreshResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refreshToken").GetString()!;

        var accessToken = loginResponse.GetProperty("accessToken").GetString()!;
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reusedResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(secondRefresh));
        reusedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Full_flow_login_protected_refresh_logout()
    {
        var loginResponse = await LoginViaGoogle();
        var accessToken = loginResponse.GetProperty("accessToken").GetString()!;
        var refreshToken = loginResponse.GetProperty("refreshToken").GetString()!;

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var meResponse = await _client.SendAsync(meRequest);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(refreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newAccessToken = newTokens.GetProperty("accessToken").GetString()!;
        var newRefreshToken = newTokens.GetProperty("refreshToken").GetString()!;

        var meRequest2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAccessToken);
        var meResponse2 = await _client.SendAsync(meRequest2);
        meResponse2.StatusCode.Should().Be(HttpStatusCode.OK);

        var logoutResponse = await _client.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest(newRefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshAfterLogout = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(newRefreshToken));
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<JsonElement> LoginViaGoogle()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/google",
            new OAuthCallbackRequest("test-auth-code"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }
}

public sealed class AuthTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string BffApiKey = "test-bff-key-for-bff-auth-patch-a";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-hmac-sha256!",
                ["Jwt:Issuer"] = "buildcv-test",
                ["Jwt:Audience"] = "buildcv-test",
                ["Ai:ApiKey"] = "test-key",
                ["Auth:BffApiKey"] = BffApiKey,
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

public sealed class FakeOAuthAdapter : IAuthenticationService
{
    public Task<Result<OAuthUserInfo>> ExchangeCodeAsync(
        string provider, string code, string redirectUri, CancellationToken ct = default)
    {
        var userInfo = new OAuthUserInfo(
            Provider: provider,
            ProviderId: $"fake-{provider}-123",
            Email: "fake@example.com",
            Name: "Fake User");

        return Task.FromResult(Result.Success(userInfo));
    }
}
