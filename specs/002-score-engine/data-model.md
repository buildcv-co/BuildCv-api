# Data Model: 002-score-engine

> **Source of truth:** `src/BuildCv.Domain/Scoring/ScoreResult.cs`, `SkillMatcher.cs`, `KeywordAnalysis.cs`, `Recommendation.cs`, `CvProfile.cs`, `MatchResult.cs`, `ScoringEngine.cs` (commit `eded372`).

## Domain Types (inmutables, records)

```csharp
namespace BuildCv.Domain.Scoring;

/// <summary>Componentes del puntaje (Match / Structure / Achievements / Format / Length).</summary>
public enum ComponentId
{
    Match,
    Structure,
    Achievements,
    Format,
    Length,
}

/// <summary>Banda cualitativa; el número (Overall) es el valor rector (FR-010).</summary>
public enum ScoreBand
{
    Bajo,    // < 40
    Medio,   // < 65
    Bueno,   // < 85
    Fuerte,  // ≥ 85
}

/// <summary>Subpuntaje de un componente con peso, medibilidad y confianza.</summary>
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
/// Resultado del análisis determinista. Sellado con EngineVersion + LexiconVersion
/// + ContextHash para reproducibilidad (FR-006/013/031).
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

public sealed record KeywordAnalysis(
    IReadOnlyList<KeywordView> Present,
    IReadOnlyList<KeywordView> Missing,
    IReadOnlyList<KeywordView> Partial);

public sealed record KeywordView(
    string Display,
    string Category,
    string Section,
    double RequirementWeight,
    MatchTier Tier,
    Placement Placement,
    double Credit,
    string? Note);

public sealed record Recommendation(
    string Action,
    RecommendationType Type,
    ComponentId Component,
    int EstimatedGain,
    bool Invents,
    string Rationale);

public enum RecommendationType
{
    Learn,
    Surface,
    AddMetric,
    FixFormat,
    Restructure,
}

public sealed record CvProfile(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Sections);

public sealed record MatchResult(
    Requirement Requirement,
    MatchTier Tier,
    Placement Placement,
    double Credit,
    string? EvidenceSnippet);

public enum MatchTier
{
    None,
    Exact,
    Alias,
    Lemma,
    Related,
    Fuzzy,
}

public enum Placement
{
    NotFound,
    Prominent,
    Buried,
}
```

## Pesos de los componentes (`ScoringEngine.cs:8-24`)

```csharp
public const double WMatch = 0.45;
public const double WStructure = 0.20;
public const double WAchievements = 0.20;
public const double WFormat = 0.10;
public const double WLength = 0.05;
public const double FormatMeasurabilityV0 = 0.5;  // texto pegado, no PDF
public const double FormatBaselineV0 = 0.75;
public const string Version = "1.0.0";
```

La fórmula global (`ScoringEngine.ComputeOverall`):

```
numerator   = Σ (Weight * Measurability * SubScore)
denominator = Σ (Weight * Measurability)
overall     = round(100 * numerator / denominator, AwayFromZero)
```

`Measurability` ∈ [0, 1] indica qué tan observable es el componente en el input actual. Format arranca en 0.5 en v0 (texto pegado no permite detectar columnas/tablas/imágenes) — el resto arranca en 1.0. La renormalización sobre el denominador evita penalizar por información no disponible.

## Application Layer Types

```csharp
namespace BuildCv.Application.Features.Scoring;

public sealed record ScoreCvCommand(
    string CvText,
    string JobText);
```

## Api Layer (DTOs HTTP)

```csharp
namespace BuildCv.Api.Contracts;

/// <summary>Vista HTTP del ScoreResult. La forma completa (incluyendo
/// recommendations, formatIssues, gatesApplied, disclaimer) se expone al cliente.</summary>
public sealed record ScoreResponseDto(
    int Score,
    string Band,
    string Disclaimer,
    IReadOnlyList<ComponentBreakdownDto> Components,
    IReadOnlyList<KeywordDto> Present,
    IReadOnlyList<KeywordDto> Missing,
    IReadOnlyList<KeywordDto> Partial,
    IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<FormatIssueDto> FormatIssues,
    IReadOnlyList<GateAppliedDto> GatesApplied,
    string EngineVersion,
    string LexiconVersion,
    string ContextHash);

public sealed record ComponentBreakdownDto(
    string Code,
    double Weight,
    double SubScore,
    double Measurability,
    double Confidence,
    string Summary);
```

## State Machine

```
[Start]
   ↓
[Validate input] ──invalid──→ [400 ProblemDetails]
   ↓ valid
[Extract job requirements] → JobRequirementSet
   ↓
[Extract CV profile]     → CvProfile
   ↓
[Match each requirement]  → IReadOnlyList<MatchResult>  (cascada T0–T4)
   ↓
[Compute 5 components]   → IReadOnlyList<ComponentScore>
   ↓
[Apply Gates]            → IReadOnlyList<GateApplied> (no-contact, no-experience, keyword-stuffing, partial-measurement)
   ↓
[Renormalize weights]    → overall (int 0-100)
   ↓
[Compute Band]           → ScoreBand (Bajo/Medio/Bueno/Fuerte)
   ↓
[Build recommendations]  → IReadOnlyList<Recommendation>
   ↓
[Build format issues]    → IReadOnlyList<FormatIssue>
   ↓
Return ScoreResponseDto (seal: EngineVersion + LexiconVersion + ContextHash)
```

## Persistence

**NONE** (v0 mandate, Art. III). El score se calcula en memoria y se retorna al cliente. El `ContextHash` se sella en cada `ScoreResult` para reproducibilidad bit-a-bit (mismo input + misma versión del motor + misma versión del gazetteer + mismo hash de contexto = mismo número).
