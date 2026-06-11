using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Payments;

public sealed class EfPaymentStore : IPaymentStore
{
    private readonly BuildCvDbContext _db;

    public EfPaymentStore(BuildCvDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
    {
        return await _db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct);
    }

    public async Task<Payment?> GetByWompiTransactionIdAsync(string wompiTransactionId, CancellationToken ct = default)
    {
        return await _db.Payments.FirstOrDefaultAsync(p => p.WompiTransactionId == wompiTransactionId, ct);
    }

    public async Task<IReadOnlyList<Payment>> ListByUserIdAsync(string userId, int page, int perPage, CancellationToken ct = default)
    {
        return await _db.Payments
            .Where(p => p.UserId.ToString() == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Payment>> ListStalePendingAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - threshold;
        return await _db.Payments
            .Where(p => p.Status == PaymentStatus.Pending
                && p.WompiTransactionId != null
                && p.CreatedAt <= cutoff)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        var entry = _db.ChangeTracker.Entries<Payment>()
            .FirstOrDefault(e => e.Entity.Id == payment.Id);

        if (entry is null)
        {
            entry = await _db.Payments.FirstOrDefaultAsync(p => p.Id == payment.Id, ct) is { } tracked
                ? _db.ChangeTracker.Entries<Payment>().First(e => e.Entity.Id == payment.Id)
                : throw new InvalidOperationException($"Payment {payment.Id} not found");
        }

        entry.CurrentValues.SetValues(payment);
        await _db.SaveChangesAsync(ct);
    }
}
