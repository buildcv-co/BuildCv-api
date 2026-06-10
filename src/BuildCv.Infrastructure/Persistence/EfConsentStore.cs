using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Persistence;

public sealed class EfConsentStore(BuildCvDbContext dbContext) : IConsentStore
{
    public async Task AddAsync(ConsentRecord record, CancellationToken ct = default)
    {
        dbContext.ConsentRecords.Add(record);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ConsentRecord?> GetActiveAsync(Guid userId, string purpose, CancellationToken ct = default)
    {
        return await dbContext.ConsentRecords
            .Where(c => c.UserId == userId && c.Purpose == purpose)
            .OrderByDescending(c => c.ConsentDate)
            .FirstOrDefaultAsync(c => c.RevokedAt == null, ct);
    }

    public async Task<ConsentRecord?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default)
    {
        return await dbContext.ConsentRecords
            .Where(c => c.UserId == userId && c.Purpose == purpose)
            .OrderByDescending(c => c.ConsentDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ConsentRecord>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.ConsentRecords
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.ConsentDate)
            .ToListAsync(ct);
    }

    public async Task RevokeAllAsync(Guid userId, DateTime revokedAt, CancellationToken ct = default)
    {
        var activeRecords = await dbContext.ConsentRecords
            .Where(c => c.UserId == userId && c.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var record in activeRecords)
        {
            dbContext.Entry(record).Property(c => c.RevokedAt).CurrentValue = revokedAt;
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
