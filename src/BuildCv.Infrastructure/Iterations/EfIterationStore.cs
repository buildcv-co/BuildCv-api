using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Iterations;

public sealed class EfIterationStore(BuildCvDbContext db) : IIterationStore, IIterationCleanupCapable
{
    public async Task SaveRequestAsync(IterationRequest request, CancellationToken ct = default)
    {
        await db.IterationRequests.AddAsync(request, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default)
    {
        var existing = await db.IterationRequests.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (existing is null)
        {
            return;
        }

        db.IterationRequests.Entry(existing).CurrentValues["Status"] = (int)status;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveResultAsync(IterationResult result, CancellationToken ct = default)
    {
        var withExpiry = result.ExpiresAt == default
            ? result with { ExpiresAt = DateTime.UtcNow.AddHours(24) }
            : result;

        var existing = await db.IterationResults.FirstOrDefaultAsync(r => r.RequestId == withExpiry.RequestId, ct);
        if (existing is null)
        {
            await db.IterationResults.AddAsync(withExpiry, ct);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(withExpiry);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<(IterationRequest?, IterationResult?)> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await db.IterationRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        var result = await db.IterationResults.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        return (request, result);
    }

    public async Task<int> DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var expired = await db.IterationResults
            .Where(r => r.ExpiresAt < olderThan)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        db.IterationResults.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        return expired.Count;
    }
}
