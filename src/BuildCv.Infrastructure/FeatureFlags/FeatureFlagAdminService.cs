using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class FeatureFlagAdminService(
    BuildCvDbContext db,
    IFeatureFlagStore store,
    ILogger<FeatureFlagAdminService> logger) : IFeatureFlagAdminService
{
    public async Task<FeatureFlag> UpdateAsync(
        string name,
        bool newValue,
        Guid changedBy,
        string? reason,
        CancellationToken ct = default)
    {
        var existing = await store.GetAsync(name, ct)
            ?? throw new FeatureFlagNotFoundException(name);

        var auditLog = new FeatureFlagAuditLog
        {
            Id = Guid.NewGuid(),
            FlagName = name,
            OldValue = existing.CurrentValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            Reason = reason
        };

        var updated = existing with
        {
            CurrentValue = newValue,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = changedBy
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await store.UpsertAsync(updated, ct);
            await store.AppendAuditLogAsync(auditLog, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        logger.LogInformation(
            "Feature flag committed (flagName={FlagName}, oldValue={OldValue}, newValue={NewValue}, changedBy={ChangedBy}, auditLogId={AuditLogId})",
            name, existing.CurrentValue, newValue, changedBy, auditLog.Id);

        return updated;
    }
}
