namespace BuildCv.Domain.Text;

/// <summary>
/// Métricas de similitud deterministas para el último nivel de la cascada de match
/// (fuzzy, D02). Implementadas a mano para mantener el dominio sin dependencias.
/// </summary>
public static class StringSimilarity
{
    /// <summary>Distancia de edición de Levenshtein (número de inserciones/borrados/sustituciones).</summary>
    public static int Levenshtein(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>Levenshtein normalizado a [0,1]; 1 = idénticos.</summary>
    public static double NormalizedLevenshtein(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0 && b.Length == 0)
        {
            return 1.0;
        }

        var max = Math.Max(a.Length, b.Length);
        return 1.0 - ((double)Levenshtein(a, b) / max);
    }

    /// <summary>Similitud de Jaro-Winkler en [0,1]; favorece coincidencias de prefijo.</summary>
    public static double JaroWinkler(string a, string b)
    {
        var jaro = Jaro(a, b);

        var maxPrefix = Math.Min(4, Math.Min(a.Length, b.Length));
        var prefix = 0;
        while (prefix < maxPrefix && a[prefix] == b[prefix])
        {
            prefix++;
        }

        return jaro + (prefix * 0.1 * (1 - jaro));
    }

    private static double Jaro(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0 && b.Length == 0)
        {
            return 1.0;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0.0;
        }

        var matchDistance = Math.Max(0, (Math.Max(a.Length, b.Length) / 2) - 1);
        var aMatches = new bool[a.Length];
        var bMatches = new bool[b.Length];
        var matches = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(i + matchDistance + 1, b.Length);
            for (var j = start; j < end; j++)
            {
                if (bMatches[j] || a[i] != b[j])
                {
                    continue;
                }

                aMatches[i] = true;
                bMatches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0)
        {
            return 0.0;
        }

        double transpositions = 0;
        var k = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (!aMatches[i])
            {
                continue;
            }

            while (!bMatches[k])
            {
                k++;
            }

            if (a[i] != b[k])
            {
                transpositions += 0.5;
            }

            k++;
        }

        double m = matches;
        return ((m / a.Length) + (m / b.Length) + ((m - transpositions) / m)) / 3.0;
    }
}
