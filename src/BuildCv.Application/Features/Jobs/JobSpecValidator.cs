using System.Text.RegularExpressions;
using FluentValidation;

namespace BuildCv.Application.Features.Jobs;

/// <summary>
/// Validación determinista de <see cref="JobSpec"/>. Refleja el contrato
/// del esquema Zod en <c>BuildCv-web/lib/job/job-spec.ts</c>; los códigos de
/// error y la lista de patrones anti-injection deben coincidir 1:1
/// (parity test). La validación rechaza el payload ANTES de cualquier costo
/// de IA (Constitution Art. V).
/// </summary>
public sealed class JobSpecValidator : AbstractValidator<JobSpec>
{
    public const int TitleMaxLength = 200;
    public const int CompanyMaxLength = 200;
    public const int DescriptionMaxLength = 5000;
    public const int LocationMaxLength = 200;
    public const int RequirementMaxLength = 500;
    public const int RequirementsMaxItems = 50;
    public const int RequirementsMinItems = 1;

    /// <summary>
    /// Substrings que se rechazan en cualquier campo de texto libre.
    /// Coincidencia case-insensitive. Debe coincidir con
    /// <c>PROMPT_INJECTION_PATTERNS</c> en el frontend.
    /// </summary>
    public static readonly IReadOnlyList<string> PromptInjectionPatterns = new[]
    {
        "ignore previous",
        "system:",
        "<|im_start|>",
        "assistant:",
    };

    private static readonly Regex ControlCharsRegex = new(
        @"[\x00-\x1F\x7F]", RegexOptions.Compiled);

    private static readonly Regex ZeroWidthRegex = new(
        @"[\u200B-\u200D\uFEFF]", RegexOptions.Compiled);

    public JobSpecValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(TitleMaxLength)
            .Must(NotContainInjection)
            .WithErrorCode("JOB_SPEC_PROMPT_INJECTION");

        RuleFor(x => x.Company)
            .NotEmpty()
            .MaximumLength(CompanyMaxLength)
            .Must(NotContainInjection)
            .WithErrorCode("JOB_SPEC_PROMPT_INJECTION");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(DescriptionMaxLength)
            .Must(NotContainInjection)
            .WithErrorCode("JOB_SPEC_PROMPT_INJECTION");

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(LocationMaxLength)
            .Must(NotContainInjection)
            .WithErrorCode("JOB_SPEC_PROMPT_INJECTION");

        RuleFor(x => x.EmploymentType)
            .IsInEnum()
            .WithErrorCode("JOB_SPEC_INVALID_ENUM");

        RuleFor(x => x.Requirements)
            .Must((spec, requirements) => requirements.Count >= RequirementsMinItems)
            .WithErrorCode("JOB_SPEC_MISSING_REQUIREMENTS");

        RuleFor(x => x.Requirements)
            .Must((spec, requirements) => requirements.Count <= RequirementsMaxItems)
            .WithErrorCode("JOB_SPEC_FIELD_TOO_LONG");

        RuleForEach(x => x.Requirements)
            .NotEmpty()
            .MaximumLength(RequirementMaxLength)
            .Must(NotContainInjection)
            .WithErrorCode("JOB_SPEC_PROMPT_INJECTION");
    }

    private static bool NotContainInjection(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            // Las reglas NotEmpty/MaximumLength se encargan; aquí solo
            // tratamos el aspecto de inyección.
            return true;
        }

        if (ControlCharsRegex.IsMatch(value))
        {
            return false;
        }

        if (ZeroWidthRegex.IsMatch(value))
        {
            return false;
        }

        foreach (var pattern in PromptInjectionPatterns)
        {
            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
