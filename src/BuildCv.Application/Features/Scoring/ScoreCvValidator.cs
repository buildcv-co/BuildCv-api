using BuildCv.Application.Features.Jobs;
using FluentValidation;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Validación del comando discriminada por <c>EngineVersion</c>. La rama
/// v1 aplica los límites de texto largo (línea base) y la rama v2 aplica
/// <see cref="JobSpecValidator"/>. Los códigos de error deben coincidir
/// 1:1 con los del esquema Zod del frontend (parity test).
/// </summary>
public sealed class ScoreCvValidator : AbstractValidator<ScoreCvCommand>
{
    public ScoreCvValidator()
    {
        RuleFor(x => x.EngineVersion)
            .Must(v => v == EngineVersions.V1 || v == EngineVersions.V2)
            .WithErrorCode("ENGINE_VERSION_UNKNOWN");

        When(x => x.EngineVersion == EngineVersions.V2, () =>
        {
            RuleFor(x => x)
                .Must(x => x is StructuredScoreCommand)
                .WithErrorCode("JOB_SPEC_REQUIRED");

            RuleFor(x => x)
                .Must(x => x is StructuredScoreCommand structured
                    && new JobSpecValidator().Validate(structured.Job).IsValid)
                .WithMessage("El JobSpec no cumple el contrato v2 (ver códigos de JobSpecValidator).");
        });

        When(x => x.EngineVersion == EngineVersions.V1, () =>
        {
            RuleFor(x => x)
                .Must(x => x is TextScoreCommand)
                .WithErrorCode("VERSION_MISMATCH");

            RuleFor(x => x)
                .Must(x => x is TextScoreCommand text
                    && text.CvText.Length >= 200
                    && text.CvText.Length <= 20_000
                    && text.JobText.Length >= 100
                    && text.JobText.Length <= 20_000)
                .WithMessage("Los textos no cumplen los límites v1 (cv 200-20000, job 100-20000).");
        });
    }
}
