using System.Collections.Concurrent;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;

namespace BuildCv.Infrastructure.Payments;

public sealed class InMemoryPaymentStore : IPaymentStore
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();
    private readonly ConcurrentDictionary<string, Payment> _byIdempotencyKey = new();
    private readonly ConcurrentDictionary<string, Payment> _byWompiTransactionId = new();

    public Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        _payments[payment.Id] = payment;
        _byIdempotencyKey[payment.IdempotencyKey] = payment;
        if (payment.WompiTransactionId is not null)
        {
            _byWompiTransactionId[payment.WompiTransactionId] = payment;
        }

        return Task.CompletedTask;
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _payments.TryGetValue(id, out var payment);
        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
    {
        _byIdempotencyKey.TryGetValue(key, out var payment);
        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByWompiTransactionIdAsync(string wompiTransactionId, CancellationToken ct = default)
    {
        _byWompiTransactionId.TryGetValue(wompiTransactionId, out var payment);
        return Task.FromResult(payment);
    }

    public Task<IReadOnlyList<Payment>> ListByUserIdAsync(string userId, int page, int perPage, CancellationToken ct = default)
    {
        var payments = _payments.Values
            .Where(p => p.UserId.ToString() == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(payments);
    }

    public Task<IReadOnlyList<Payment>> ListStalePendingAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - threshold;
        var payments = _payments.Values
            .Where(p => p.Status == PaymentStatus.Pending
                && p.WompiTransactionId is not null
                && p.CreatedAt <= cutoff)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(payments);
    }

    public Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _payments[payment.Id] = payment;
        _byIdempotencyKey[payment.IdempotencyKey] = payment;
        if (payment.WompiTransactionId is not null)
        {
            _byWompiTransactionId[payment.WompiTransactionId] = payment;
        }

        return Task.CompletedTask;
    }
}
