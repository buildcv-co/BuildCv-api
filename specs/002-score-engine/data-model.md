# Data Model: 002-score-engine

## Domain Types (inmutables, records)

```csharp
namespace BuildCv.Domain.Scoring;

/// Resultado del análisis determinista. Sellado con <see cref="EngineVersion"/>
/// + <see cref="GazetteerVersion"/> para reproducibilidad (FR-006, FR-013).
public sealed record ScoreResult(
    int Score,
    string Band,
    IReadOnlyList<ComponentBreakdown> Components,
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing,
    string EngineVersion,
    string GazetteerVersion);

public sealed record ComponentBreakdown(
    string Code,
    double Weight,
    double Value,
    string Rationale);

public sealed record KeywordAnalysis(
    IReadOnlyList<string> JobKeywords,
    IReadOnlyList<string> CvKeywords,
    IReadOnlyList<string> Matched,
    IReadOnlyList<string> Missing);

public sealed record MatchResult(
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Related);

public sealed record Recommendation(
    string Priority,
    string Action,
    string Rationale);

public sealed record CvProfile(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Sections);

/// Bandas cualitativas del score (FR-010).
public static class ScoreBands
{
    public const string Excellent = "Excellent";   // 80-100
    public const string Strong = "Strong";         // 60-79
    public const string Moderate = "Moderate";     // 40-59
    public const string Weak = "Weak";             // 20-39
    public const string Insufficient = "Insufficient"; // 0-19

    public static string FromScore(int score) => score switch
    {
        >= 80 => Excellent,
        >= 60 => Strong,
        >= 40 => Moderate,
        >= 20 => Weak,
        _ => Insufficient
    };
}
```

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

public sealed record ScoreResponseDto(
    int Score,
    string Band,
    IReadOnlyList<ComponentBreakdownDto> Components,
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing,
    string EngineVersion);

public sealed record ComponentBreakdownDto(
    string Code,
    double Weight,
    double Value,
    string Rationale);

public static class ScoreResponseMapper
{
    public static ScoreResponseDto Map(ScoreResult result) => new(
        Score: result.Score,
        Band: result.Band,
        Components: result.Components
            .Select(c => new ComponentBreakdownDto(c.Code, c.Weight, c.Value, c.Rationale))
            .ToList(),
        Present: result.Present,
        Missing: result.Missing,
        EngineVersion: result.EngineVersion);
}
```

## State Machine

```
[Start]
   ↓
[Validate input] ──invalid──→ [400 ProblemDetails]
   ↓ valid
[Analyze job]  → jobKeywords
   ↓
[Analyze CV]   → cvProfile
   ↓
[Match skills] → MatchResult
   ↓
[Compute components] → ComponentBreakdown[]
   ↓
[Renormalize weights] (skip non-observable)
   ↓
[Aggregate to final score] → ScoreResult
   ↓
[Generate recommendations]
   ↓
Return ScoreResponseDto
```

## Persistence

**NONE** (v0 mandate, Art. III). El score se calcula en memoria y se retorna al cliente.
