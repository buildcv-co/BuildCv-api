using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Application.Tests.Credits;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Subscriptions;

public sealed class HandleRecurringChargeHandlerTests
{
    [Fact]
    public async Task HandleSuccessAsync_advances_period_and_grants_credits_with_period_reference()
    {
        var store = new TestSubscriptionStore();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new HandleRecurringChargeHandler(store, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(userId, SubscriptionPlan.Standard, "ps_target", start);
        await store.UpsertAsync(sub);
        var chargedAt = start.AddDays(30);

        await handler.HandleSuccessAsync("ps_target", chargedAt, "ch_abc");

        var refreshed = await store.GetByPaymentSourceIdAsync("ps_target");
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(SubscriptionStatus.Active);
        refreshed.CurrentPeriodStart.Should().Be(sub.CurrentPeriodEnd);
        refreshed.CurrentPeriodEnd.Should().Be(sub.CurrentPeriodEnd.AddDays(30));
        refreshed.LastChargeAt.Should().Be(chargedAt);
        refreshed.RetryCount.Should().Be(0);

        ledger.AllEntries.Should().ContainSingle();
        ledger.AllEntries.Single().Reference.Should().Be($"subscription:{sub.Id}:{chargedAt:O}");
        ledger.AllEntries.Single().Delta.Should().Be(100);
    }

    [Fact]
    public async Task HandleSuccessAsync_is_idempotent_when_reference_already_recorded()
    {
        var store = new TestSubscriptionStore();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new HandleRecurringChargeHandler(store, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_target", start);
        await store.UpsertAsync(sub);
        var chargedAt = start.AddDays(30);

        await handler.HandleSuccessAsync("ps_target", chargedAt, "ch_first");
        await handler.HandleSuccessAsync("ps_target", chargedAt, "ch_first_duplicate");

        ledger.AllEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleFailureAsync_transitions_to_past_due_and_increments_retry_count()
    {
        var store = new TestSubscriptionStore();
        var ledger = new TestCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var handler = new HandleRecurringChargeHandler(store, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_target", start);
        await store.UpsertAsync(sub);
        var beforeCall = DateTime.UtcNow;
        await handler.HandleFailureAsync("ps_target", beforeCall, "card_declined");
        var afterCall = DateTime.UtcNow;

        var refreshed = await store.GetByPaymentSourceIdAsync("ps_target");
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(SubscriptionStatus.PastDue);
        refreshed.RetryCount.Should().Be(1);

        var expectedMinNextCharge = beforeCall.AddDays(1);
        var expectedMaxNextCharge = afterCall.AddDays(1).AddSeconds(1);
        refreshed.NextChargeAt.Should().BeOnOrAfter(expectedMinNextCharge)
            .And.BeBefore(expectedMaxNextCharge);
    }
}
