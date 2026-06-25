using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Features.FeatureFlags;

public sealed class ListFeatureFlagsHandler(IFeatureFlag flags)
{
    public Task<IReadOnlyList<FeatureFlag>> HandleAsync(CancellationToken ct = default)
        => flags.ListAsync(ct);
}