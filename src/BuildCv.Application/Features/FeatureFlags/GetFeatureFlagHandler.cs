using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Features.FeatureFlags;

public sealed class GetFeatureFlagHandler(IFeatureFlag flags)
{
    public Task<FeatureFlag?> HandleAsync(string name, CancellationToken ct = default)
        => flags.GetAsync(name, ct);
}