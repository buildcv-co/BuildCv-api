namespace BuildCv.Domain.FeatureFlags;

public sealed record FeatureFlagAuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FlagName { get; init; } = "";
    public bool? OldValue { get; init; }
    public bool NewValue { get; init; }
    public Guid ChangedBy { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    public string? Reason { get; init; }
}