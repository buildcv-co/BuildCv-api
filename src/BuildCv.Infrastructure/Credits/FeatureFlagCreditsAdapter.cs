using BuildCv.Application.Common;

namespace BuildCv.Infrastructure.Credits;

public sealed class FeatureFlagCreditsAdapter(IFeatureFlag flags) : ICreditsFeatureFlag
{
    public bool IsEnabled
        => flags.IsEnabledAsync("credits-enabled").GetAwaiter().GetResult();
}
