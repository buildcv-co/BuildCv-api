using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Common;

public interface IFeatureFlagStore
{
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
    Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default);
    Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(string flagName, int limit, string? cursor, CancellationToken ct = default);
}
