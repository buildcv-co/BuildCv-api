using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildCv.Api.Contracts;
using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Api.IntegrationTests;

public sealed class IterationEndpointsTests : IDisposable
{
    private readonly IterationTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public IterationEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Post_returns_401_when_unauthenticated()
    {
        var body = JsonContent.Create(new
        {
            cvText = "CV content with more than two hundred characters of valid text. " + new string('x', 250),
            vacancyText = "Job description here.",
        });

        var response = await _client.PostAsync("/api/v1/adapt/iterate", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_returns_200_with_valid_auth_and_default_iterations()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await GrantCreditsAsync(accessToken, 25);

        var body = JsonContent.Create(new
        {
            cvText = SAMPLE_CV,
            vacancyText = SAMPLE_VACANCY,
        });

        var response = await SendAuthedAsync(HttpMethod.Post, "/api/v1/adapt/iterate", accessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("requestId").GetGuid().Should().NotBeEmpty();
        result.GetProperty("status").GetString().Should().BeOneOf("Completed", "Failed", "TimedOut");
        result.GetProperty("allSteps").GetArrayLength().Should().BeGreaterThan(0);
        result.GetProperty("creditsConsumed").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Post_returns_200_with_custom_iteration_count()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await GrantCreditsAsync(accessToken, 25);

        var body = JsonContent.Create(new
        {
            cvText = SAMPLE_CV,
            vacancyText = SAMPLE_VACANCY,
            iterationCount = 3,
            probabilityThreshold = 75,
        });

        var response = await SendAuthedAsync(HttpMethod.Post, "/api/v1/adapt/iterate", accessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("creditsConsumed").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Post_returns_422_when_cv_text_is_empty()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var body = JsonContent.Create(new
        {
            cvText = "",
            vacancyText = SAMPLE_VACANCY,
        });

        var response = await SendAuthedAsync(HttpMethod.Post, "/api/v1/adapt/iterate", accessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("error").GetString().Should().Be("VALIDATION/INVALID_INPUT");
    }

    [Fact]
    public async Task Post_returns_402_when_insufficient_credits()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var body = JsonContent.Create(new
        {
            cvText = SAMPLE_CV,
            vacancyText = SAMPLE_VACANCY,
            iterationCount = 20,
        });

        var response = await SendAuthedAsync(HttpMethod.Post, "/api/v1/adapt/iterate", accessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("error").GetString().Should().Be("CREDIT/INSUFFICIENT");
    }

    [Fact]
    public async Task Get_returns_200_when_iteration_cached()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await GrantCreditsAsync(accessToken, 25);

        var postBody = JsonContent.Create(new
        {
            cvText = SAMPLE_CV,
            vacancyText = SAMPLE_VACANCY,
            iterationCount = 2,
        });
        var postResponse = await SendAuthedAsync(HttpMethod.Post, "/api/v1/adapt/iterate", accessToken, postBody);
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var postResult = await postResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = postResult.GetProperty("requestId").GetGuid();

        var getResponse = await SendAuthedAsync(HttpMethod.Get, $"/api/v1/adapt/iterate/{requestId}", accessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        getResult.GetProperty("requestId").GetGuid().Should().Be(requestId);
    }

    [Fact]
    public async Task Get_returns_404_when_iteration_not_found()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var missingId = Guid.NewGuid();
        var response = await SendAuthedAsync(HttpMethod.Get, $"/api/v1/adapt/iterate/{missingId}", accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("error").GetString().Should().Be("ITERATION/NOT_FOUND");
    }

    [Fact]
    public async Task Get_returns_401_when_unauthenticated()
    {
        var response = await _client.GetAsync($"/api/v1/adapt/iterate/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private async Task GrantCreditsAsync(string userAccessToken, int amount)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(userAccessToken);
        var userId = jsonToken.Subject ?? jsonToken.Claims.First(c => c.Type == "sub").Value;

        var adminToken = IssueAdminToken(Guid.NewGuid(), "iter-admin@example.com");
        var grantBody = JsonContent.Create(new
        {
            userId = Guid.Parse(userId),
            amount,
            reason = "test grant for iterations",
            reference = "test-grant",
        });
        var grantResponse = await SendAuthedAsync(HttpMethod.Post, "/api/v1/credits/gift", adminToken, grantBody);
        grantResponse.EnsureSuccessStatusCode();
    }

    private static string IssueAdminToken(Guid userId, string email)
    {
        const string signingKey = "test-signing-key-that-is-long-enough-for-hmac-sha256!";
        const string issuer = "buildcv-test";
        const string audience = "buildcv-test";

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
            new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin"),
            new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
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

    private const string SAMPLE_CV = """
        Mariana López
        Backend developer con 5 años de experiencia en C#, .NET, ASP.NET Core y PostgreSQL.
        He trabajado en RealCorp como ingeniera de software senior, construyendo APIs REST
        con Entity Framework Core y autenticación JWT. También tengo experiencia con Docker,
        AWS Lambda y AWS SQS para arquitecturas serverless. Stack secundario: React, Next.js.
        """;

    private const string SAMPLE_VACANCY = """
        Buscamos backend developer con C# y .NET para equipo fintech en Bogotá.
        Requisitos: ASP.NET Core, Entity Framework Core, PostgreSQL, Docker, AWS.
        Ofrecemos contrato indefinido y trabajo remoto.
        """;
}

public sealed class IterationTestWebApplicationFactory : WebApplicationFactory<Program>
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

            services.AddSingleton<IAuthenticationService, IterationFakeOAuthAdapter>();
        });

        return base.CreateHost(builder);
    }
}

public sealed class IterationFakeOAuthAdapter : IAuthenticationService
{
    public Task<Result<OAuthUserInfo>> ExchangeCodeAsync(
        string provider, string code, string redirectUri, CancellationToken ct = default)
    {
        var userInfo = new OAuthUserInfo(
            Provider: provider,
            ProviderId: $"fake-{provider}-123",
            Email: "iteration-fake@example.com",
            Name: "Iteration Fake User");

        return Task.FromResult(Result.Success(userInfo));
    }
}
