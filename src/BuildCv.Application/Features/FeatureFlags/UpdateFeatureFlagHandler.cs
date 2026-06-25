using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Features.FeatureFlags;

public sealed class UpdateFeatureFlagHandler(
    IFeatureFlagAdminService adminService,
    IFeatureFlagCache cache)
{
    public async Task<FeatureFlag> HandleAsync(
        string name,
        bool newValue,
        Guid changedBy,
        string? reason,
        CancellationToken ct = default)
    {
        var updated = await adminService.UpdateAsync(name, newValue, changedBy, reason, ct);
        cache.Invalidate(name);
        return updated;
    }
}
