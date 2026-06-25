using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Api.IntegrationTests;

public sealed class FeatureFlagAdminEndpointsTests : IDisposable
{
    private readonly Factory _factory;
    private readonly HttpClient _client;

    public FeatureFlagAdminEndpointsTests()
    {
        _factory = new Factory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_list_returns_401_without_jwt()
    {
        var response = await _client.GetAsync("/api/v1/admin/feature-flags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_list_returns_403_for_non_admin()
    {
        var userToken = IssueUserToken(Guid.NewGuid(), "user@example.com");

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags", userToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_list_returns_200_with_valid_admin_auth()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("flags").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("flags").GetArrayLength().Should().BeGreaterThan(0,
            "the 3 default flags from appsettings (factus-enabled, wompi-enabled, credits-enabled) must be seeded");
    }

    [Fact]
    public async Task Get_list_returns_flags_sorted_alphabetically()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags", adminToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var flags = body.GetProperty("flags").EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();
        flags.Should().BeInAscendingOrder("feature flags must be sorted by name for deterministic operator UX");
    }

    [Fact]
    public async Task Get_single_returns_200_when_flag_exists()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("wompi-enabled");
        body.GetProperty("defaultValue").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Get_single_returns_404_when_flag_missing()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/nonexistent-flag-xyz", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_returns_401_without_jwt()
    {
        var body = JsonContent.Create(new { value = false, reason = "test" });
        var response = await _client.PutAsync("/api/v1/admin/feature-flags/wompi-enabled", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_returns_403_for_non_admin()
    {
        var userToken = IssueUserToken(Guid.NewGuid(), "user@example.com");
        var body = JsonContent.Create(new { value = false, reason = "test" });

        var response = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", userToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_returns_404_for_unknown_flag()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");
        var body = JsonContent.Create(new { value = true, reason = "test" });

        var response = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/ghost-flag-999", adminToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("error").GetString().Should().Be("FEATURE_FLAG/NOT_FOUND");
    }

    [Fact]
    public async Task Put_updates_value_and_persists_audit_log()
    {
        var adminId = Guid.NewGuid();
        var adminToken = IssueAdminToken(adminId, "admin@example.com");

        var putBody = JsonContent.Create(new { value = false, reason = "incident P1-273" });
        var putResponse = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, putBody);

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var putBodyJson = await putResponse.Content.ReadFromJsonAsync<JsonElement>();
        putBodyJson.GetProperty("currentValue").GetBoolean().Should().BeFalse();
        putBodyJson.GetProperty("updatedBy").GetGuid().Should().Be(adminId);

        var getResponse = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled", adminToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        getBody.GetProperty("currentValue").GetBoolean().Should().BeFalse(
            "the new value must persist across reads (audit log + flag row updated in same transaction)");

        var auditResponse = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled/audit-log", adminToken);
        var auditBody = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        auditBody.GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
        var firstEntry = auditBody.GetProperty("entries")[0];
        firstEntry.GetProperty("newValue").GetBoolean().Should().BeFalse();
        firstEntry.GetProperty("oldValue").GetBoolean().Should().BeTrue();
        firstEntry.GetProperty("changedBy").GetGuid().Should().Be(adminId);
        firstEntry.GetProperty("reason").GetString().Should().Be("incident P1-273");
    }

    [Fact]
    public async Task Put_invalidates_cache_so_next_read_returns_new_value()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var initial = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled", adminToken);
        var initialBody = await initial.Content.ReadFromJsonAsync<JsonElement>();
        var initialValue = initialBody.GetProperty("currentValue").GetBoolean();

        var flipped = !initialValue;
        var putBody = JsonContent.Create(new { value = flipped, reason = "cache invalidation test" });
        var putResponse = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, putBody);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterPut = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled", adminToken);
        var afterBody = await afterPut.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.GetProperty("currentValue").GetBoolean().Should().Be(flipped,
            "after admin update, the GET must return the freshly persisted value (cache invalidated synchronously by UpdateFeatureFlagHandler)");
    }

    [Fact]
    public async Task Put_preserves_defaultValue_after_update()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var putBody = JsonContent.Create(new { value = false, reason = "defaultValue preservation test" });
        var putResponse = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, putBody);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var putBodyJson = await putResponse.Content.ReadFromJsonAsync<JsonElement>();

        putBodyJson.GetProperty("defaultValue").GetBoolean().Should().BeTrue(
            "PUT only changes currentValue — defaultValue stays at appsettings seed (true for wompi-enabled)");
        putBodyJson.GetProperty("currentValue").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Audit_log_returns_200_paginated()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        for (var i = 0; i < 3; i++)
        {
            var putBody = JsonContent.Create(new { value = i % 2 == 0, reason = $"flip #{i}" });
            var putResponse = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, putBody);
            putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled/audit-log?limit=2", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("entries").GetArrayLength().Should().BeLessThanOrEqualTo(2);
        body.TryGetProperty("nextCursor", out _).Should().BeTrue(
            "with 3 entries and limit=2, nextCursor must be present for keyset pagination");
    }

    [Fact]
    public async Task Audit_log_returns_empty_for_unknown_flag()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        var response = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/never-touched-flag/audit-log", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("entries").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Put_returns_429_after_rate_limit_exceeded()
    {
        var adminToken = IssueAdminToken(Guid.NewGuid(), "admin@example.com");

        for (var i = 0; i < 30; i++)
        {
            var body = JsonContent.Create(new { value = i % 2 == 0, reason = $"burst {i}" });
            var response = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, body);
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"requests 1..30 must succeed (admin policy = 30/min/IP). Failed at request #{i + 1}");
        }

        var body31 = JsonContent.Create(new { value = true, reason = "overflow" });
        var response31 = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, body31);
        response31.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "request 31 must be rejected by the admin rate limit policy (30/min/IP, Art. VII)");
    }

    [Fact]
    public async Task Audit_log_response_does_not_leak_email_or_pii()
    {
        var adminId = Guid.NewGuid();
        var adminToken = IssueAdminToken(adminId, "secret-admin@example.com");

        var putBody = JsonContent.Create(new { value = false, reason = "PII test" });
        var putResponse = await SendAuthedAsync(HttpMethod.Put, "/api/v1/admin/feature-flags/wompi-enabled", adminToken, putBody);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auditResponse = await SendAuthedAsync(HttpMethod.Get, "/api/v1/admin/feature-flags/wompi-enabled/audit-log", adminToken);
        var raw = await auditResponse.Content.ReadAsStringAsync();
        raw.Should().NotContain("secret-admin@example.com",
            "audit log entries expose userId (Guid) only, never email or PII (Art. III — privacy in API responses)");
        raw.Should().Contain(adminId.ToString(),
            "audit log MUST include userId (Guid) so operators know who changed what (Art. IX compliance evidence)");
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

    private static string IssueUserToken(Guid userId, string email)
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

    public sealed class Factory : WebApplicationFactory<Program>
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
                    ["Persistence:Provider"] = "InMemory",
                    ["FeatureFlags:CacheTtlSeconds"] = "60",
                    ["FeatureFlags:Defaults:factus-enabled"] = "false",
                    ["FeatureFlags:Defaults:wompi-enabled"] = "true",
                    ["FeatureFlags:Defaults:credits-enabled"] = "true",
                }));

            return base.CreateHost(builder);
        }
    }
}
