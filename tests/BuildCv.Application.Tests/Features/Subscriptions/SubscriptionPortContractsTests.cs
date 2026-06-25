using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Subscriptions;

public sealed class SubscriptionPortContractsTests
{
    [Fact]
    public async Task ISubscriptionStore_GetByUserIdAsync_returns_latest_active_subscription()
    {
        var store = new TestSubscriptionStore();
        var userId = Guid.NewGuid();
        var first = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var second = Subscription.Create(userId, SubscriptionPlan.Standard, "ps_new", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        await store.UpsertAsync(first);
        await store.UpsertAsync(second);

        var result = await store.GetByUserIdAsync(userId, includeCanceled: false);

        result.Should().NotBeNull();
        result!.PaymentSourceId.Should().Be("ps_new");
    }

    [Fact]
    public async Task ISubscriptionStore_GetByUserIdAsync_excludes_canceled_when_flag_false()
    {
        var store = new TestSubscriptionStore();
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var active = Subscription.Create(userId, SubscriptionPlan.Standard, "ps_active", start);
        var canceled = SubscriptionStateMachine.TransitionToCanceled(active, start.AddDays(5), "user");
        await store.UpsertAsync(canceled);

        var excluded = await store.GetByUserIdAsync(userId, includeCanceled: false);
        var included = await store.GetByUserIdAsync(userId, includeCanceled: true);

        excluded.Should().BeNull();
        included.Should().NotBeNull();
    }

    [Fact]
    public async Task ISubscriptionStore_GetByPaymentSourceIdAsync_returns_matching_subscription()
    {
        var store = new TestSubscriptionStore();
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Standard, "ps_target", DateTime.UtcNow);
        await store.UpsertAsync(sub);

        var result = await store.GetByPaymentSourceIdAsync("ps_target");

        result.Should().NotBeNull();
        result!.PaymentSourceId.Should().Be("ps_target");
    }

    [Fact]
    public async Task ISubscriptionStore_GetDueForRetryAsync_returns_only_past_due_subscriptions_whose_next_charge_is_due()
    {
        var store = new TestSubscriptionStore();
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var due = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_due", now);
        var pastDue = SubscriptionStateMachine.TransitionToPastDue(due, now, attemptNumber: 1);
        await store.UpsertAsync(pastDue);

        var stillActive = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Standard, "ps_active", now);
        await store.UpsertAsync(stillActive);

        var futureDue = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_future", now);
        var futurePastDue = SubscriptionStateMachine.TransitionToPastDue(futureDue, now, attemptNumber: 1) with { NextChargeAt = now.AddDays(10) };
        await store.UpsertAsync(futurePastDue);

        var dueSubs = await store.GetDueForRetryAsync(now.AddDays(2), limit: 50);

        dueSubs.Should().HaveCount(1);
        dueSubs[0].PaymentSourceId.Should().Be("ps_due");
    }

    [Fact]
    public async Task ISubscriptionProvider_CreateScheduledChargeAsync_returns_distinct_charge_ids()
    {
        var provider = new TestSubscriptionProvider();

        var first = await provider.CreateScheduledChargeAsync("ps_x", 30_000m, "COP", DateTime.UtcNow.AddDays(30));
        var second = await provider.CreateScheduledChargeAsync("ps_y", 80_000m, "COP", DateTime.UtcNow.AddDays(30));

        first.Should().NotBe(second);
        provider.ScheduledCharges.Should().HaveCount(2);
    }

    [Fact]
    public async Task ISubscriptionProvider_CancelScheduledChargeAsync_returns_true_on_success()
    {
        var provider = new TestSubscriptionProvider();

        var ok = await provider.CancelScheduledChargeAsync("ps_target");

        ok.Should().BeTrue();
        provider.CancelledPaymentSources.Should().Contain("ps_target");
    }

    [Fact]
    public void ISubscriptionProvider_VerifyWebhookSignature_returns_true_for_valid_signature()
    {
        var provider = new TestSubscriptionProvider();

        var valid = provider.VerifyWebhookSignature("payload", "signature-abc");
        var invalid = provider.VerifyWebhookSignature("payload", "");

        valid.Should().BeTrue();
        invalid.Should().BeFalse();
    }

    [Fact]
    public void ISubscriptionFeatureFlag_toggles_between_enabled_and_disabled()
    {
        var flag = new TestSubscriptionFeatureFlag();

        flag.IsEnabled.Should().BeFalse();
        flag.Enable();
        flag.IsEnabled.Should().BeTrue();
        flag.Disable();
        flag.IsEnabled.Should().BeFalse();
    }
}
