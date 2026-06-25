using System.Collections.Concurrent;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Infrastructure.Subscriptions;

public sealed class InMemorySubscriptionStore : ISubscriptionStore
{
    private readonly ConcurrentDictionary<Guid, Subscription> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _paymentSourceIndex = new(StringComparer.Ordinal);

    public IReadOnlyList<Subscription> All => _byId.Values.ToList();

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _byId.TryGetValue(id, out var sub);
        return Task.FromResult(sub);
    }

    public Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct = default)
    {
        IReadOnlyList<Subscription> matches = _byId.Values
            .Where(s => s.UserId == userId)
            .Where(s => includeCanceled || s.Status != SubscriptionStatus.Canceled)
            .OrderByDescending(s => s.StartedAt)
            .ToList();
        return Task.FromResult(matches.Count == 0 ? null : matches[0]);
    }

    public Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct = default)
    {
        if (_paymentSourceIndex.TryGetValue(paymentSourceId, out var id))
        {
            _byId.TryGetValue(id, out var sub);
            return Task.FromResult(sub);
        }

        return Task.FromResult<Subscription?>(null);
    }

    public Task UpsertAsync(Subscription subscription, CancellationToken ct = default)
    {
        if (_byId.TryGetValue(subscription.Id, out var previous)
            && !string.Equals(previous.PaymentSourceId, subscription.PaymentSourceId, StringComparison.Ordinal))
        {
            _paymentSourceIndex.TryRemove(previous.PaymentSourceId, out _);
        }

        _byId[subscription.Id] = subscription;
        _paymentSourceIndex[subscription.PaymentSourceId] = subscription.Id;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Subscription>> GetDueForRetryAsync(DateTime now, int limit, CancellationToken ct = default)
    {
        IReadOnlyList<Subscription> due = _byId.Values
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.NextChargeAt <= now)
            .OrderBy(s => s.NextChargeAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(due);
    }
}
