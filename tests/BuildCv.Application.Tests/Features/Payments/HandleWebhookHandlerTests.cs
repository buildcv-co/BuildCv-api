using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class HandleWebhookHandlerTests
{
    private readonly TestPaymentStore _store = new();
    private readonly TestPaymentProvider _provider = new();
    private readonly HandleWebhookHandler _handler;

    public HandleWebhookHandlerTests()
    {
        _handler = new HandleWebhookHandler(_store, _provider);
    }

    [Fact]
    public async Task HandleAsync_updates_status_to_approved_on_valid_webhook()
    {
        var payment = CreatePendingPayment("tx-123");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-123", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _store.GetByWompiTransactionIdAsync("tx-123");
        updated!.Status.Should().Be(PaymentStatus.Approved);
        updated.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_rejects_tampered_webhook()
    {
        var payment = CreatePendingPayment("tx-456");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(false);

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-456", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "tampered-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/INVALID_SIGNATURE");
        var unchanged = await _store.GetByWompiTransactionIdAsync("tx-456");
        unchanged!.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task HandleAsync_handles_duplicate_webhook_idempotently()
    {
        var payment = CreatePendingPayment("tx-789");
        payment = payment with { Status = PaymentStatus.Approved, PaidAt = DateTime.UtcNow };
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-789", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_sets_failed_status_on_declined()
    {
        var payment = CreatePendingPayment("tx-decline");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("DECLINED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-decline", "status": "DECLINED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _store.GetByWompiTransactionIdAsync("tx-decline");
        updated!.Status.Should().Be(PaymentStatus.Failed);
    }

    private static Payment CreatePendingPayment(string wompiTransactionId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        PackageId = "starter",
        Credits = 10,
        AmountInCents = 1_500_000,
        Currency = "COP",
        Status = PaymentStatus.Pending,
        WompiTransactionId = wompiTransactionId,
        IdempotencyKey = $"idem-{wompiTransactionId}",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
