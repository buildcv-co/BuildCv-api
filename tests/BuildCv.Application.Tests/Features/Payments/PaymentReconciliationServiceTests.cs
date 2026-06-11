using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Invoicing;
using BuildCv.Domain.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class PaymentReconciliationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_finds_pending_payments_older_than_five_minutes()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-1");
        var recent = CreatePayment(PaymentStatus.Pending, ageMinutes: 1, wompiTxId: "recent-1");
        await store.AddAsync(stale);
        await store.AddAsync(recent);

        await service.ReconcileAsync(CancellationToken.None);

        provider.GetTransactionCalls.Should().Contain("stale-1");
        provider.GetTransactionCalls.Should().NotContain("recent-1");
    }

    [Fact]
    public async Task ReconcileAsync_skips_payments_without_wompi_transaction_id()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var orphan = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: null);
        await store.AddAsync(orphan);

        await service.ReconcileAsync(CancellationToken.None);

        provider.GetTransactionCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_skips_non_pending_payments()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var approved = CreatePayment(PaymentStatus.Approved, ageMinutes: 30, wompiTxId: "approved-1");
        var failed = CreatePayment(PaymentStatus.Failed, ageMinutes: 30, wompiTxId: "failed-1");
        var error = CreatePayment(PaymentStatus.Error, ageMinutes: 30, wompiTxId: "error-1");
        await store.AddAsync(approved);
        await store.AddAsync(failed);
        await store.AddAsync(error);

        await service.ReconcileAsync(CancellationToken.None);

        provider.GetTransactionCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_updates_payment_status_to_approved_when_wompi_returns_approved()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-2");
        await store.AddAsync(stale);
        provider.SetTransactionStatus("APPROVED");

        await service.ReconcileAsync(CancellationToken.None);

        var updated = await store.GetByWompiTransactionIdAsync("stale-2");
        updated!.Status.Should().Be(PaymentStatus.Approved);
        updated.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReconcileAsync_updates_payment_status_to_failed_when_wompi_returns_declined()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-3");
        await store.AddAsync(stale);
        provider.SetTransactionStatus("DECLINED");

        await service.ReconcileAsync(CancellationToken.None);

        var updated = await store.GetByWompiTransactionIdAsync("stale-3");
        updated!.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task ReconcileAsync_updates_payment_status_to_error_when_wompi_returns_error()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-4");
        await store.AddAsync(stale);
        provider.SetTransactionStatus("ERROR");

        await service.ReconcileAsync(CancellationToken.None);

        var updated = await store.GetByWompiTransactionIdAsync("stale-4");
        updated!.Status.Should().Be(PaymentStatus.Error);
    }

    [Fact]
    public async Task ReconcileAsync_keeps_status_pending_when_wompi_returns_pending()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-5");
        await store.AddAsync(stale);
        provider.SetTransactionStatus("PENDING");

        await service.ReconcileAsync(CancellationToken.None);

        var updated = await store.GetByWompiTransactionIdAsync("stale-5");
        updated!.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task ReconcileAsync_creates_invoice_when_status_transitions_to_approved()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-inv");
        await store.AddAsync(stale);
        provider.SetTransactionStatus("APPROVED");

        await service.ReconcileAsync(CancellationToken.None);

        invoices.CreatedInvoices.Should().HaveCount(1);
        invoices.CreatedInvoices[0].AmountInCents.Should().Be(stale.AmountInCents);
    }

    [Fact]
    public async Task ReconcileAsync_does_not_create_invoice_when_status_remains_non_approved()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-noinv");
        await store.AddAsync(stale);
        provider.SetTransactionStatus("DECLINED");

        await service.ReconcileAsync(CancellationToken.None);

        invoices.CreatedInvoices.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_swallows_provider_exceptions_and_continues()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        var stale1 = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-A");
        var stale2 = CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "stale-B");
        await store.AddAsync(stale1);
        await store.AddAsync(stale2);
        provider.ThrowOnGetTransactionFor("stale-A");
        provider.SetTransactionStatus("APPROVED");

        await service.ReconcileAsync(CancellationToken.None);

        var updatedB = await store.GetByWompiTransactionIdAsync("stale-B");
        updatedB!.Status.Should().Be(PaymentStatus.Approved);
    }

    [Fact]
    public async Task ReconcileAsync_returns_count_of_reconciled_payments()
    {
        var store = new TestPaymentStore();
        var provider = new TestPaymentProvider();
        var invoices = new TestInvoiceProvider();
        var service = new PaymentReconciliationService(store, provider, invoices, NullLogger<PaymentReconciliationService>.Instance);

        await store.AddAsync(CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "r-1"));
        await store.AddAsync(CreatePayment(PaymentStatus.Pending, ageMinutes: 10, wompiTxId: "r-2"));
        await store.AddAsync(CreatePayment(PaymentStatus.Pending, ageMinutes: 1, wompiTxId: "r-3"));
        provider.SetTransactionStatus("APPROVED");

        var reconciled = await service.ReconcileAsync(CancellationToken.None);

        reconciled.Should().Be(2);
    }

    private static Payment CreatePayment(PaymentStatus status, int ageMinutes, string? wompiTxId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        PackageId = "starter",
        Credits = 10,
        AmountInCents = 1_500_000,
        Currency = "COP",
        Status = status,
        WompiTransactionId = wompiTxId,
        IdempotencyKey = $"idem-{Guid.NewGuid():N}",
        CreatedAt = DateTime.UtcNow.AddMinutes(-ageMinutes),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-ageMinutes)
    };
}
