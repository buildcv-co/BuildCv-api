namespace BuildCv.Domain.Scoring;

/// <summary>
/// Sobre del resultado del motor v2 (Art. II). Envuelve el
/// <see cref="ScoreResult"/> legacy con desglose por sección y red flags.
/// El <see cref="EngineVersion"/> se sella como constante pública para que
/// las pruebas y los consumidores puedan fijarlo sin ambigüedad (FR-006).
/// </summary>
public sealed record ScoreResultV2
{
    /// <summary>Versión del motor que produjo este sobre. Se incluye en
    /// el JSON de respuesta y se usa para ruteo de contrato (v1 vs v2).</summary>
    public const string CurrentEngineVersion = "2.0.0";

    /// <summary>Resultado heredado de v1 (componentes, keywords, gates).
    /// Se conserva íntegro para no romper consumidores existentes.</summary>
    public required ScoreResult Legacy { get; init; }

    /// <summary>Sub-puntaje por sección del CV (experience/education/skills/
    /// certifications/contact). Cada valor está clampado a 0–100.</summary>
    public required PerSectionScore PerSection { get; init; }

    /// <summary>Red flags detectadas (gaps, job hopping, etc.). Señal pura,
    /// nunca deducción (Art. I).</summary>
    public required IReadOnlyList<RedFlag> RedFlags { get; init; }

    /// <summary>Versión del motor efectiva. Constante, derivada de
    /// <see cref="CurrentEngineVersion"/>.</summary>
    public string EngineVersion => CurrentEngineVersion;

    /// <summary>Puntaje global, delegado al resultado legacy.</summary>
    public int OverallScore => Legacy.Overall;

    /// <summary>Banda cualitativa textual, derivada del resultado legacy
    /// para que la UI no consuma el enum directamente.</summary>
    public string Band => Legacy.Band.ToString();

    /// <summary>Construye un sobre v2 a partir del resultado legacy sin
    /// haber calculado aún el desglose por sección. La lógica real de
    /// scoring por sección llega en una iteración posterior; aquí solo se
    /// garantiza la forma del sobre (compatibilidad de contrato).</summary>
    public static ScoreResultV2 FromLegacy(ScoreResult legacy)
        => new()
        {
            Legacy = legacy,
            PerSection = PerSectionScore.Zero,
            RedFlags = Array.Empty<RedFlag>(),
        };
}
