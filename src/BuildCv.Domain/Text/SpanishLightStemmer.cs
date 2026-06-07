namespace BuildCv.Domain.Text;

/// <summary>
/// Stemmer ligero de español por recorte de sufijos (plurales y formas verbales/derivadas
/// frecuentes). Determinista y conservador: nunca recorta por debajo de una raíz mínima.
/// Opera sobre texto ya normalizado (sin acentos). Es el fallback del nivel lema de la
/// cascada (D02); los términos técnicos se resuelven antes por el gazetteer y no se estiman.
/// </summary>
public sealed class SpanishLightStemmer : ISpanishStemmer
{
    private const int MinStemLength = 3;

    // Sufijos sin acentos, ordenados por longitud descendente para recortar el más largo primero.
    private static readonly string[] Suffixes = BuildSuffixes();

    public string Stem(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word;
        }

        foreach (var suffix in Suffixes)
        {
            if (word.Length - suffix.Length >= MinStemLength
                && word.EndsWith(suffix, StringComparison.Ordinal))
            {
                return word[..^suffix.Length];
            }
        }

        return word;
    }

    private static string[] BuildSuffixes()
    {
        string[] suffixes =
        [
            "amientos", "imientos", "amiento", "imiento", "aciones", "iciones",
            "idades", "logias",
            "acion", "icion", "ables", "ibles", "ancia", "encia", "mente",
            "ando", "iendo", "ados", "idos", "amos", "emos", "imos", "aron", "ieron",
            "ado", "ido", "ora", "ores", "oras",
            "ar", "er", "ir", "or", "as", "os", "es", "an", "en",
            "a", "o", "e", "s",
        ];

        Array.Sort(suffixes, static (x, y) => y.Length.CompareTo(x.Length));
        return suffixes;
    }
}
