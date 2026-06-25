using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionStore
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct = default);
    Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct = default);
    Task UpsertAsync(Subscription subscription, CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> GetDueForRetryAsync(DateTime now, int limit, CancellationToken ct = default);
}
