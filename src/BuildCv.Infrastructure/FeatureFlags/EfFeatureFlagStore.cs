using System.Text;
using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class EfFeatureFlagStore(
    BuildCvDbContext db,
    ILogger<EfFeatureFlagStore> logger) : IFeatureFlagStore
{
    public async Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
    {
        try
        {
            return await db.FeatureFlags.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Name == name, ct);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            logger.LogWarning(ex, "Transient failure reading flag {FlagName}, retrying once", name);
            db.ChangeTracker.Clear();
            return await db.FeatureFlags.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Name == name, ct);
        }
    }

    public async Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
    {
        var list = await db.FeatureFlags.AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
        return list;
    }

    public async Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        var existing = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Name == flag.Name, ct);
        if (existing is null)
        {
            await db.FeatureFlags.AddAsync(flag, ct);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(flag);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default)
    {
        await db.FeatureFlagAuditLogs.AddAsync(log, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = db.FeatureFlagAuditLogs
            .AsNoTracking()
            .Where(l => l.FlagName == flagName);

        if (TryDecodeCursor(cursor, out var cursorAt, out var cursorId))
        {
            query = query.Where(l =>
                l.ChangedAt.Ticks < cursorAt.Ticks
                || (l.ChangedAt.Ticks == cursorAt.Ticks && l.Id.CompareTo(cursorId) < 0));
        }

        IReadOnlyList<FeatureFlagAuditLog> page = await query
            .OrderByDescending(l => l.ChangedAt)
            .ThenByDescending(l => l.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

        return page;
    }

    private static bool TryDecodeCursor(string? cursor, out DateTime at, out Guid id)
    {
        at = default;
        id = default;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split(':', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            at = new DateTime(long.Parse(parts[0]), DateTimeKind.Utc);
            id = Guid.Parse(parts[1]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is Npgsql.PostgresException pg && pg.IsTransient;
}
