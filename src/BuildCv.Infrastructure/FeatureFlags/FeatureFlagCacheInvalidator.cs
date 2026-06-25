using BuildCv.Application.Common;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class FeatureFlagCacheInvalidator(CachingFeatureFlagDecorator decorator) : IFeatureFlagCache
{
    public void Invalidate(string name) => decorator.Invalidate(name);
}
