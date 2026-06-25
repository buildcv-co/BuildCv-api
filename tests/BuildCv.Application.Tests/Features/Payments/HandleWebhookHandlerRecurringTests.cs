using System.Text.Json;
using BuildCv.Application.Common;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Payments;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Application.Tests.Credits;
using BuildCv.Application.Tests.Features.Subscriptions;
using BuildCv.Domain.Common;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Payments;
using BuildCv.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class HandleWebhookHandlerRecurringTests
{
    [Fact]
    public async Task RecurringChargeSuccessful_dispatches_to_HandleRecurringChargeHandler_and_advances_period()
    {
        var paymentStore = new TestPaymentStore();
        var subscriptionStore = new TestSubscriptionStore();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var chargeHandler = new HandleRecurringChargeHandler(subscriptionStore, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var provider = new NoopPaymentProvider();

        var userId = Guid.NewGuid();
        var sub = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_target", DateTime.UtcNow);
        await subscriptionStore.UpsertAsync(sub);

        var handler = new HandleWebhookHandler(
            paymentStore,
            provider,
            invoiceProvider: null,
            creditLedger: null,
            new NoopCreditsFeatureFlag(),
            chargeHandler,
            NullLogger<HandleWebhookHandler>.Instance);

        var payload = """{"event":"recurring_charge.successful","data":{"payment_source_id":"ps_target","charge_id":"ch_abc"}}""";

        var result = await handler.HandleAsync(new HandleWebhookCommand
        {
            Payload = payload,
            SignatureHeader = "valid-sig",
        });

        result.IsSuccess.Should().BeTrue();
        var refreshed = await subscriptionStore.GetByPaymentSourceIdAsync("ps_target");
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(SubscriptionStatus.Active);
        refreshed.CurrentPeriodStart.Should().Be(sub.CurrentPeriodEnd);
        ledger.AllEntries.Should().HaveCount(1);
        ledger.AllEntries.Single().Delta.Should().Be(30);
    }

    [Fact]
    public async Task RecurringChargeFailed_transitions_subscription_to_past_due()
    {
        var paymentStore = new TestPaymentStore();
        var subscriptionStore = new TestSubscriptionStore();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var chargeHandler = new HandleRecurringChargeHandler(subscriptionStore, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var provider = new NoopPaymentProvider();

        var userId = Guid.NewGuid();
        var sub = Subscription.Create(userId, SubscriptionPlan.Standard, "ps_target2", DateTime.UtcNow);
        await subscriptionStore.UpsertAsync(sub);

        var handler = new HandleWebhookHandler(
            paymentStore,
            provider,
            invoiceProvider: null,
            creditLedger: null,
            new NoopCreditsFeatureFlag(),
            chargeHandler,
            NullLogger<HandleWebhookHandler>.Instance);

        var payload = """{"event":"recurring_charge.failed","data":{"payment_source_id":"ps_target2","charge_id":"ch_xyz","reason":"card_declined"}}""";

        var result = await handler.HandleAsync(new HandleWebhookCommand
        {
            Payload = payload,
            SignatureHeader = "valid-sig",
        });

        result.IsSuccess.Should().BeTrue();
        var refreshed = await subscriptionStore.GetByPaymentSourceIdAsync("ps_target2");
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(SubscriptionStatus.PastDue);
        refreshed.RetryCount.Should().Be(1);
        ledger.AllEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task OneTimePayment_still_works_with_recurring_handler_present()
    {
        var paymentStore = new TestPaymentStore();
        var payment = NewPayment("wompi-tx-1");
        await paymentStore.AddAsync(payment);

        var provider = new NoopPaymentProvider();
        var subscriptionStore = new TestSubscriptionStore();
        var chargeHandler = new HandleRecurringChargeHandler(
            subscriptionStore,
            new AccreditPurchaseHandler(new TestCreditLedger()),
            NullLogger<HandleRecurringChargeHandler>.Instance);

        var handler = new HandleWebhookHandler(
            paymentStore,
            provider,
            invoiceProvider: null,
            creditLedger: null,
            new NoopCreditsFeatureFlag(),
            chargeHandler,
            NullLogger<HandleWebhookHandler>.Instance);

        var payload = """{"event":"transaction.updated","data":{"id":"wompi-tx-1","status":"APPROVED"}}""";

        var result = await handler.HandleAsync(new HandleWebhookCommand
        {
            Payload = payload,
            SignatureHeader = "valid-sig",
        });

        result.IsSuccess.Should().BeTrue();
        var refreshed = await paymentStore.GetByWompiTransactionIdAsync("wompi-tx-1");
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(PaymentStatus.Approved);
    }

    [Fact]
    public async Task RecurringEvent_with_invalid_signature_returns_failure()
    {
        var paymentStore = new TestPaymentStore();
        var subscriptionStore = new TestSubscriptionStore();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var chargeHandler = new HandleRecurringChargeHandler(subscriptionStore, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var provider = new SignatureDenyingProvider();

        var handler = new HandleWebhookHandler(
            paymentStore,
            provider,
            invoiceProvider: null,
            creditLedger: null,
            new NoopCreditsFeatureFlag(),
            chargeHandler,
            NullLogger<HandleWebhookHandler>.Instance);

        var payload = """{"event":"recurring_charge.successful","data":{"payment_source_id":"ps_target3"}}""";

        var result = await handler.HandleAsync(new HandleWebhookCommand
        {
            Payload = payload,
            SignatureHeader = "wrong",
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/INVALID_SIGNATURE");
    }

    [Fact]
    public async Task RecurringEvent_without_recurring_handler_returns_failure()
    {
        var paymentStore = new TestPaymentStore();
        var provider = new NoopPaymentProvider();

        var handler = new HandleWebhookHandler(
            paymentStore,
            provider,
            invoiceProvider: null,
            creditLedger: null,
            new NoopCreditsFeatureFlag(),
            recurringHandler: null,
            NullLogger<HandleWebhookHandler>.Instance);

        var payload = """{"event":"recurring_charge.successful","data":{"payment_source_id":"ps_target4"}}""";

        var result = await handler.HandleAsync(new HandleWebhookCommand
        {
            Payload = payload,
            SignatureHeader = "valid-sig",
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/UNSUPPORTED_EVENT");
    }

    [Fact]
    public async Task RecurringEvent_without_payment_source_id_returns_invalid_payload()
    {
        var paymentStore = new TestPaymentStore();
        var subscriptionStore = new TestSubscriptionStore();
        var chargeHandler = new HandleRecurringChargeHandler(
            subscriptionStore,
            new AccreditPurchaseHandler(new TestCreditLedger()),
            NullLogger<HandleRecurringChargeHandler>.Instance);
        var provider = new NoopPaymentProvider();

        var handler = new HandleWebhookHandler(
            paymentStore,
            provider,
            invoiceProvider: null,
            creditLedger: null,
            new NoopCreditsFeatureFlag(),
            chargeHandler,
            NullLogger<HandleWebhookHandler>.Instance);

        var payload = """{"event":"recurring_charge.successful","data":{"charge_id":"ch_abc"}}""";

        var result = await handler.HandleAsync(new HandleWebhookCommand
        {
            Payload = payload,
            SignatureHeader = "valid-sig",
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/INVALID_PAYLOAD");
    }

    private static Payment NewPayment(string wompiTransactionId) => new()
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
        UpdatedAt = DateTime.UtcNow,
    };

    private sealed class NoopPaymentProvider : IPaymentProvider
    {
        public Task<CheckoutSession> CreateCheckoutAsync(string userId, CreditPackage package, string idempotencyKey, CancellationToken ct = default)
        {
            _ = userId;
            _ = package;
            _ = idempotencyKey;
            return Task.FromResult(new CheckoutSession { SessionId = "noop", PublicKey = "k", AmountInCents = 0, Currency = "COP", Reference = "noop" });
        }

        public Task<TransactionStatus?> GetTransactionStatusAsync(string wompiTransactionId, CancellationToken ct = default)
        {
            _ = wompiTransactionId;
            return Task.FromResult<TransactionStatus?>(null);
        }

        public bool VerifyWebhookSignature(string payload, string signatureHeader)
        {
            _ = payload;
            return signatureHeader == "valid-sig";
        }
    }

    private sealed class SignatureDenyingProvider : IPaymentProvider
    {
        public Task<CheckoutSession> CreateCheckoutAsync(string userId, CreditPackage package, string idempotencyKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TransactionStatus?> GetTransactionStatusAsync(string wompiTransactionId, CancellationToken ct = default) => throw new NotSupportedException();
        public bool VerifyWebhookSignature(string payload, string signatureHeader) => false;
    }

    private sealed class NoopCreditsFeatureFlag : ICreditsFeatureFlag
    {
        public bool IsEnabled => false;
    }
}
