using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Api.IntegrationTests;

public sealed class SessionEndpointTests : IDisposable
{
    private readonly SessionTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public SessionEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetSession_Returns200_WithValidNextAuthJwt()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "session@test.com", "Session Tester");

        var nextAuthJwt = SessionTestTokens.CreateNextAuthJwt(userId, "session@test.com");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextAuthJwt);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("jwt").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("expiresAt").GetDateTime().Should().BeAfter(DateTime.UtcNow);
        body.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
        body.GetProperty("user").GetProperty("email").GetString().Should().Be("session@test.com");
        body.GetProperty("user").GetProperty("name").GetString().Should().Be("Session Tester");
    }

    [Fact]
    public async Task GetSession_Returns401_WithoutAuthHeader()
    {
        var response = await _client.GetAsync("/api/v1/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSession_Returns401_WithMalformedBearerToken()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSession_Returns401_WhenNextAuthJwtSignedWithWrongSecret()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "secret@test.com", "Secret Tester");

        var nextAuthJwt = SessionTestTokens.CreateNextAuthJwt(
            userId,
            "secret@test.com",
            SessionTestTokens.Issuer,
            SessionTestTokens.Audience,
            "different-secret-key-still-long-enough-for-hs256!",
            DateTime.UtcNow.AddMinutes(15));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextAuthJwt);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSession_Returns401_WhenNextAuthJwtIsExpired()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "expired@test.com", "Expired Tester");

        var nextAuthJwt = SessionTestTokens.CreateNextAuthJwt(
            userId,
            "expired@test.com",
            SessionTestTokens.Issuer,
            SessionTestTokens.Audience,
            SessionTestTokens.SigningKey,
            DateTime.UtcNow.AddMinutes(-30));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextAuthJwt);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSession_Returns401_WhenUserDeleted_ArcoCompliance()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "arco@test.com", "Arco Tester");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userStore = scope.ServiceProvider.GetRequiredService<IUserDataStore>();
            await userStore.DeleteAsync(userId);
        }

        var nextAuthJwt = SessionTestTokens.CreateNextAuthJwt(userId, "arco@test.com");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextAuthJwt);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSession_GeneratedBackendJwt_AllowsAccessToCreditsBalance()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "balance@test.com", "Balance Tester");

        var nextAuthJwt = SessionTestTokens.CreateNextAuthJwt(userId, "balance@test.com");
        var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        sessionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextAuthJwt);
        var sessionResponse = await _client.SendAsync(sessionRequest);
        sessionResponse.EnsureSuccessStatusCode();

        var sessionBody = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var backendJwt = sessionBody.GetProperty("jwt").GetString();

        var balanceRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/credits/balance");
        balanceRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", backendJwt);
        var balanceResponse = await _client.SendAsync(balanceRequest);

        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var balanceBody = await balanceResponse.Content.ReadFromJsonAsync<JsonElement>();
        balanceBody.GetProperty("balance").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSession_BackendJwt_ContainsExpectedClaims()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "claims@test.com", "Claims Tester");

        var nextAuthJwt = SessionTestTokens.CreateNextAuthJwt(userId, "claims@test.com");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextAuthJwt);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var backendJwt = body.GetProperty("jwt").GetString()!;

        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(backendJwt);

        jsonToken.Subject.Should().Be(userId.ToString());
        jsonToken.Claims.Should().Contain(c => c.Value == "claims@test.com");
        jsonToken.Claims.Should().Contain(c => c.Value == "Claims Tester");
        jsonToken.Issuer.Should().Be("buildcv-test");
        jsonToken.Audiences.Should().Contain("buildcv-test");
        jsonToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    private async Task SeedUserAsync(Guid userId, string email, string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserDataStore>();
        await userStore.UpsertAsync(new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = $"google-{userId:N}",
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

internal static class SessionTestTokens
{
    public const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256-32bytes!";
    public const string Issuer = "buildcv-web-test";
    public const string Audience = "buildcv-api-test";

    public static string CreateNextAuthJwt(Guid userId, string email)
    {
        return CreateNextAuthJwt(userId, email, Issuer, Audience, SigningKey, DateTime.UtcNow.AddMinutes(15));
    }

    public static string CreateNextAuthJwt(
        Guid userId,
        string email,
        string issuer,
        string audience,
        string signingKey,
        DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Email, email),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var notBefore = expiresAt.AddMinutes(-30);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiresAt,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class SessionTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SessionTestTokens.SigningKey,
                ["Jwt:Issuer"] = "buildcv-test",
                ["Jwt:Audience"] = "buildcv-test",
                ["NextAuth:SigningKey"] = SessionTestTokens.SigningKey,
                ["NextAuth:Issuer"] = SessionTestTokens.Issuer,
                ["NextAuth:Audience"] = SessionTestTokens.Audience,
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
