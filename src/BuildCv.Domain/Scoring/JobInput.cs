namespace BuildCv.Domain.Scoring;

/// <summary>
/// Entrada estructurada de la vacante para el motor de puntaje v2.
/// Mirror mínimo de <c>BuildCv.Application.Features.Jobs.JobSpec</c>
/// con los únicos campos que <see cref="ScoringEngine.ScoreV2"/> necesita.
/// Constitution Art. VI (Domain PURO) obliga a no referenciar Application
/// desde Domain; el adaptador <c>JobSpec → JobInput</c> se hace en la capa
/// Application (PR 3c). Solo se expone lo necesario para scoring: título
/// (match de seniority) y requisitos (cobertura de skills).
/// </summary>
public sealed record JobInput(
    string Title,
    IReadOnlyList<string> Requirements);
