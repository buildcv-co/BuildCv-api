using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Application.Tests.Credits;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Subscriptions;

public sealed class SubscribeHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_subscription_persists_it_and_returns_active_status()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new SubscribeHandler(store, provider, accredit, NullLogger<SubscribeHandler>.Instance);
        var userId = Guid.NewGuid();

        var result = await handler.HandleAsync(userId, SubscriptionPlan.Starter, "ps_test_001");

        result.UserId.Should().Be(userId);
        result.Plan.Should().Be(SubscriptionPlan.Starter);
        result.PaymentSourceId.Should().Be("ps_test_001");
        result.Status.Should().Be(SubscriptionStatus.Active);
        result.CreditsPerMonth.Should().Be(30);
        result.AmountCop.Should().Be(30_000m);

        var stored = await store.GetByUserIdAsync(userId, includeCanceled: false);
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(result.Id);

        provider.ScheduledCharges.Should().ContainSingle()
            .Which.Should().Be(("ps_test_001", 30_000m, "COP", result.NextChargeAt));
    }

    [Fact]
    public async Task HandleAsync_grants_first_month_credits_with_subscription_reference()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new SubscribeHandler(store, provider, accredit, NullLogger<SubscribeHandler>.Instance);
        var userId = Guid.NewGuid();

        var sub = await handler.HandleAsync(userId, SubscriptionPlan.Standard, "ps_test_002");

        ledger.AllEntries.Should().ContainSingle();
        var entry = ledger.AllEntries.Single();
        entry.UserId.Should().Be(userId);
        entry.Reason.Should().Be(CreditLedgerReason.Purchase);
        entry.Reference.Should().Be($"subscription:{sub.Id}");
        entry.Delta.Should().Be(100);
        entry.BalanceAfter.Should().Be(100);
    }

    [Fact]
    public async Task HandleAsync_throws_when_user_already_has_active_subscription()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new SubscribeHandler(store, provider, accredit, NullLogger<SubscribeHandler>.Instance);
        var userId = Guid.NewGuid();
        await handler.HandleAsync(userId, SubscriptionPlan.Starter, "ps_first");

        var act = () => handler.HandleAsync(userId, SubscriptionPlan.Standard, "ps_second");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already has an active subscription*");
    }

    [Fact]
    public async Task HandleAsync_replays_grant_when_called_with_same_payment_source_after_user_was_already_subscribed()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new SubscribeHandler(store, provider, accredit, NullLogger<SubscribeHandler>.Instance);
        var userId = Guid.NewGuid();

        var sub = await handler.HandleAsync(userId, SubscriptionPlan.Starter, "ps_first");
        var ledgerCount = ledger.AllEntries.Count;

        var duplicateAttempt = () => handler.HandleAsync(userId, SubscriptionPlan.Standard, "ps_second");

        await duplicateAttempt.Should().ThrowAsync<InvalidOperationException>();
        ledger.AllEntries.Count.Should().Be(ledgerCount);
        provider.ScheduledCharges.Should().HaveCount(1);
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }
}
