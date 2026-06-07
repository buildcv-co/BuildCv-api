namespace BuildCv.Domain.Scoring;

/// <summary>Componentes del puntaje (C1–C5, FR-007).</summary>
public enum ComponentId
{
    Match,
    Structure,
    Achievements,
    Format,
    Length,
}

/// <summary>Banda cualitativa; el número es el valor rector (FR-010).</summary>
public enum ScoreBand
{
    Bajo,
    Medio,
    Bueno,
    Fuerte,
}

/// <summary>Subpuntaje de un componente con su peso, medibilidad y confianza.</summary>
public sealed record ComponentScore(
    ComponentId Id,
    double SubScore,
    double Weight,
    double Measurability,
    double Confidence,
    string Summary);

/// <summary>Observación de formato (severidad: "warn" | "info").</summary>
public sealed record FormatIssue(string Code, string Severity, string Message);

/// <summary>Compuerta/cap aplicado a un componente y su razón (FR-012).</summary>
public sealed record GateApplied(ComponentId Component, double Cap, string Reason, string Message);

/// <summary>
/// Resultado del análisis determinista. Sellado con <see cref="EngineVersion"/> +
/// <see cref="LexiconVersion"/> y <see cref="ContextHash"/> para reproducibilidad (FR-006/013/031).
/// </summary>
public sealed record ScoreResult(
    int Overall,
    ScoreBand Band,
    string Disclaimer,
    IReadOnlyList<ComponentScore> Components,
    KeywordAnalysis Keywords,
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<FormatIssue> FormatIssues,
    IReadOnlyList<GateApplied> GatesApplied,
    string EngineVersion,
    string LexiconVersion,
    string ContextHash);
