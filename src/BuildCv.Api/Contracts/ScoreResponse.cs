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
    IReadOnlyList<GateResponse> GatesApplied,
    PerSectionResponse? PerSection = null,
    IReadOnlyList<RedFlagResponse>? RedFlags = null);

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

/// <summary>
/// Sub-puntaje por sección (engineVersion 2.0.0). Cada valor es 0–100 o
/// <c>null</c> cuando la sección está ausente (renormalización, FR-011).
/// </summary>
public sealed record PerSectionResponse(
    int? Experience,
    int? Education,
    int? Skills,
    int? Certifications,
    int? Contact);

/// <summary>
/// Señal informativa del motor 2.0.0 (Art. I — sin deducción).
/// </summary>
public sealed record RedFlagResponse(
    string Code,
    string Severity,
    string Message,
    int? Months = null,
    int? EmployersIn5y = null);
