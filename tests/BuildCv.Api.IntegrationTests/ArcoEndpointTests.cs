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

public sealed class ArcoEndpointTests : IDisposable
{
    private readonly ArcoTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ArcoEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Access_user_data_returns_profile()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await EnsureConsentGranted(accessToken, "data-access");

        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be("fake@example.com");
        body.GetProperty("name").GetString().Should().Be("Fake User");
        body.GetProperty("provider").GetString().Should().Be("google");
        body.GetProperty("userId").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Access_without_consent_returns_403()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rectify_user_data_updates_fields()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await EnsureConsentGranted(accessToken, "rectification");

        var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/v1/user/data", accessToken,
            new RectifyUserDataRequest(Email: "updated@example.com", Name: null));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be("updated@example.com");
        body.GetProperty("name").GetString().Should().Be("Fake User");
    }

    [Fact]
    public async Task Rectify_name_only_preserves_email()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await EnsureConsentGranted(accessToken, "rectification");

        var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/v1/user/data", accessToken,
            new RectifyUserDataRequest(Email: null, Name: "New Name"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be("fake@example.com");
        body.GetProperty("name").GetString().Should().Be("New Name");
    }

    [Fact]
    public async Task Rectify_without_consent_returns_403()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/v1/user/data", accessToken,
            new RectifyUserDataRequest(Email: "hacked@example.com", Name: null));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_user_data_removes_profile()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await EnsureConsentGranted(accessToken, "data-access");

        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/v1/user/data", accessToken);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_revokes_all_consent()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        await EnsureConsentGranted(accessToken, "data-access");

        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/v1/user/data", accessToken);
        await _client.SendAsync(deleteRequest);

        var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_without_consent_returns_403()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/v1/user/data", accessToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Full_arco_lifecycle_access_rectify_delete()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        await EnsureConsentGranted(accessToken, "data-access");
        await EnsureConsentGranted(accessToken, "rectification");

        var accessRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var accessResponse = await _client.SendAsync(accessRequest);
        accessResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessBody = await accessResponse.Content.ReadFromJsonAsync<JsonElement>();
        accessBody.GetProperty("email").GetString().Should().Be("fake@example.com");

        var rectifyRequest = CreateAuthenticatedRequest(HttpMethod.Put, "/api/v1/user/data", accessToken,
            new RectifyUserDataRequest(Email: "rectified@example.com", Name: "Rectified Name"));
        var rectifyResponse = await _client.SendAsync(rectifyRequest);
        rectifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rectifyBody = await rectifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        rectifyBody.GetProperty("email").GetString().Should().Be("rectified@example.com");
        rectifyBody.GetProperty("name").GetString().Should().Be("Rectified Name");

        var verifyRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var verifyResponse = await _client.SendAsync(verifyRequest);
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        verifyBody.GetProperty("email").GetString().Should().Be("rectified@example.com");

        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/v1/user/data", accessToken);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deletedRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/user/data", accessToken);
        var deletedResponse = await _client.SendAsync(deletedRequest);
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task EnsureConsentGranted(string accessToken, string purpose)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/user/data/consent", accessToken,
            new ConsentRequest(purpose));
        var response = await _client.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
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

public sealed class ArcoTestWebApplicationFactory : WebApplicationFactory<Program>
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
