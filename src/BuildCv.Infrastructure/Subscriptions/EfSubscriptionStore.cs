using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Subscriptions;

public sealed class EfSubscriptionStore(BuildCvDbContext db) : ISubscriptionStore
{
    public async Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct = default)
    {
        var query = db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId);
        if (!includeCanceled)
        {
            query = query.Where(s => s.Status != SubscriptionStatus.Canceled);
        }

        return await query
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct = default)
        => await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PaymentSourceId == paymentSourceId, ct);

    public async Task UpsertAsync(Subscription subscription, CancellationToken ct = default)
    {
        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscription.Id, ct);
        if (existing is null)
        {
            await db.Subscriptions.AddAsync(subscription, ct);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(subscription);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Subscription>> GetDueForRetryAsync(DateTime now, int limit, CancellationToken ct = default)
        => await db.Subscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.NextChargeAt <= now)
            .OrderBy(s => s.NextChargeAt)
            .Take(limit)
            .ToListAsync(ct);
}
