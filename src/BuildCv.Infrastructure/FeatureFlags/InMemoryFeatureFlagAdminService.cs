using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class InMemoryFeatureFlagAdminService(IFeatureFlagStore store) : IFeatureFlagAdminService
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
            Reason = reason,
        };

        var updated = existing with
        {
            CurrentValue = newValue,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = changedBy,
        };

        await store.UpsertAsync(updated, ct);
        await store.AppendAuditLogAsync(auditLog, ct);

        return updated;
    }
}