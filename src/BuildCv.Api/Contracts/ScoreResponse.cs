namespace BuildCv.Api.Contracts;

/// <summary>
/// Contrato HTTP del análisis (formato congelado, consumido por el frontend y el cliente TS).
/// Nombres honestos: "coincidencia + legibilidad", nunca "ATS oficial" (FR-009).
/// </summary>
public sealed record ScoreResponse(
    int OverallScore,
    string Band,
    string HonestyNotice,
    string EngineVersion,
    string LexiconVersion,
    string ContextId,
    IReadOnlyList<ComponentResponse> Components,
    KeywordAnalysisResponse KeywordAnalysis,
    IReadOnlyList<RecommendationResponse> Recommendations,
    IReadOnlyList<FormatIssueResponse> FormatIssues,
    IReadOnlyList<GateResponse> GatesApplied);

public sealed record ComponentResponse(
    string ComponentId,
    string Label,
    int SubScore,
    double Weight,
    double MeasurementCoverage,
    string Confidence,
    string Explanation);

public sealed record KeywordResponse(
    string CanonicalTerm,
    string Category,
    string SourceSection,
    double Weight,
    string MatchLevel,
    string Location,
    double CreditAwarded,
    string Note);

public sealed record KeywordAnalysisResponse(
    IReadOnlyList<KeywordResponse> Present,
    IReadOnlyList<KeywordResponse> Missing,
    IReadOnlyList<KeywordResponse> Partial);

public sealed record RecommendationResponse(
    string Action,
    string Type,
    string TargetComponent,
    int EstimatedImpact,
    bool RequiresInvention,
    string HonestyNote);

public sealed record FormatIssueResponse(string Code, string Severity, string Message);

public sealed record GateResponse(string ComponentId, double Cap, string Reason, string Message);
