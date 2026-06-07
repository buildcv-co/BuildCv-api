namespace BuildCv.Domain.Scoring;

/// <summary>Tipo de recomendación (FR-022). Solo "Learn" señala una brecha real.</summary>
public enum RecommendationType
{
    Surface,
    Rewrite,
    AddMetric,
    FixFormat,
    Learn,
}

/// <summary>
/// Recomendación priorizada por impacto estimado. <see cref="Invents"/> es siempre
/// false para acciones ejecutables; las brechas reales (tipo Learn) nunca fabrican (FR-022).
/// </summary>
public sealed record Recommendation(
    string Action,
    RecommendationType Type,
    ComponentId Component,
    int EstimatedGain,
    bool Invents,
    string HonestyNote);
