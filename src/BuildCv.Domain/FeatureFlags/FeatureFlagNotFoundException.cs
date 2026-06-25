namespace BuildCv.Domain.FeatureFlags;

public sealed class FeatureFlagNotFoundException : Exception
{
    public string FlagName { get; }
    public FeatureFlagNotFoundException(string flagName)
        : base($"Feature flag '{flagName}' not found in DB or appsettings")
    {
        FlagName = flagName;
    }
}