using BuildCv.Domain.Lexicon;

namespace BuildCv.Domain.Jobs;

/// <summary>Sección de la vacante donde aparece el requisito; modula su peso.</summary>
public enum RequirementSection
{
    MustHave,
    Responsibility,
    NiceToHave,
    Title,
}

/// <summary>
/// Requisito extraído de la vacante. <see cref="CanonicalId"/> es el id del gazetteer
/// si se resolvió; de lo contrario, el término normalizado (keyword genérica).
/// </summary>
public sealed record Requirement(
    string CanonicalId,
    string Display,
    SkillCategory Category,
    RequirementSection Section,
    double Weight);

/// <summary>
/// Conjunto de requisitos de una vacante. <see cref="ContextHash"/> sella la extracción
/// para que el CV adaptado se re-puntúe contra el mismo set (reproducibilidad, FR-031).
/// </summary>
public sealed record JobRequirementSet(
    IReadOnlyList<Requirement> Requirements,
    string ContextHash);
