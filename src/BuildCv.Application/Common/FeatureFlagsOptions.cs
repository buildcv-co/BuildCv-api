namespace BuildCv.Application.Common;

public sealed class FeatureFlagsOptions
{
    public int CacheTtlSeconds { get; init; } = 60;
    public Dictionary<string, bool> Defaults { get; init; } = new();
}