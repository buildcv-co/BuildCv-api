using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildCv.Api.Contracts;
using BuildCv.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Api.IntegrationTests;

public sealed class CreditEndpointsTests : IDisposable
{
    private readonly CreditTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public CreditEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetBalance_returns_200_with_valid_jwt()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/credits/balance", accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("balance").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("recentConsumption").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetBalance_returns_401_without_jwt()
    {
        var response = await _client.GetAsync("/api/v1/credits/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_returns_200_with_valid_jwt()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/credits/history", accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("entries").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetHistory_returns_401_without_jwt()
    {
        var response = await _client.GetAsync("/api/v1/credits/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gift_returns_401_without_jwt()
    {
        var body = JsonContent.Create(new
        {
            userId = Guid.NewGuid(),
            amount = 5,
            reason = "support credit",
        });
        var response = await _client.PostAsync("/api/v1/credits/gift", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gift_returns_403_for_non_admin()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var body = JsonContent.Create(new
        {
            userId = Guid.NewGuid(),
            amount = 5,
            reason = "support credit",
        });
        var response = await SendAuthedAsync(HttpMethod.Post, "/api/v1/credits/gift", accessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gift_returns_400_on_zero_amount()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var body = JsonContent.Create(new
        {
            userId = Guid.NewGuid(),
            amount = 0,
            reason = "support",
        });
        var response = await SendAuthedAsync(HttpMethod.Post, "/api/v1/credits/gift", adminToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("error").GetString().Should().Be("CREDIT/INVALID_AMOUNT");
    }

    [Fact]
    public async Task Gift_credits_a_user_for_admin()
    {
        var adminUserId = Guid.NewGuid();
        var adminToken = IssueAdminToken(adminUserId, "admin@example.com");

        var (userAccessToken, _) = await LoginAndAuthenticate();
        var userId = GetUserIdFromToken(userAccessToken);

        var giftBody = JsonContent.Create(new
        {
            userId,
            amount = 7,
            reason = "promo support",
        });
        var giftResponse = await SendAuthedAsync(HttpMethod.Post, "/api/v1/credits/gift", adminToken, giftBody);
        giftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var balanceResponse = await SendAuthedAsync(HttpMethod.Get, "/api/v1/credits/balance", userAccessToken);
        var balance = await balanceResponse.Content.ReadFromJsonAsync<JsonElement>();
        balance.GetProperty("balance").GetInt32().Should().Be(10);
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

    private static string IssueAdminToken(Guid userId, string email)
    {
        const string signingKey = "test-signing-key-that-is-long-enough-for-hmac-sha256!";
        const string issuer = "buildcv-test";
        const string audience = "buildcv-test";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Guid GetUserIdFromToken(string accessToken)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(accessToken);
        var sub = jsonToken.Subject ?? jsonToken.Claims.First(c => c.Type == "sub").Value;
        return Guid.Parse(sub);
    }

    private Task<HttpResponseMessage> SendAuthedAsync(HttpMethod method, string url, string accessToken, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = body;
        }
        return _client.SendAsync(request);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public sealed class CreditTestWebApplicationFactory : WebApplicationFactory<Program>
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
