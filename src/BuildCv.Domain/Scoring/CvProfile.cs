namespace BuildCv.Domain.Scoring;

/// <summary>
/// Vista del CV que necesita el matcher: skills detectados con su ubicación, y los
/// tokens/raíces normalizados para los niveles lema y fuzzy de la cascada. La produce
/// el análisis del CV (sin IO); el matcher solo la consume.
/// </summary>
public sealed record CvProfile(
    IReadOnlyDictionary<string, Placement> SkillPlacements,
    IReadOnlySet<string> Tokens,
    IReadOnlySet<string> Stems);
