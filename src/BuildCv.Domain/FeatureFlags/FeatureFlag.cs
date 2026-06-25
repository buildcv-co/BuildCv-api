namespace BuildCv.Domain.FeatureFlags;

public sealed record FeatureFlag
{
    public string Name { get; init; } = "";
    public bool DefaultValue { get; init; }
    public bool CurrentValue { get; init; }
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; init; }

    public static FeatureFlag Create(string name, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name required", nameof(name));
        }

        return new FeatureFlag
        {
            Name = name,
            DefaultValue = defaultValue,
            CurrentValue = defaultValue,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
