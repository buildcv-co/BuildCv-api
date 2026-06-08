using FluentValidation;

namespace BuildCv.Application.Features.Adapt;

public sealed class AdaptCvValidator : AbstractValidator<AdaptCvCommand>
{
    public AdaptCvValidator()
    {
        RuleFor(x => x.CvText)
            .NotEmpty()
            .MaximumLength(50_000);

        RuleFor(x => x.JobText)
            .NotEmpty()
            .MaximumLength(20_000);

        RuleFor(x => x)
            .Must(NotBeIdentical)
            .WithMessage("El CV y la vacante no pueden ser idénticos.")
            .WithName("AdaptCvCommand");
    }

    private static bool NotBeIdentical(AdaptCvCommand c) =>
        !string.Equals(c.CvText.Trim(), c.JobText.Trim(), StringComparison.Ordinal);
}
