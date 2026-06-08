using FluentValidation;

namespace BuildCv.Application.Features.Export;

public sealed class ExportPdfValidator : AbstractValidator<ExportPdfCommand>
{
    public ExportPdfValidator()
    {
        RuleFor(x => x.AdaptedCv)
            .NotEmpty()
            .MaximumLength(50_000);

        RuleFor(x => x.CandidateName)
            .MaximumLength(100);

        RuleFor(x => x.Validation)
            .NotNull();
    }
}
