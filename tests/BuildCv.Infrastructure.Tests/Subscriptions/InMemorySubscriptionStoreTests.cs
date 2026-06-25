using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Subscriptions;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class InMemorySubscriptionStoreTests
{
    private readonly InMemorySubscriptionStore _store = new();

    [Fact]
    public async Task UpsertAsync_stores_new_subscription()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Starter, "ps_a");

        await _store.UpsertAsync(sub);

        var byId = await _store.GetByIdAsync(sub.Id);
        byId.Should().NotBeNull();
        byId!.UserId.Should().Be(userId);
        byId.PaymentSourceId.Should().Be("ps_a");
    }

    [Fact]
    public async Task UpsertAsync_replaces_existing_subscription_and_keeps_indexes_consistent()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Standard, "ps_b");
        await _store.UpsertAsync(sub);

        var updated = sub with { Status = SubscriptionStatus.PastDue, RetryCount = 2 };
        await _store.UpsertAsync(updated);

        var byId = await _store.GetByIdAsync(sub.Id);
        var bySource = await _store.GetByPaymentSourceIdAsync("ps_b");
        byId!.Status.Should().Be(SubscriptionStatus.PastDue);
        bySource!.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_excludes_canceled_by_default()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Starter, "ps_c");
        await _store.UpsertAsync(sub);
        await _store.UpsertAsync(sub with { Status = SubscriptionStatus.Canceled, CanceledAt = DateTime.UtcNow });

        var result = await _store.GetByUserIdAsync(userId, includeCanceled: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_includes_canceled_when_requested()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Standard, "ps_d");
        await _store.UpsertAsync(sub);
        await _store.UpsertAsync(sub with { Status = SubscriptionStatus.Canceled, CanceledAt = DateTime.UtcNow });

        var result = await _store.GetByUserIdAsync(userId, includeCanceled: true);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SubscriptionStatus.Canceled);
    }

    [Fact]
    public async Task GetDueForRetryAsync_filters_by_status_and_due_time()
    {
        var now = DateTime.UtcNow;
        var pastDue = NewSubscription(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_pd")
            with
        { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddMinutes(-5) };
        var active = NewSubscription(Guid.NewGuid(), SubscriptionPlan.Standard, "ps_active");
        var future = NewSubscription(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_future")
            with
        { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddHours(1) };
        await _store.UpsertAsync(pastDue);
        await _store.UpsertAsync(active);
        await _store.UpsertAsync(future);

        var result = await _store.GetDueForRetryAsync(now, limit: 10);

        result.Should().HaveCount(1);
        result[0].PaymentSourceId.Should().Be("ps_pd");
    }

    [Fact]
    public async Task GetDueForRetryAsync_orders_by_next_charge_at_and_respects_limit()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await _store.UpsertAsync(NewSubscription(Guid.NewGuid(), SubscriptionPlan.Starter, $"ps_{i}")
                with
            { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddMinutes(-i - 1) });
        }

        var result = await _store.GetDueForRetryAsync(now, limit: 3);

        result.Should().HaveCount(3);
        result.Select(s => s.NextChargeAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetByPaymentSourceIdAsync_returns_null_when_no_match()
    {
        var result = await _store.GetByPaymentSourceIdAsync("ps_missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task InMemorySubscriptionStore_implements_contract()
    {
        _store.Should().BeAssignableTo<ISubscriptionStore>();
    }

    private static Subscription NewSubscription(Guid userId, SubscriptionPlan plan, string paymentSourceId)
        => Subscription.Create(userId, plan, paymentSourceId, DateTime.UtcNow);
}
