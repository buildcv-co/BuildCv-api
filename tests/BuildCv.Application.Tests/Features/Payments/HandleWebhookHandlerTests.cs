using BuildCv.Application.Common;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Payments;
using BuildCv.Application.Tests.Credits;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class HandleWebhookHandlerTests
{
    private readonly TestPaymentStore _store = new();
    private readonly TestPaymentProvider _provider = new();
    private readonly TestInvoiceProvider _invoices = new();
    private readonly TestCreditLedger _credits = new();
    private readonly AlwaysEnabledFeatureFlag _featureFlag = new();
    private readonly HandleWebhookHandler _handler;

    public HandleWebhookHandlerTests()
    {
        _handler = new HandleWebhookHandler(
            _store,
            _provider,
            _invoices,
            _credits,
            _featureFlag,
            NullLogger<HandleWebhookHandler>.Instance);
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
        _invoices.CreatedInvoices.Should().BeEmpty();
        _credits.AllEntries.Should().BeEmpty();
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

    [Fact]
    public async Task HandleAsync_creates_invoice_on_approved_webhook()
    {
        var payment = CreatePendingPayment("tx-invoice");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-invoice", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _invoices.CreatedInvoices.Should().HaveCount(1);
        _invoices.CreatedInvoices[0].AmountInCents.Should().Be(payment.AmountInCents);
        _invoices.CreatedInvoices[0].UserId.Should().Be(payment.UserId);
        _invoices.CreatedInvoices[0].Currency.Should().Be(payment.Currency);
    }

    [Fact]
    public async Task HandleAsync_does_not_create_invoice_when_invoice_provider_is_null()
    {
        var handler = new HandleWebhookHandler(
            _store,
            _provider,
            invoiceProvider: null,
            _credits,
            _featureFlag,
            NullLogger<HandleWebhookHandler>.Instance);

        var payment = CreatePendingPayment("tx-noinvprov");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-noinvprov", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _store.GetByWompiTransactionIdAsync("tx-noinvprov");
        updated!.Status.Should().Be(PaymentStatus.Approved);
        _invoices.CreatedInvoices.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_does_not_create_invoice_on_non_approved_status()
    {
        var payment = CreatePendingPayment("tx-declined-noinv");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("DECLINED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-declined-noinv", "status": "DECLINED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _invoices.CreatedInvoices.Should().BeEmpty();
        _credits.AllEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_credits_user_on_approved_when_feature_flag_enabled()
    {
        var payment = CreatePendingPayment("tx-credit");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-credit", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _credits.AllEntries.Should().HaveCount(1);
        var entry = _credits.AllEntries.Single();
        entry.Reason.Should().Be(CreditLedgerReason.Purchase);
        entry.Reference.Should().Be($"payment:{payment.Id}");
        entry.Delta.Should().Be(payment.Credits);
        entry.UserId.Should().Be(payment.UserId);
        (await _credits.GetBalanceAsync(payment.UserId, CancellationToken.None)).Should().Be(payment.Credits);
    }

    [Fact]
    public async Task HandleAsync_does_not_credit_user_when_feature_flag_disabled()
    {
        var disabledHandler = new HandleWebhookHandler(
            _store,
            _provider,
            _invoices,
            _credits,
            new AlwaysDisabledFeatureFlag(),
            NullLogger<HandleWebhookHandler>.Instance);

        var payment = CreatePendingPayment("tx-flagoff");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-flagoff", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await disabledHandler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _credits.AllEntries.Should().BeEmpty();
        _invoices.CreatedInvoices.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_does_not_fail_webhook_when_credit_grant_throws()
    {
        var throwingCredits = new ThrowingCreditLedger();
        var handlerWithThrowingCredits = new HandleWebhookHandler(
            _store,
            _provider,
            _invoices,
            throwingCredits,
            _featureFlag,
            NullLogger<HandleWebhookHandler>.Instance);

        var payment = CreatePendingPayment("tx-throw");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-throw", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        var result = await handlerWithThrowingCredits.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _store.GetByWompiTransactionIdAsync("tx-throw");
        updated!.Status.Should().Be(PaymentStatus.Approved);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_when_credit_grant_replays()
    {
        var payment = CreatePendingPayment("tx-replay");
        await _store.AddAsync(payment);

        _provider.SetWebhookSignatureValid(true);
        _provider.SetTransactionStatus("APPROVED");

        var command = new HandleWebhookCommand
        {
            Payload = """{"transaction": {"id": "tx-replay", "status": "APPROVED", "amount_in_cents": 1500000}}""",
            SignatureHeader = "valid-hmac"
        };

        await _handler.HandleAsync(command, CancellationToken.None);
        await _handler.HandleAsync(command, CancellationToken.None);

        _credits.AllEntries.Should().HaveCount(1);
        (await _credits.GetBalanceAsync(payment.UserId, CancellationToken.None)).Should().Be(payment.Credits);
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

internal sealed class AlwaysEnabledFeatureFlag : ICreditsFeatureFlag
{
    public bool IsEnabled => true;
}

internal sealed class AlwaysDisabledFeatureFlag : ICreditsFeatureFlag
{
    public bool IsEnabled => false;
}

internal sealed class ThrowingCreditLedger : ICreditLedger
{
    public Task<CreditLedgerEntry> AccreditAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        int delta,
        int balanceAfter,
        string? metadata,
        CancellationToken ct)
        => throw new InvalidOperationException("Simulated ledger failure for test.");

    public Task<CreditLedgerEntry?> FindByReferenceAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        CancellationToken ct)
        => Task.FromResult<CreditLedgerEntry?>(null);

    public Task<int> GetBalanceAsync(Guid userId, CancellationToken ct)
        => Task.FromResult(0);

    public Task<IReadOnlyList<CreditLedgerEntry>> GetHistoryAsync(
        Guid userId,
        int limit,
        CreditCursorPosition? before,
        CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CreditLedgerEntry>>([]);

    public Task<int> CountConsumptionsSinceAsync(Guid userId, DateTime since, CancellationToken ct)
        => Task.FromResult(0);
}
