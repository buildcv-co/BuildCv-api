using BuildCv.Domain.Subscriptions;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Subscriptions;

public sealed class SubscriptionTests
{
    [Fact]
    public void Create_sets_all_required_fields_for_active_subscription()
    {
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var sub = Subscription.Create(userId, SubscriptionPlan.Starter, "ps_test_123", now);

        sub.UserId.Should().Be(userId);
        sub.Plan.Should().Be(SubscriptionPlan.Starter);
        sub.PaymentSourceId.Should().Be("ps_test_123");
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.StartedAt.Should().Be(now);
        sub.CurrentPeriodStart.Should().Be(now);
        sub.CurrentPeriodEnd.Should().Be(now.AddDays(30));
        sub.NextChargeAt.Should().Be(now.AddDays(27));
        sub.LastChargeAt.Should().BeNull();
        sub.CanceledAt.Should().BeNull();
        sub.RetryCount.Should().Be(0);
        sub.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_throws_when_payment_source_id_is_null_or_whitespace()
    {
        var now = DateTime.UtcNow;

        var actNull = () => Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, null!, now);
        var actEmpty = () => Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "", now);
        var actWhitespace = () => Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "   ", now);

        actNull.Should().Throw<ArgumentException>().WithMessage("*Payment source*");
        actEmpty.Should().Throw<ArgumentException>().WithMessage("*Payment source*");
        actWhitespace.Should().Throw<ArgumentException>().WithMessage("*Payment source*");
    }

    [Fact]
    public void Starter_plan_grants_thirty_credits_per_month()
    {
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_test_x", DateTime.UtcNow);

        sub.CreditsPerMonth.Should().Be(30);
    }

    [Fact]
    public void Standard_plan_grants_one_hundred_credits_per_month()
    {
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Standard, "ps_test_x", DateTime.UtcNow);

        sub.CreditsPerMonth.Should().Be(100);
    }
}
