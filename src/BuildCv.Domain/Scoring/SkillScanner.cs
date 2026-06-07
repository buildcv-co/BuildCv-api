using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Text;

namespace BuildCv.Domain.Scoring;

/// <summary>
/// Resuelve los skills presentes en un texto contra el gazetteer, probando n-gramas
/// (trigrama → bigrama → unigrama) de izquierda a derecha y consumiendo el más largo
/// que resuelva, para no contar dos veces. Devuelve id canónico → frecuencia.
/// </summary>
public sealed class SkillScanner(ISkillGazetteer gazetteer, ITextNormalizer normalizer)
{
    private const int MaxGram = 3;

    public IReadOnlyDictionary<string, int> Scan(string text)
    {
        var tokens = normalizer.Tokenize(text);
        var hits = new Dictionary<string, int>(StringComparer.Ordinal);

        var i = 0;
        while (i < tokens.Count)
        {
            var matched = false;
            var maxN = Math.Min(MaxGram, tokens.Count - i);
            for (var n = maxN; n >= 1; n--)
            {
                var gram = n == 1 ? tokens[i] : string.Join(' ', tokens.Skip(i).Take(n));
                if (gazetteer.TryResolve(gram, out var entry))
                {
                    hits[entry.Id] = hits.GetValueOrDefault(entry.Id) + 1;
                    i += n;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                i++;
            }
        }

        return hits;
    }
}
