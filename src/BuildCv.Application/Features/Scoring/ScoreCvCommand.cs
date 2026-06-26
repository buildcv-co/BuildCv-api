using BuildCv.Application.Features.Jobs;
using BuildCv.Domain.Resumes;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Discriminador de versión del motor de puntaje (Constitution Art. II).
/// </summary>
public static class EngineVersions
{
    public const string V1 = "1.0.0";
    public const string V2 = "2.0.0";
}

/// <summary>
/// Comando de análisis de CV. El discriminador <c>EngineVersion</c> decide
/// qué rama de validación y de scoring se aplica. Mezclar
/// <c>StructuredScoreCommand</c> con <c>"1.0.0"</c> (o viceversa) es
/// VERSION_MISMATCH (Constitution Art. V).
/// </summary>
public abstract record ScoreCvCommand(string EngineVersion);

/// <summary>
/// Línea base (v1): CV y vacante como texto pegado por el usuario.
/// </summary>
public sealed record TextScoreCommand(string CvText, string JobText)
    : ScoreCvCommand(EngineVersions.V1);

/// <summary>
/// Línea v2: CV estructurado en formato JSON Resume y vacante como
/// <see cref="JobSpec"/>. La emisión y validación del documento se rigen
/// por Constitution Art. I (cero invención, confidence) y Art. V
/// (anti-injection).
/// </summary>
public sealed record StructuredScoreCommand(CvDocument Cv, JobSpec Job)
    : ScoreCvCommand(EngineVersions.V2);
