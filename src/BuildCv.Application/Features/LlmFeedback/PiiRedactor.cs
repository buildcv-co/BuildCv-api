using System.Text.RegularExpressions;

namespace BuildCv.Application.Features.LlmFeedback;

public static partial class PiiRedactor
{
    public const int MaxInputCharacters = 100_000;

    private static readonly string[] AllowedUrlHosts =
    [
        "linkedin.com",
        "www.linkedin.com",
        "github.com",
        "www.github.com",
    ];

    public static string Redact(string input)
    {
        try
        {
            if (input.Length > MaxInputCharacters)
            {
                throw new LlmFeedbackRedactionException();
            }

            var redacted = EmailRegex().Replace(input, "[EMAIL_REDACTED]");
            redacted = PhoneRegex().Replace(redacted, "[PHONE_REDACTED]");
            redacted = UrlRegex().Replace(redacted, match => IsAllowedProfessionalUrl(match.Value) ? match.Value : "[URL_REDACTED]");
            return redacted.Contains('{', StringComparison.Ordinal)
                ? AddressRegex().Replace(redacted, "[ADDRESS_REDACTED]")
                : FullLineAddressRegex().Replace(redacted, "[ADDRESS_REDACTED]");
        }
        catch (LlmFeedbackRedactionException)
        {
            throw;
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new LlmFeedbackRedactionException() { Source = ex.Source };
        }
    }

    private static bool IsAllowedProfessionalUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && AllowedUrlHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 250)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+?(?:57|1|34)[\s.-]?)?(?:\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}|\d{3}[\s.-]?\d{3}[\s.-]?\d{3})(?!\d)", RegexOptions.CultureInvariant, 250)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"https?://[^\s)]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 250)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b(?:calle|carrera|avenida|direcci[oó]n)\b[^\n\r,.;}\]]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 250)]
    private static partial Regex AddressRegex();

    [GeneratedRegex(@"(?im)^.*\b(?:calle|carrera|avenida|direcci[oó]n)\b.*$", RegexOptions.CultureInvariant, 250)]
    private static partial Regex FullLineAddressRegex();
}
