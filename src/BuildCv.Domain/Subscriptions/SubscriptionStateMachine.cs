namespace BuildCv.Domain.Subscriptions;

public static class SubscriptionStateMachine
{
    public const int MaxRetries = 3;
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);
    public static readonly TimeSpan[] RetryDelays = new[]
    {
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(7),
    };

    public static Subscription TransitionToActive(Subscription sub, DateTime chargedAt, DateTime now)
    {
        if (sub.Status == SubscriptionStatus.Canceled)
        {
            throw new InvalidOperationException("SUBSCRIPTION/INVALID_TRANSITION: cannot reactivate canceled subscription");
        }

        _ = now;

        return sub with
        {
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = sub.CurrentPeriodEnd,
            CurrentPeriodEnd = sub.CurrentPeriodEnd.AddDays(30),
            LastChargeAt = chargedAt,
            NextChargeAt = sub.CurrentPeriodEnd.AddDays(27),
            RetryCount = 0
        };
    }

    public static Subscription TransitionToPastDue(Subscription sub, DateTime now, int attemptNumber)
    {
        if (sub.Status == SubscriptionStatus.Canceled)
        {
            throw new InvalidOperationException("SUBSCRIPTION/INVALID_TRANSITION: cannot move canceled subscription to past_due");
        }

        _ = attemptNumber;

        var newRetryCount = sub.RetryCount + 1;
        if (newRetryCount >= MaxRetries)
        {
            return TransitionToCanceled(sub, now, "Max retries exceeded");
        }

        var delay = RetryDelays[Math.Min(sub.RetryCount, RetryDelays.Length - 1)];
        return sub with
        {
            Status = SubscriptionStatus.PastDue,
            NextChargeAt = now.Add(delay),
            RetryCount = newRetryCount
        };
    }

    public static Subscription TransitionToCanceled(Subscription sub, DateTime now, string reason)
    {
        _ = reason;
        return sub with
        {
            Status = SubscriptionStatus.Canceled,
            CanceledAt = now,
            NextChargeAt = DateTime.MaxValue
        };
    }
}
