using BuildCv.Application.Features.Jobs;
using BuildCv.Domain.Jobs;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Orquesta el análisis determinista y discrimina por <c>engineVersion</c>:
/// v1 (1.0.0) usa <see cref="IJobAnalyzer"/> + <see cref="ICvAnalyzer"/> +
/// <see cref="IScoringEngine.Score"/> sobre texto pegado (camino legacy,
/// intacto); v2 (2.0.0) invoca <see cref="ScoringEngine.ScoreV2"/>
/// directamente sobre el <see cref="CvDocument"/> y el
/// <see cref="BuildCv.Application.Features.Jobs.JobSpec"/> adaptados al
/// <see cref="JobInput"/> mínimo del Domain. Cualquier <c>engineVersion</c>
/// fuera del enum sellado lanza <see cref="UnsupportedScoreEngineVersionException"/>
/// (defensa en profundidad, Constitution Art. V).
/// </summary>
public sealed class ScoreCvHandler(
    IJobAnalyzer jobAnalyzer,
    ICvAnalyzer cvAnalyzer,
    IScoringEngine engine)
{
    public ScoreOutcome Handle(ScoreCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.EngineVersion switch
        {
            EngineVersions.V1 => command is TextScoreCommand text
                ? new V1ScoreOutcome(ScoreV1(text))
                : throw new UnsupportedScoreEngineVersionException(command.EngineVersion),
            EngineVersions.V2 => command is StructuredScoreCommand structured
                ? new V2ScoreOutcome(ScoreV2(structured))
                : throw new UnsupportedScoreEngineVersionException(command.EngineVersion),
            _ => throw new UnsupportedScoreEngineVersionException(command.EngineVersion),
        };
    }

    private ScoreResult ScoreV1(TextScoreCommand command)
    {
        var job = jobAnalyzer.Analyze(command.JobText);
        var cv = cvAnalyzer.Analyze(command.CvText);
        return engine.Score(job, cv);
    }

    private static ScoreResultV2 ScoreV2(StructuredScoreCommand command)
    {
        var jobInput = JobSpecAdapter.ToJobInput(command.Job);
        return ScoringEngine.ScoreV2(command.Cv, jobInput);
    }
}
