using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Common;

public interface IFeatureFlagAdminService
{
    Task<FeatureFlag> UpdateAsync(string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct = default);
}