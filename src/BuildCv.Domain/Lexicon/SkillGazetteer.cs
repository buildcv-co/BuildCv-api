using System.Diagnostics.CodeAnalysis;
using BuildCv.Domain.Text;

namespace BuildCv.Domain.Lexicon;

/// <summary>
/// Implementación pura del diccionario de habilidades: construye índices inmutables
/// (por id, por token normalizado) y un conjunto simétrico de confundibles. Sin IO.
/// </summary>
public sealed class SkillGazetteer : ISkillGazetteer
{
    private readonly IReadOnlyDictionary<string, SkillEntry> _byId;
    private readonly IReadOnlyDictionary<string, SkillEntry> _byToken;
    private readonly HashSet<(string, string)> _confusables;

    public SkillGazetteer(string version, IEnumerable<SkillEntry> entries, ITextNormalizer normalizer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(normalizer);

        Version = version;

        var byId = new Dictionary<string, SkillEntry>(StringComparer.Ordinal);
        var byToken = new Dictionary<string, SkillEntry>(StringComparer.Ordinal);
        var confusables = new HashSet<(string, string)>();

        foreach (var entry in entries)
        {
            byId[entry.Id] = entry;

            // Indexa el canónico y todos sus alias en forma normalizada. El primero gana
            // ante colisiones para mantener el resultado determinista.
            foreach (var token in entry.Aliases.Prepend(entry.Canonical))
            {
                var normalized = normalizer.Normalize(token);
                if (normalized.Length > 0)
                {
                    byToken.TryAdd(normalized, entry);
                }
            }

            foreach (var other in entry.ConfusableWith)
            {
                confusables.Add(Pair(entry.Id, other));
            }
        }

        _byId = byId;
        _byToken = byToken;
        _confusables = confusables;
    }

    public string Version { get; }

    public bool TryResolve(string normalizedToken, [MaybeNullWhen(false)] out SkillEntry entry)
        => _byToken.TryGetValue(normalizedToken, out entry);

    public bool TryGetById(string canonicalId, [MaybeNullWhen(false)] out SkillEntry entry)
        => _byId.TryGetValue(canonicalId, out entry);

    public IReadOnlyList<string> Related(string canonicalId)
        => _byId.TryGetValue(canonicalId, out var entry) ? entry.Related : [];

    public IReadOnlyList<string> Implies(string canonicalId)
        => _byId.TryGetValue(canonicalId, out var entry) ? entry.Implies : [];

    public bool AreConfusable(string a, string b)
        => !string.Equals(a, b, StringComparison.Ordinal) && _confusables.Contains(Pair(a, b));

    private static (string, string) Pair(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
