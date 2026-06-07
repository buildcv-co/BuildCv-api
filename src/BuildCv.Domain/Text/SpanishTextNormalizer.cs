using System.Globalization;
using System.Text;

namespace BuildCv.Domain.Text;

/// <summary>
/// Normalizador determinista para español (D02). Orden: NFKC → minúsculas
/// (cultura invariante) → proteger tokens técnicos → quitar diacríticos
/// preservando la Ñ → limpiar puntuación → restaurar técnicos → colapsar espacios.
/// </summary>
public sealed class SpanishTextNormalizer : ITextNormalizer
{
    // Tokens técnicos cuya puntuación es significativa. Si no se protegen, el paso
    // de limpieza los destruiría ("c#" -> "c"). Orden por longitud descendente para
    // que "asp.net" se proteja antes que ".net".
    private static readonly string[] ProtectedTokens =
    [
        "asp.net", "node.js", "next.js", "vue.js", "objective-c",
        ".net", "ci/cd", "c++", "c#", "f#",
    ];

    private const char EnyePlaceholder = '';
    private const char MarkerStart = '';
    private const char MarkerEnd = '';

    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // 1) NFKC + minúsculas invariantes.
        var text = input.Normalize(NormalizationForm.FormKC).ToLowerInvariant();

        // 2) Proteger tokens técnicos con marcadores que sobreviven a la limpieza.
        var restorations = new List<string>();
        foreach (var token in ProtectedTokens)
        {
            int index;
            while ((index = text.IndexOf(token, StringComparison.Ordinal)) >= 0)
            {
                var placeholder = $"{MarkerStart}{restorations.Count}{MarkerEnd}";
                restorations.Add(token);
                text = string.Concat(text.AsSpan(0, index), placeholder, text.AsSpan(index + token.Length));
            }
        }

        // 3) Quitar diacríticos preservando la Ñ ("año" != "ano").
        text = text.Replace('ñ', EnyePlaceholder);
        text = RemoveDiacritics(text);
        text = text.Replace(EnyePlaceholder, 'ñ');

        // 4) Limpiar puntuación: conservar letras, dígitos, espacios y marcadores.
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch is MarkerStart or MarkerEnd)
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }

        // 5) Restaurar tokens técnicos y colapsar espacios.
        var cleaned = sb.ToString();
        for (var i = 0; i < restorations.Count; i++)
        {
            cleaned = cleaned.Replace($"{MarkerStart}{i}{MarkerEnd}", restorations[i]);
        }

        return CollapseWhitespace(cleaned);
    }

    public IReadOnlyList<string> Tokenize(string input)
    {
        var normalized = Normalize(input);
        return normalized.Length == 0
            ? []
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CollapseWhitespace(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}
