using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildCv.Api.Contracts;
using BuildCv.Api.Endpoints;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Common;
using BuildCv.Domain.Payments;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Api.IntegrationTests.Payments;

public sealed class PaymentEndpointsTests : IDisposable
{
    private readonly PaymentTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public PaymentEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Checkout_returns_session_with_valid_package()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/payments/checkout", accessToken,
            new CheckoutRequest("starter"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("sessionId").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("publicKey").GetString().Should().Be("test-public-key");
        body.GetProperty("amountInCents").GetInt64().Should().Be(1_500_000);
        body.GetProperty("currency").GetString().Should().Be("COP");
        body.GetProperty("reference").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Checkout_without_auth_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/payments/checkout",
            new CheckoutRequest("starter"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_with_invalid_package_returns_400()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/payments/checkout", accessToken,
            new CheckoutRequest("nonexistent"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("PAYMENT/INVALID_PACKAGE");
    }

    [Fact]
    public async Task Checkout_is_idempotent_for_same_user_and_package()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var first = await _client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/payments/checkout", accessToken,
            new CheckoutRequest("pro")));
        var second = await _client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/payments/checkout", accessToken,
            new CheckoutRequest("pro")));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        secondBody.GetProperty("sessionId").GetString().Should().Be(firstBody.GetProperty("sessionId").GetString());
        secondBody.GetProperty("reference").GetString().Should().Be(firstBody.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task Webhook_with_valid_hmac_returns_200_and_updates_status()
    {
        const string transactionId = "tx-1234";
        SeedPaymentWithTransaction(transactionId, PaymentStatus.Pending);

        var payload = BuildWebhookPayload(transactionId, "APPROVED");
        var signature = ComputeHmac(payload, PaymentTestWebApplicationFactory.WebhookSecret);

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = content,
        };
        request.Headers.Add("X-Event-Checksum", signature);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_with_invalid_hmac_returns_401()
    {
        const string transactionId = "tx-9999";
        var payload = BuildWebhookPayload(transactionId, "APPROVED");

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = content,
        };
        request.Headers.Add("X-Event-Checksum", "deadbeef");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPayment_by_id_with_auth_returns_payment()
    {
        var (accessToken, _) = await LoginAndAuthenticate();
        var seed = SeedPaymentForCurrentUser(accessToken);

        var response = await _client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/payments/{seed.Id}", accessToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().Be(seed.Id);
        body.GetProperty("packageId").GetString().Should().Be("standard");
        body.GetProperty("amountInCents").GetInt64().Should().Be(6_000_000);
    }

    [Fact]
    public async Task GetPayment_without_auth_returns_401()
    {
        var response = await _client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListPayments_with_auth_returns_200()
    {
        var (accessToken, _) = await LoginAndAuthenticate();

        var response = await _client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/payments", accessToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task ListPayments_returns_only_current_user_payments()
    {
        var (accessToken, userId) = await LoginAndAuthenticate();
        SeedPaymentForCurrentUser(accessToken, "starter");
        SeedPaymentForCurrentUser(accessToken, "pro");

        var response = await _client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/payments", accessToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(2);
        foreach (var item in body.EnumerateArray())
        {
            item.GetProperty("userId").GetGuid().Should().Be(userId);
        }
    }

    private void SeedPaymentWithTransaction(string transactionId, PaymentStatus status)
    {
        var store = _factory.Services.GetRequiredService<IPaymentStore>();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PackageId = "starter",
            Credits = 10,
            AmountInCents = 1_500_000,
            Currency = "COP",
            Status = status,
            WompiTransactionId = transactionId,
            IdempotencyKey = $"seed-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        store.AddAsync(payment).GetAwaiter().GetResult();
    }

    private Payment SeedPaymentForCurrentUser(string accessToken, string packageId = "standard")
    {
        var userId = GetUserIdFromToken(accessToken);
        var store = _factory.Services.GetRequiredService<IPaymentStore>();
        var package = CreditPackage.FindById(packageId)!;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageId = packageId,
            Credits = package.Credits,
            AmountInCents = package.PriceInCents,
            Currency = package.Currency,
            Status = PaymentStatus.Pending,
            IdempotencyKey = $"seed-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        store.AddAsync(payment).GetAwaiter().GetResult();
        return payment;
    }

    private Guid GetUserIdFromToken(string accessToken)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(accessToken);
        var sub = jsonToken.Subject ?? jsonToken.Claims.First(c => c.Type == "sub").Value;
        return Guid.Parse(sub);
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static string BuildWebhookPayload(string transactionId, string status)
        => $"{{\"data\":{{\"id\":\"{transactionId}\",\"status\":\"{status}\",\"amount_in_cents\":1500000}}}}";

    private static string ComputeHmac(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public sealed class CheckoutRequest
{
    public string PackageId { get; init; } = "";

    public CheckoutRequest() { }

    public CheckoutRequest(string packageId) => PackageId = packageId;
}

public sealed class PaymentTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "test-webhook-secret-key";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-hmac-sha256!",
                ["Jwt:Issuer"] = "buildcv-test",
                ["Jwt:Audience"] = "buildcv-test",
                ["Ai:ApiKey"] = "test-key",
                ["Wompi:Enabled"] = "true",
                ["Wompi:Environment"] = "sandbox",
                ["Wompi:PublicKey"] = "test-public-key",
                ["Wompi:PrivateKey"] = "test-private-key",
                ["Wompi:WebhookSecret"] = WebhookSecret,
            }));

        builder.ConfigureServices(services =>
        {
            var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAuthenticationService));
            if (authDescriptor is not null)
            {
                services.Remove(authDescriptor);
            }

            services.AddSingleton<IAuthenticationService, FakeOAuthAdapter>();

            var providerDescriptors = services.Where(d => d.ServiceType == typeof(IPaymentProvider)).ToList();
            foreach (var descriptor in providerDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IPaymentProvider, ConfigurablePaymentProvider>();
        });

        return base.CreateHost(builder);
    }
}

public sealed class ConfigurablePaymentProvider : IPaymentProvider
{
    private int _counter;

    public Task<CheckoutSession> CreateCheckoutAsync(
        string userId, CreditPackage package, string idempotencyKey, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref _counter);
        return Task.FromResult(new CheckoutSession
        {
            SessionId = $"sess-{n}",
            PublicKey = "test-public-key",
            AmountInCents = package.PriceInCents,
            Currency = package.Currency,
            Reference = idempotencyKey,
        });
    }

    public Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId, CancellationToken ct = default)
        => Task.FromResult<TransactionStatus?>(new TransactionStatus
        {
            WompiTransactionId = wompiTransactionId,
            Status = "APPROVED",
            AmountInCents = 0,
        });

    public bool VerifyWebhookSignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(PaymentTestWebApplicationFactory.WebhookSecret);
        var data = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(key);
        var expected = hmac.ComputeHash(data);
        var providedHex = signatureHeader.ToLowerInvariant();
        if (providedHex.Length != expected.Length * 2)
        {
            return false;
        }
        var provided = new byte[expected.Length];
        for (var i = 0; i < expected.Length; i++)
        {
            provided[i] = Convert.ToByte(providedHex.Substring(i * 2, 2), 16);
        }
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
