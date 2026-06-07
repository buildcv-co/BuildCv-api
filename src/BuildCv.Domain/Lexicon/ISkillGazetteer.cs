using System.Diagnostics.CodeAnalysis;

namespace BuildCv.Domain.Lexicon;

/// <summary>
/// Diccionario de habilidades cargado como datos inmutables (NO es tabla; se sella en
/// <see cref="Version"/> dentro de cada resultado para reproducibilidad — FR-013).
/// </summary>
public interface ISkillGazetteer
{
    /// <summary>Versión del recurso (p. ej. "2026.06.0"), sellada en el resultado.</summary>
    string Version { get; }

    /// <summary>Resuelve un token YA normalizado contra el término canónico o sus alias.</summary>
    bool TryResolve(string normalizedToken, [MaybeNullWhen(false)] out SkillEntry entry);

    /// <summary>Obtiene una entrada por su id canónico.</summary>
    bool TryGetById(string canonicalId, [MaybeNullWhen(false)] out SkillEntry entry);

    /// <summary>IDs canónicos relacionados (crédito parcial de la cascada).</summary>
    IReadOnlyList<string> Related(string canonicalId);

    /// <summary>IDs canónicos que el término implica (p. ej. ASP.NET Core ⇒ .NET).</summary>
    IReadOnlyList<string> Implies(string canonicalId);

    /// <summary>True si dos términos están marcados como confundibles (no deben hacer match difuso).</summary>
    bool AreConfusable(string a, string b);
}
