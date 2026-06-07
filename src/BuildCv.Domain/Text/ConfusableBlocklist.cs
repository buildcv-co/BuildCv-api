namespace BuildCv.Domain.Text;

/// <summary>
/// Lista simétrica de términos que NUNCA deben considerarse coincidencia difusa
/// entre sí (FR-017). Un solo falso positivo catastrófico (java ⇎ javascript,
/// Jaro-Winkler ≈ 0.90) destruiría la credibilidad del puntaje.
/// </summary>
public sealed class ConfusableBlocklist
{
    private static readonly (string, string)[] DefaultPairs =
    [
        ("java", "javascript"),
        ("c", "c#"),
        ("c", "c++"),
        ("c#", "c++"),
        ("react", "react native"),
        ("go", "mongo"),
        ("r", "ruby"),
        ("php", "perl"),
        ("scala", "java"),
        ("kotlin", "scala"),
    ];

    private readonly HashSet<(string, string)> _pairs;

    public ConfusableBlocklist()
        : this(DefaultPairs)
    {
    }

    public ConfusableBlocklist(IEnumerable<(string A, string B)> pairs)
    {
        _pairs = [];
        foreach (var (a, b) in pairs)
        {
            _pairs.Add(Key(a, b));
        }
    }

    /// <summary>True si <paramref name="a"/> y <paramref name="b"/> son términos confundibles distintos.</summary>
    public bool AreConfusable(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _pairs.Contains(Key(a, b));
    }

    private static (string, string) Key(string a, string b)
    {
        var x = a.Trim().ToLowerInvariant();
        var y = b.Trim().ToLowerInvariant();
        return string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
    }
}
