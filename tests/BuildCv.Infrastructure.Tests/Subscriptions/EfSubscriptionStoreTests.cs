using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Persistence;
using BuildCv.Infrastructure.Subscriptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class EfSubscriptionStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfSubscriptionStore _store;

    public EfSubscriptionStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfSubscriptionStore(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task UpsertAsync_inserts_new_subscription()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Starter, "ps_abc");

        await _store.UpsertAsync(sub);

        var persisted = await _dbContext.Subscriptions.FindAsync(sub.Id);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(userId);
        persisted.Plan.Should().Be(SubscriptionPlan.Starter);
        persisted.PaymentSourceId.Should().Be("ps_abc");
        persisted.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task UpsertAsync_updates_existing_subscription()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Starter, "ps_abc");
        await _store.UpsertAsync(sub);

        var canceled = sub with { Status = SubscriptionStatus.Canceled, CanceledAt = DateTime.UtcNow };
        await _store.UpsertAsync(canceled);

        var result = await _store.GetByIdAsync(sub.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(SubscriptionStatus.Canceled);
        result.CanceledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_active_subscription_by_default()
    {
        var userId = Guid.NewGuid();
        await _store.UpsertAsync(NewSubscription(userId, SubscriptionPlan.Starter, "ps_a"));

        var result = await _store.GetByUserIdAsync(userId, includeCanceled: false);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task GetByUserIdAsync_excludes_canceled_when_includeCanceled_false()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Starter, "ps_a");
        await _store.UpsertAsync(sub);
        await _store.UpsertAsync(sub with { Status = SubscriptionStatus.Canceled, CanceledAt = DateTime.UtcNow });

        var result = await _store.GetByUserIdAsync(userId, includeCanceled: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_canceled_when_includeCanceled_true()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Standard, "ps_b");
        await _store.UpsertAsync(sub);
        await _store.UpsertAsync(sub with { Status = SubscriptionStatus.Canceled, CanceledAt = DateTime.UtcNow });

        var result = await _store.GetByUserIdAsync(userId, includeCanceled: true);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SubscriptionStatus.Canceled);
    }

    [Fact]
    public async Task GetByPaymentSourceIdAsync_returns_subscription_with_matching_source()
    {
        var userId = Guid.NewGuid();
        var sub = NewSubscription(userId, SubscriptionPlan.Standard, "ps_unique_token");
        await _store.UpsertAsync(sub);

        var result = await _store.GetByPaymentSourceIdAsync("ps_unique_token");

        result.Should().NotBeNull();
        result!.Id.Should().Be(sub.Id);
    }

    [Fact]
    public async Task GetByPaymentSourceIdAsync_returns_null_when_no_match()
    {
        var result = await _store.GetByPaymentSourceIdAsync("ps_unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDueForRetryAsync_returns_only_past_due_with_due_next_charge()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var due1 = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_due1", now.AddDays(-10))
            with
        { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddMinutes(-5) };
        var due2 = Subscription.Create(userId, SubscriptionPlan.Standard, "ps_due2", now.AddDays(-20))
            with
        { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddMinutes(-1) };
        var active = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_active", now);
        var future = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_future", now)
            with
        { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddHours(1) };
        var canceled = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_cancel", now)
            with
        { Status = SubscriptionStatus.Canceled, CanceledAt = now };

        await _store.UpsertAsync(due1);
        await _store.UpsertAsync(due2);
        await _store.UpsertAsync(active);
        await _store.UpsertAsync(future);
        await _store.UpsertAsync(canceled);

        var result = await _store.GetDueForRetryAsync(now, limit: 10);

        result.Should().HaveCount(2);
        result.Select(s => s.PaymentSourceId).Should().Contain(new[] { "ps_due1", "ps_due2" });
        result[0].NextChargeAt.Should().BeOnOrBefore(result[1].NextChargeAt);
    }

    [Fact]
    public async Task GetDueForRetryAsync_respects_limit()
    {
        var now = DateTime.UtcNow;
        var subs = Enumerable.Range(0, 5)
            .Select(i => Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, $"ps_{i}", now)
                with
            { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddMinutes(-i - 1) })
            .ToList();
        foreach (var s in subs)
        {
            await _store.UpsertAsync(s);
        }

        var result = await _store.GetDueForRetryAsync(now, limit: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_not_found()
    {
        var result = await _store.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task EfSubscriptionStore_implements_contract()
    {
        _store.Should().BeAssignableTo<BuildCv.Application.Features.Subscriptions.ISubscriptionStore>();
    }

    private static Subscription NewSubscription(Guid userId, SubscriptionPlan plan, string paymentSourceId)
        => Subscription.Create(userId, plan, paymentSourceId, DateTime.UtcNow);
}
