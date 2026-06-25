using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Tests.Features.Subscriptions;

internal sealed class TestSubscriptionStore : ISubscriptionStore
{
    private readonly Dictionary<Guid, Subscription> _byId = new();
    private readonly Dictionary<Guid, List<Subscription>> _byUser = new();
    private readonly Dictionary<string, Subscription> _byPaymentSource = new(StringComparer.Ordinal);

    public IReadOnlyList<Subscription> All => _byId.Values.ToList();

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _byId.TryGetValue(id, out var sub);
        return Task.FromResult(sub);
    }

    public Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct = default)
    {
        if (!_byUser.TryGetValue(userId, out var list))
        {
            return Task.FromResult<Subscription?>(null);
        }

        var match = includeCanceled
            ? list.OrderByDescending(s => s.StartedAt).FirstOrDefault()
            : list.Where(s => s.Status != SubscriptionStatus.Canceled).OrderByDescending(s => s.StartedAt).FirstOrDefault();

        return Task.FromResult(match);
    }

    public Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct = default)
    {
        _byPaymentSource.TryGetValue(paymentSourceId, out var sub);
        return Task.FromResult(sub);
    }

    public Task UpsertAsync(Subscription subscription, CancellationToken ct = default)
    {
        _byId[subscription.Id] = subscription;
        _byPaymentSource[subscription.PaymentSourceId] = subscription;
        if (!_byUser.TryGetValue(subscription.UserId, out var list))
        {
            list = new List<Subscription>();
            _byUser[subscription.UserId] = list;
        }
        list.RemoveAll(s => s.Id == subscription.Id);
        list.Add(subscription);
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

internal sealed class TestSubscriptionProvider : ISubscriptionProvider
{
    private int _chargeCounter;

    public List<(string PaymentSourceId, decimal Amount, string Currency, DateTime ChargeDate)> ScheduledCharges { get; } = new();
    public List<string> CancelledPaymentSources { get; } = new();

    public Func<string, decimal, string, DateTime, string>? ScheduleChargeOverride { get; set; }
    public Func<string, bool>? CancelChargeOverride { get; set; }
    public bool VerifySignatureReturns { get; set; } = true;

    public Task<string> CreateScheduledChargeAsync(string paymentSourceId, decimal amountCop, string currency, DateTime chargeDate, CancellationToken ct = default)
    {
        ScheduledCharges.Add((paymentSourceId, amountCop, currency, chargeDate));
        if (ScheduleChargeOverride is not null)
        {
            return Task.FromResult(ScheduleChargeOverride(paymentSourceId, amountCop, currency, chargeDate));
        }
        var chargeId = $"ch_test_{Interlocked.Increment(ref _chargeCounter)}";
        return Task.FromResult(chargeId);
    }

    public Task<bool> CancelScheduledChargeAsync(string paymentSourceId, CancellationToken ct = default)
    {
        CancelledPaymentSources.Add(paymentSourceId);
        if (CancelChargeOverride is not null)
        {
            return Task.FromResult(CancelChargeOverride(paymentSourceId));
        }
        return Task.FromResult(true);
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        _ = payload;
        return VerifySignatureReturns && !string.IsNullOrEmpty(signature);
    }
}

internal sealed class TestSubscriptionFeatureFlag : ISubscriptionFeatureFlag
{
    public bool IsEnabled { get; set; }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
