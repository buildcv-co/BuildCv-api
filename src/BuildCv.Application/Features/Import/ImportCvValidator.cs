using FluentValidation;

namespace BuildCv.Application.Features.Import;

public sealed class ImportCvValidator : AbstractValidator<ImportCvCommand>
{
    public ImportCvValidator()
    {
        RuleFor(x => x.OriginalFileName)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .Must(BePdfOrDocxMime)
            .WithMessage("El tipo MIME debe ser application/pdf o application/vnd.openxmlformats-officedocument.wordprocessingml.document.");

        RuleFor(x => x.TraceId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.FileBytes)
            .NotNull();
    }

    private static bool BePdfOrDocxMime(string mime)
    {
        if (string.IsNullOrEmpty(mime))
        {
            return false;
        }

        return mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || mime.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase);
    }
}
