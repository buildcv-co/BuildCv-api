namespace BuildCv.Application.Features.Jobs;

/// <summary>
/// Modalidad contractual declarada en la vacante. El enum es cerrado y debe
/// coincidir 1:1 con <c>EmploymentTypeSchema</c> en
/// <c>BuildCv-web/lib/job/job-spec.ts</c>.
/// </summary>
public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Internship,
    Temporary,
}

/// <summary>
/// Especificación estructurada de la vacante. Es la entrada del motor de
/// puntaje v2 (<c>engineVersion: "2.0.0"</c>) y reemplaza al par
/// <c>{CvText, JobText}</c> de la línea base (FR-037, Constitution Art. V).
/// </summary>
public sealed record JobSpec(
    string Title,
    string Company,
    string Description,
    string Location,
    EmploymentType EmploymentType,
    IReadOnlyList<string> Requirements);
