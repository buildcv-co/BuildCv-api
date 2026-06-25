using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Subscriptions;

public sealed class CancelSubscriptionHandlerTests
{
    [Fact]
    public async Task HandleAsync_cancels_provider_charge_transitions_status_and_preserves_period_end()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var handler = new CancelSubscriptionHandler(store, provider, NullLogger<CancelSubscriptionHandler>.Instance);
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(userId, SubscriptionPlan.Standard, "ps_target", start);
        await store.UpsertAsync(sub);

        var canceled = await handler.HandleAsync(userId);

        canceled.Status.Should().Be(SubscriptionStatus.Canceled);
        canceled.CanceledAt.Should().NotBeNull();
        canceled.CurrentPeriodEnd.Should().Be(sub.CurrentPeriodEnd);
        canceled.NextChargeAt.Should().Be(DateTime.MaxValue);

        provider.CancelledPaymentSources.Should().ContainSingle().Which.Should().Be("ps_target");
    }

    [Fact]
    public async Task HandleAsync_throws_when_no_active_subscription_exists_for_user()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var handler = new CancelSubscriptionHandler(store, provider, NullLogger<CancelSubscriptionHandler>.Instance);

        var act = () => handler.HandleAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active subscription*");
        provider.CancelledPaymentSources.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_persists_canceled_subscription_via_store()
    {
        var store = new TestSubscriptionStore();
        var provider = new TestSubscriptionProvider();
        var handler = new CancelSubscriptionHandler(store, provider, NullLogger<CancelSubscriptionHandler>.Instance);
        var userId = Guid.NewGuid();
        await store.UpsertAsync(Subscription.Create(userId, SubscriptionPlan.Starter, "ps_x", DateTime.UtcNow));

        await handler.HandleAsync(userId);

        var persisted = await store.GetByUserIdAsync(userId, includeCanceled: true);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(SubscriptionStatus.Canceled);
    }
}
