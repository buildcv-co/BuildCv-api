using BuildCv.Domain.Jobs;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Orquesta el análisis determinista. Discrimina por tipo de comando:
/// <c>TextScoreCommand</c> (v1) usa los analizadores regex de texto;
/// <c>StructuredScoreCommand</c> (v2) consume el CV estructurado
/// directamente. La rama v2 se materializa en PR3 (ScoreV2).
/// </summary>
public sealed class ScoreCvHandler(
    IJobAnalyzer jobAnalyzer,
    ICvAnalyzer cvAnalyzer,
    IScoringEngine engine)
{
    public ScoreResult Handle(ScoreCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            TextScoreCommand text => ScoreV1(text),
            StructuredScoreCommand structured => ScoreV2(structured),
            _ => throw new InvalidOperationException(
                $"Tipo de comando desconocido: {command.GetType().FullName}"),
        };
    }

    private ScoreResult ScoreV1(TextScoreCommand command)
    {
        var job = jobAnalyzer.Analyze(command.JobText);
        var cv = cvAnalyzer.Analyze(command.CvText);
        return engine.Score(job, cv);
    }

    private static ScoreResult ScoreV2(StructuredScoreCommand command)
    {
        // PR3 materializa el motor v2. Por ahora la ruta está sellada para
        // no emitir un puntaje con un motor que aún no existe.
        throw new NotImplementedException(
            "ScoringEngine.ScoreV2 se implementa en PR3 (021 PR 3).");
    }
}
