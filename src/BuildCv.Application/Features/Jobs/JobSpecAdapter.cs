using BuildCv.Domain.Scoring;

namespace BuildCv.Application.Features.Jobs;

/// <summary>
/// Adapta <see cref="JobSpec"/> (Application: tipado rico con validators) al
/// <see cref="JobInput"/> (Domain: mirror mínimo para scoring puro).
/// Constitution Art. VI obliga a Domain PURO sin referencias a Application;
/// el adaptador vive en la capa Application (PR 3c).
/// <para>
/// Solo se exponen los campos que <see cref="ScoringEngine.ScoreV2"/>
/// necesita: <c>Title</c> (match de seniority) y <c>Requirements</c>
/// (cobertura de skills). Description, Company, Location y EmploymentType
/// quedan fuera del cálculo del número (Art. II: el motor solo consume lo
/// declarado en su contrato).
/// </para>
/// </summary>
public static class JobSpecAdapter
{
    public static JobInput ToJobInput(JobSpec job) => new(
        Title: job.Title,
        Requirements: job.Requirements);
}
