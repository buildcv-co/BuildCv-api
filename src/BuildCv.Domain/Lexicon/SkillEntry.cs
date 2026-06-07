namespace BuildCv.Domain.Lexicon;

/// <summary>Categoría de un término del diccionario; afecta el peso base del requisito (FR-014).</summary>
public enum SkillCategory
{
    HardSkill,
    Tool,
    SoftSkill,
    GenericKeyword,
}

/// <summary>
/// Entrada del diccionario de habilidades (gazetteer). Recurso versionado, inmutable.
/// Las relaciones (<see cref="Implies"/>, <see cref="Related"/>, <see cref="Broader"/>)
/// alimentan el crédito por nivel de la cascada de match (FR-018); <see cref="ConfusableWith"/>
/// evita falsos positivos difusos (FR-017).
/// </summary>
public sealed record SkillEntry(
    string Id,
    string Canonical,
    SkillCategory Category,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Implies,
    IReadOnlyList<string> Related,
    IReadOnlyList<string> Broader,
    IReadOnlyList<string> ConfusableWith);
