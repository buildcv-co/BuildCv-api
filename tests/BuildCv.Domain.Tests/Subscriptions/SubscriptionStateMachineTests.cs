using BuildCv.Domain.Subscriptions;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Subscriptions;

public sealed class SubscriptionStateMachineTests
{
    [Fact]
    public void TransitionToActive_advances_period_and_resets_retry_count()
    {
        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Standard, "ps_test_x", start);
        var pastDue = sub with
        {
            Status = SubscriptionStatus.PastDue,
            RetryCount = 2,
            NextChargeAt = start.AddDays(4)
        };

        var chargedAt = start.AddDays(30);
        var advanced = SubscriptionStateMachine.TransitionToActive(pastDue, chargedAt, chargedAt);

        advanced.Status.Should().Be(SubscriptionStatus.Active);
        advanced.CurrentPeriodStart.Should().Be(pastDue.CurrentPeriodEnd);
        advanced.CurrentPeriodEnd.Should().Be(pastDue.CurrentPeriodEnd.AddDays(30));
        advanced.LastChargeAt.Should().Be(chargedAt);
        advanced.NextChargeAt.Should().Be(pastDue.CurrentPeriodEnd.AddDays(27));
        advanced.RetryCount.Should().Be(0);
    }

    [Fact]
    public void TransitionToPastDue_increments_retry_count_and_schedules_next_attempt()
    {
        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_test_x", start);

        var pastDue = SubscriptionStateMachine.TransitionToPastDue(sub, start.AddDays(30), attemptNumber: 1);

        pastDue.Status.Should().Be(SubscriptionStatus.PastDue);
        pastDue.RetryCount.Should().Be(1);
        pastDue.NextChargeAt.Should().Be(start.AddDays(30).AddDays(1));
    }

    [Fact]
    public void TransitionToPastDue_uses_three_day_delay_for_second_retry()
    {
        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_test_x", start);
        var firstPastDue = SubscriptionStateMachine.TransitionToPastDue(sub, start.AddDays(30), attemptNumber: 1);

        var secondPastDue = SubscriptionStateMachine.TransitionToPastDue(firstPastDue, start.AddDays(31), attemptNumber: 2);

        secondPastDue.Status.Should().Be(SubscriptionStatus.PastDue);
        secondPastDue.RetryCount.Should().Be(2);
        secondPastDue.NextChargeAt.Should().Be(start.AddDays(31).AddDays(3));
    }

    [Fact]
    public void TransitionToPastDue_auto_cancels_after_max_retries_exceeded()
    {
        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_test_x", start);
        var twoRetries = sub with { Status = SubscriptionStatus.PastDue, RetryCount = 2 };

        var canceled = SubscriptionStateMachine.TransitionToPastDue(twoRetries, start.AddDays(35), attemptNumber: 3);

        canceled.Status.Should().Be(SubscriptionStatus.Canceled);
        canceled.CanceledAt.Should().Be(start.AddDays(35));
        canceled.NextChargeAt.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void UserCancel_transitions_to_canceled_and_freezes_next_charge()
    {
        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Standard, "ps_test_x", start);

        var canceled = SubscriptionStateMachine.TransitionToCanceled(sub, start.AddDays(15), "user canceled");

        canceled.Status.Should().Be(SubscriptionStatus.Canceled);
        canceled.CanceledAt.Should().Be(start.AddDays(15));
        canceled.NextChargeAt.Should().Be(DateTime.MaxValue);
        canceled.CurrentPeriodEnd.Should().Be(sub.CurrentPeriodEnd);
    }

    [Fact]
    public void TransitionToActive_rejects_canceled_subscription()
    {
        var start = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var sub = Subscription.Create(Guid.NewGuid(), SubscriptionPlan.Starter, "ps_test_x", start);
        var canceled = SubscriptionStateMachine.TransitionToCanceled(sub, start, "user canceled");

        var act = () => SubscriptionStateMachine.TransitionToActive(canceled, start.AddDays(30), start.AddDays(30));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*INVALID_TRANSITION*");
    }

    [Fact]
    public void MaxRetries_is_three_and_retry_delays_are_one_three_seven_days()
    {
        SubscriptionStateMachine.MaxRetries.Should().Be(3);
        SubscriptionStateMachine.RetryDelays.Should().HaveCount(3);
        SubscriptionStateMachine.RetryDelays[0].Should().Be(TimeSpan.FromDays(1));
        SubscriptionStateMachine.RetryDelays[1].Should().Be(TimeSpan.FromDays(3));
        SubscriptionStateMachine.RetryDelays[2].Should().Be(TimeSpan.FromDays(7));
        SubscriptionStateMachine.GracePeriod.Should().Be(TimeSpan.FromDays(14));
    }
}
