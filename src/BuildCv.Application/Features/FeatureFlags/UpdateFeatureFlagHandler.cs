using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Features.FeatureFlags;

public sealed class UpdateFeatureFlagHandler(IFeatureFlagAdminService adminService)
{
    public Task<FeatureFlag> HandleAsync(string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct = default)
        => adminService.UpdateAsync(name, newValue, changedBy, reason, ct);
}
