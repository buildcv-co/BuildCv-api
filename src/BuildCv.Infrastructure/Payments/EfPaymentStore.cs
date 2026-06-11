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

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        var existing = _db.ChangeTracker.Entries<Payment>()
            .FirstOrDefault(e => e.Entity.Id == payment.Id);
        if (existing is not null)
        {
            existing.State = EntityState.Detached;
        }

        _db.Payments.Update(payment);
        await _db.SaveChangesAsync(ct);
    }
}
