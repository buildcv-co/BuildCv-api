using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;

namespace BuildCv.Domain.Scoring;

/// <summary>Vista de un keyword de la vacante y cómo coincidió con el CV (FR-019).</summary>
public sealed record KeywordView(
    string CanonicalTerm,
    SkillCategory Category,
    RequirementSection Section,
    double Weight,
    MatchTier MatchLevel,
    Placement Location,
    double Credit,
    string Note);

/// <summary>Clasificación de keywords: presentes, faltantes y parciales.</summary>
public sealed record KeywordAnalysis(
    IReadOnlyList<KeywordView> Present,
    IReadOnlyList<KeywordView> Missing,
    IReadOnlyList<KeywordView> Partial);
