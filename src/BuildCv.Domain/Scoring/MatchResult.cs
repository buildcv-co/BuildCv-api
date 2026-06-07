using BuildCv.Domain.Jobs;

namespace BuildCv.Domain.Scoring;

/// <summary>Nivel de la cascada de coincidencia (FR-015).</summary>
public enum MatchTier
{
    None,
    Exact,
    Alias,
    Lemma,
    Related,
    Fuzzy,
}

/// <summary>Prominencia del término en el CV; aplica el factor de ubicación (FR-018).</summary>
public enum Placement
{
    Prominent,
    Buried,
    NotFound,
}

/// <summary>Resultado de coincidencia de un requisito contra el CV.</summary>
public sealed record MatchResult(
    Requirement Requirement,
    MatchTier Tier,
    Placement Placement,
    double Credit,
    string? EvidenceSnippet);
