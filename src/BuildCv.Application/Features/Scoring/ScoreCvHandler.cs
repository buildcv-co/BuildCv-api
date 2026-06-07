using BuildCv.Domain.Jobs;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Orquesta el análisis determinista: analiza la vacante y el CV y delega el puntaje al
/// motor de dominio. Sin LLM, sin IO (NFR-021).
/// </summary>
public sealed class ScoreCvHandler(
    IJobAnalyzer jobAnalyzer,
    ICvAnalyzer cvAnalyzer,
    IScoringEngine engine)
{
    public ScoreResult Handle(ScoreCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var job = jobAnalyzer.Analyze(command.JobText);
        var cv = cvAnalyzer.Analyze(command.CvText);
        return engine.Score(job, cv);
    }
}
