namespace BuildCv.Domain.Scoring;

/// <summary>
/// Sub-puntaje por sección del CV (Art. II FR-011). Cada entero está
/// clampado al rango <see cref="Min"/>–<see cref="Max"/>. Una sección
/// ausente del CV se modela como <c>null</c> en el contrato de salida y
/// renormaliza el puntaje global (responsabilidad del motor, no de este
/// record).
/// </summary>
public sealed record PerSectionScore
{
    /// <summary>Límite inferior permitido para cada sub-puntaje.</summary>
    public const int Min = 0;

    /// <summary>Límite superior permitido para cada sub-puntaje.</summary>
    public const int Max = 100;

    public int Experience { get; init; }

    public int Education { get; init; }

    public int Skills { get; init; }

    public int Certifications { get; init; }

    public int Contact { get; init; }

    /// <summary>Sub-puntaje neutro (5 secciones en cero). Útil como semilla
    /// de constructores desde el resultado legacy.</summary>
    public static PerSectionScore Zero { get; } = new();

    public PerSectionScore WithExperience(int value) => this with { Experience = Clamp(value) };

    public PerSectionScore WithEducation(int value) => this with { Education = Clamp(value) };

    public PerSectionScore WithSkills(int value) => this with { Skills = Clamp(value) };

    public PerSectionScore WithCertifications(int value) => this with { Certifications = Clamp(value) };

    public PerSectionScore WithContact(int value) => this with { Contact = Clamp(value) };

    private static int Clamp(int value) => Math.Clamp(value, Min, Max);
}
