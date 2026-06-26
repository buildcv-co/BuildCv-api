namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Defensa en profundidad: el <see cref="ScoreCvValidator"/> ya rechaza
/// <c>engineVersion</c> desconocido antes de abrir el handler, pero el
/// handler revalida el discriminador para no depender exclusivamente de la
/// capa de validación (Constitution Art. V: defensa por capas).
/// </summary>
public sealed class UnsupportedScoreEngineVersionException : InvalidOperationException
{
    public string EngineVersion { get; }

    public UnsupportedScoreEngineVersionException(string engineVersion)
        : base($"Versión de motor no soportada: '{engineVersion}'. Versiones válidas: 1.0.0, 2.0.0.")
    {
        EngineVersion = engineVersion;
    }
}
