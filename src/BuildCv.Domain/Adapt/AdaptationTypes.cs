namespace BuildCv.Domain.Adapt;

public enum InventionType
{
    Skill,
    Certification,
    Company,
    Date,
    Metric,
    Title,
    Other
}

public enum InventionSeverity
{
    Soft,
    Hard
}

public enum Severity
{
    None,
    Warning,
    Critical
}

public sealed record EntityInvention(
    InventionType Type,
    string Claimed,
    string? Original,
    InventionSeverity InventionSeverity,
    int Position);

public sealed record ValidationReport(
    bool IsValid,
    Severity Severity,
    IReadOnlyList<EntityInvention> Inventions,
    IReadOnlyList<string> Warnings);

public sealed record AdaptationResult(
    string AdaptedCv,
    ValidationReport Validation,
    string EngineVersion,
    string AiModel);
