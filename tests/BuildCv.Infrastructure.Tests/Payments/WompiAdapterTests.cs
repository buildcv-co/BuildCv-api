using System.Net;
using System.Security.Cryptography;
using System.Text;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.Payments;

public sealed class WompiAdapterTests
{
    private const string TestSecret = "test-webhook-secret-12345";
    private const string TestPublicKey = "pub_test_abc123";
    private const string TestPrivateKey = "prv_test_xyz789";

    private static WompiAdapter CreateAdapter(
        HttpMessageHandler handler,
        string environment = "sandbox")
    {
        var settings = Settings(environment);
        var http = new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) };
        return new WompiAdapter(
            http,
            Options.Create(settings),
            NullLogger<WompiAdapter>.Instance);
    }

    private static WompiSettings Settings(string environment = "sandbox") => new()
    {
        Enabled = true,
        Environment = environment,
        PublicKey = TestPublicKey,
        PrivateKey = TestPrivateKey,
        WebhookSecret = TestSecret,
    };

    [Fact]
    public void BaseUrl_is_sandbox_when_environment_is_sandbox()
    {
        Settings("sandbox").BaseUrl.Should().Be("https://api.wompi.sandbox");
    }

    [Fact]
    public void BaseUrl_is_production_when_environment_is_production()
    {
        Settings("production").BaseUrl.Should().Be("https://api.wompi.co");
    }

    [Fact]
    public void BaseUrl_defaults_to_sandbox_for_unknown_environment()
    {
        Settings("staging").BaseUrl.Should().Be("https://api.wompi.sandbox");
    }

    [Fact]
    public void BaseUrl_is_case_insensitive()
    {
        Settings("PRODUCTION").BaseUrl.Should().Be("https://api.wompi.co");
    }

    [Fact]
    public void VerifyWebhookSignature_returns_true_for_correct_hmac()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var payload = """{"event":"transaction.updated","data":{"id":"tx-1","status":"APPROVED"}}""";
        var expected = ComputeHmac(payload, TestSecret);

        adapter.VerifyWebhookSignature(payload, expected).Should().BeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_false_for_wrong_hmac()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var payload = """{"event":"transaction.updated"}""";
        var wrong = ComputeHmac(payload, "different-secret");

        adapter.VerifyWebhookSignature(payload, wrong).Should().BeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_false_for_tampered_payload()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var original = """{"id":"tx-1"}""";
        var tampered = """{"id":"tx-2"}""";
        var signature = ComputeHmac(original, TestSecret);

        adapter.VerifyWebhookSignature(tampered, signature).Should().BeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_false_for_empty_signature()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        adapter.VerifyWebhookSignature("""{"id":"tx-1"}""", "").Should().BeFalse();
    }

    [Fact]
    public async Task CreateCheckoutAsync_posts_to_payment_links_endpoint()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":{"id":"wompi-link-1","public_key":"pub_test_abc123","amount_in_cents":1500000,"currency":"COP","reference":"ref-1"}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var adapter = CreateAdapter(handler);

        var session = await adapter.CreateCheckoutAsync(
            "user-1",
            CreditPackage.Starter,
            "idem-1",
            CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Be($"/v1/merchants/{TestPublicKey}/payment_links");
        session.SessionId.Should().Be("wompi-link-1");
        session.AmountInCents.Should().Be(1_500_000);
        session.Currency.Should().Be("COP");
        session.Reference.Should().Be("ref-1");
    }

    [Fact]
    public async Task CreateCheckoutAsync_sends_idempotency_header()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":{"id":"wompi-link-1","amount_in_cents":1500000,"currency":"COP","reference":"ref-1"}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var adapter = CreateAdapter(handler);

        await adapter.CreateCheckoutAsync("user-1", CreditPackage.Starter, "my-idem-key", CancellationToken.None);

        handler.Requests[0].Headers.Should().ContainKey("X-Idempotency-Key");
        handler.Requests[0].Headers.GetValues("X-Idempotency-Key").Single().Should().Be("my-idem-key");
    }

    [Fact]
    public async Task CreateCheckoutAsync_sends_body_with_amount_and_reference()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":{"id":"wompi-link-1","amount_in_cents":1500000,"currency":"COP","reference":"ref-1"}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var adapter = CreateAdapter(handler);

        await adapter.CreateCheckoutAsync("user-1", CreditPackage.Starter, "idem-1", CancellationToken.None);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.Should().Contain("\"amount_in_cents\":1500000");
        body.Should().Contain("\"reference\":\"idem-1\"");
        body.Should().Contain("\"currency\":\"COP\"");
    }

    [Fact]
    public async Task GetTransactionStatusAsync_uses_bearer_token_and_returns_status()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":{"id":"tx-1","status":"APPROVED","amount_in_cents":1500000,"currency":"COP"}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var adapter = CreateAdapter(handler);

        var status = await adapter.GetTransactionStatusAsync("tx-1", CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.Should().Be("/v1/transactions/tx-1");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be(TestPrivateKey);
        status.Should().NotBeNull();
        status!.WompiTransactionId.Should().Be("tx-1");
        status.Status.Should().Be("APPROVED");
        status.AmountInCents.Should().Be(1_500_000);
    }

    [Fact]
    public async Task CreateCheckoutAsync_throws_on_error_status()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":"server_error"}""", Encoding.UTF8, "application/json"),
        });
        var adapter = CreateAdapter(handler);

        var act = () => adapter.CreateCheckoutAsync("user-1", CreditPackage.Starter, "idem-1", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateCheckoutAsync_uses_production_base_url_when_configured()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":{"id":"wompi-link-1","amount_in_cents":1500000,"currency":"COP","reference":"ref-1"}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var adapter = CreateAdapter(handler, "production");

        await adapter.CreateCheckoutAsync("user-1", CreditPackage.Starter, "idem-1", CancellationToken.None);

        handler.Requests[0].RequestUri!.Host.Should().Be("api.wompi.co");
    }

    private static string ComputeHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }
}
