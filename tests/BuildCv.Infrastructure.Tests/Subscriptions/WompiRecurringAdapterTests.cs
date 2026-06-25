using System.Net;
using System.Security.Cryptography;
using System.Text;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class WompiRecurringAdapterTests
{
    private const string TestSecret = "test-webhook-secret-12345";
    private const string TestPublicKey = "pub_test_abc123";
    private const string TestPrivateKey = "prv_test_xyz789";

    private static WompiRecurringAdapter CreateAdapter(HttpMessageHandler handler, string environment = "sandbox")
    {
        var settings = new WompiSettings
        {
            Enabled = true,
            Environment = environment,
            PublicKey = TestPublicKey,
            PrivateKey = TestPrivateKey,
            WebhookSecret = TestSecret,
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) };
        return new WompiRecurringAdapter(
            http,
            Options.Create(settings),
            NullLogger<WompiRecurringAdapter>.Instance);
    }

    [Fact]
    public void Implements_contract()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => OkResponse("{}")));
        adapter.Should().BeAssignableTo<ISubscriptionProvider>();
    }

    [Fact]
    public async Task CreateScheduledChargeAsync_posts_to_subscriptions_endpoint_with_amount_and_charge_date()
    {
        var handler = new CapturingHandler(_ => OkResponse("""{"data":{"id":"wompi-sub-1"}}"""));
        var adapter = CreateAdapter(handler);
        var chargeDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await adapter.CreateScheduledChargeAsync("ps_test_abc", 30_000m, "COP", chargeDate);

        result.Should().Be("wompi-sub-1");
        handler.Requests.Should().HaveCount(1);
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Be("/v1/subscriptions");

        var body = await request.Content!.ReadAsStringAsync();
        body.Should().Contain("\"payment_source_id\":\"ps_test_abc\"");
        body.Should().Contain("\"amount_in_cents\":3000000");
        body.Should().Contain("\"currency\":\"COP\"");
        body.Should().Contain("\"charge_date\":\"2026-07-01\"");
    }

    [Fact]
    public async Task CreateScheduledChargeAsync_sends_bearer_token_with_private_key()
    {
        var handler = new CapturingHandler(_ => OkResponse("""{"data":{"id":"sub-1"}}"""));
        var adapter = CreateAdapter(handler);

        await adapter.CreateScheduledChargeAsync("ps_x", 30_000m, "COP", DateTime.UtcNow);

        var request = handler.Requests[0];
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be(TestPrivateKey);
    }

    [Fact]
    public async Task CancelScheduledChargeAsync_deletes_subscription_with_bearer_token()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);

        var result = await adapter.CancelScheduledChargeAsync("wompi-sub-99");

        result.Should().BeTrue();
        handler.Requests.Should().HaveCount(1);
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Delete);
        request.RequestUri!.AbsolutePath.Should().Be("/v1/subscriptions/wompi-sub-99");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be(TestPrivateKey);
    }

    [Fact]
    public async Task CancelScheduledChargeAsync_returns_false_when_wompi_responds_with_error()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var adapter = CreateAdapter(handler);

        var result = await adapter.CancelScheduledChargeAsync("wompi-sub-missing");

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_true_for_correct_hmac()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => OkResponse("{}")));
        var payload = """{"event":"recurring_charge.successful","data":{"payment_source_id":"ps_1"}}""";
        var expected = ComputeHmac(payload, TestSecret);

        adapter.VerifyWebhookSignature(payload, expected).Should().BeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_false_for_wrong_hmac()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => OkResponse("{}")));

        var payload = """{"event":"recurring_charge.failed"}""";
        var wrong = ComputeHmac(payload, "different-secret");

        adapter.VerifyWebhookSignature(payload, wrong).Should().BeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_false_for_tampered_payload()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => OkResponse("{}")));

        var original = """{"data":{"payment_source_id":"ps_1"}}""";
        var tampered = """{"data":{"payment_source_id":"ps_2"}}""";
        var signature = ComputeHmac(original, TestSecret);

        adapter.VerifyWebhookSignature(tampered, signature).Should().BeFalse();
    }

    private static HttpResponseMessage OkResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

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
