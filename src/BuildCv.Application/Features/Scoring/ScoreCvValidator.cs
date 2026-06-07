using FluentValidation;

namespace BuildCv.Application.Features.Scoring;

/// <summary>Reglas de validación del comando (FR-002, FR-037).</summary>
public sealed class ScoreCvValidator : AbstractValidator<ScoreCvCommand>
{
    public ScoreCvValidator()
    {
        RuleFor(command => command.CvText)
            .NotEmpty()
            .MinimumLength(200)
            .MaximumLength(20000);

        RuleFor(command => command.JobText)
            .NotEmpty()
            .MinimumLength(100)
            .MaximumLength(20000);
    }
}
