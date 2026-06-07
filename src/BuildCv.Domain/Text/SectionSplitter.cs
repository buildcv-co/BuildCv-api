using System.Text;

namespace BuildCv.Domain.Text;

/// <summary>Una sección de texto detectada (encabezado canónico + cuerpo).</summary>
public sealed record TextSection(string Label, string Body);

/// <summary>
/// Divide un texto en secciones a partir de líneas que parecen encabezados. Determinista:
/// los encabezados se evalúan en orden estable (más largos primero). Las líneas previas al
/// primer encabezado quedan bajo <paramref name="preambleLabel"/> (típicamente el nombre/título).
/// </summary>
public sealed class SectionSplitter(ITextNormalizer normalizer)
{
    private const int MaxHeaderWords = 5;

    public IReadOnlyList<TextSection> Split(
        string text,
        IReadOnlyDictionary<string, string> headerKeywords,
        string preambleLabel)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(headerKeywords);

        var ordered = headerKeywords
            .OrderByDescending(pair => pair.Key.Length)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        var sections = new List<TextSection>();
        var currentLabel = preambleLabel;
        var body = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            var header = MatchHeader(line, ordered);
            if (header is not null)
            {
                sections.Add(new TextSection(currentLabel, body.ToString().Trim()));
                body.Clear();
                currentLabel = header;
            }
            else
            {
                body.AppendLine(line);
            }
        }

        sections.Add(new TextSection(currentLabel, body.ToString().Trim()));
        return sections;
    }

    private string? MatchHeader(string line, List<KeyValuePair<string, string>> ordered)
    {
        var normalized = normalizer.Normalize(line);
        if (normalized.Length == 0 || normalized.Split(' ').Length > MaxHeaderWords)
        {
            return null;
        }

        foreach (var (keyword, label) in ordered)
        {
            if (normalized == keyword || normalized.StartsWith(keyword + " ", StringComparison.Ordinal))
            {
                return label;
            }
        }

        return null;
    }
}
