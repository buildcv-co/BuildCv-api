using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Features.Subscriptions;

public sealed class GetSubscriptionHandler(ISubscriptionStore store)
{
    public Task<Subscription?> HandleAsync(Guid userId, CancellationToken ct = default)
        => store.GetByUserIdAsync(userId, includeCanceled: true, ct);
}
