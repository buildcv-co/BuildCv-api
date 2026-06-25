using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionService
{
    Task<Subscription> SubscribeAsync(Guid userId, SubscriptionPlan plan, string paymentSourceId, CancellationToken ct = default);
    Task<Subscription?> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Subscription> CancelAsync(Guid userId, CancellationToken ct = default);
    Task HandleRecurringChargeSuccessAsync(string paymentSourceId, DateTime chargedAt, string chargeId, CancellationToken ct = default);
    Task HandleRecurringChargeFailureAsync(string paymentSourceId, DateTime attemptedAt, string reason, CancellationToken ct = default);
    Task ProcessRetriesAsync(CancellationToken ct = default);
}
