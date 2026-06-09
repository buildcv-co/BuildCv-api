using System.Text.RegularExpressions;

namespace BuildCv.Application.Features.Import;

/// <summary>
/// Detecta secciones candidatas por regex sobre headers en MAYÚSCULAS.
/// Función pura: entra texto, sale lista de ImportSection. Sin IO, sin reloj.
/// Constitución Art. VI: "no sobre-ingeniería" — heurística simple y honesta
/// sobre su alcance (D05 del research).
/// </summary>
public static class SectionDetector
{
    public const string ConfidenceHigh = "High";
    public const string ConfidenceLow = "Low";

    private static readonly string[] SpanishHeaders =
    [
        "EXPERIENCIA", "EDUCACION", "EDUCACIÓN", "HABILIDADES",
        "PROYECTOS", "CONTACTO", "PERFIL", "RESUMEN",
        "IDIOMAS", "CERTIFICACIONES", "REFERENCIAS", "PUBLICACIONES",
    ];

    private static readonly string[] EnglishHeaders =
    [
        "EXPERIENCE", "EDUCATION", "SKILLS", "PROJECTS",
        "CONTACT", "PROFILE", "SUMMARY", "LANGUAGES",
        "CERTIFICATIONS", "REFERENCES", "PUBLICATIONS",
    ];

    private static readonly string[] AllHeaders = [.. SpanishHeaders, .. EnglishHeaders];

    private static readonly Regex HeaderPattern = new(
        @"^\s*(?<heading>" + string.Join("|", AllHeaders) + @")\s*[\.\:]*\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PreProcessPattern = new(
        @"(?<=[^\n])(?=(?:" + string.Join("|", AllHeaders) + @"))",
        RegexOptions.Compiled);

    private static readonly Regex PostProcessPattern = new(
        @"(?:" + string.Join("|", AllHeaders) + @")(?=[^\n\.\:])",
        RegexOptions.Compiled);

    public static IReadOnlyList<ImportSection> Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<ImportSection>();
        }

        var matches = HeaderPattern.Matches(text);
        if (matches.Count == 0)
        {
            var preProcessed = PreProcessPattern.Replace(text, "\n");
            preProcessed = PostProcessPattern.Replace(preProcessed, "$&\n");
            matches = HeaderPattern.Matches(preProcessed);
            if (matches.Count == 0)
            {
                return Array.Empty<ImportSection>();
            }
            text = preProcessed;
        }

        var sections = new List<ImportSection>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var heading = match.Groups["heading"].Value;
            var start = match.Index + match.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var lineTrim = match.Value.Trim().TrimEnd('.', ':', ' ');

            var confidence = lineTrim.Equals(heading, StringComparison.Ordinal)
                ? ConfidenceHigh
                : ConfidenceLow;

            sections.Add(new ImportSection(heading, start, end, confidence));
        }

        return sections;
    }
}
