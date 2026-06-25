namespace BuildCv.Domain.Subscriptions;

public sealed record Subscription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public SubscriptionPlan Plan { get; init; }
    public string PaymentSourceId { get; init; } = "";
    public SubscriptionStatus Status { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime CurrentPeriodStart { get; init; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; init; } = DateTime.UtcNow.AddDays(30);
    public DateTime? CanceledAt { get; init; }
    public DateTime? LastChargeAt { get; init; }
    public DateTime NextChargeAt { get; init; } = DateTime.UtcNow.AddDays(27);
    public int RetryCount { get; init; }

    public static Subscription Create(Guid userId, SubscriptionPlan plan, string paymentSourceId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(paymentSourceId))
        {
            throw new ArgumentException("Payment source required", nameof(paymentSourceId));
        }

        return new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = plan,
            PaymentSourceId = paymentSourceId,
            Status = SubscriptionStatus.Active,
            StartedAt = now,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(30),
            NextChargeAt = now.AddDays(27),
            RetryCount = 0
        };
    }

    public int CreditsPerMonth => Plan switch
    {
        SubscriptionPlan.Starter => 30,
        SubscriptionPlan.Standard => 100,
        _ => 0
    };

    public decimal AmountCop => Plan switch
    {
        SubscriptionPlan.Starter => 30_000m,
        SubscriptionPlan.Standard => 80_000m,
        _ => 0m
    };
}
