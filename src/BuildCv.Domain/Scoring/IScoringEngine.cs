using BuildCv.Domain.Jobs;
using BuildCv.Domain.Resumes;

namespace BuildCv.Domain.Scoring;

/// <summary>
/// Motor de puntaje determinista (Art. II): produce el número y su desglose explicable
/// a partir del análisis de la vacante y del CV. Sin IO, red, reloj ni aleatoriedad.
/// </summary>
public interface IScoringEngine
{
    ScoreResult Score(JobRequirementSet job, CvAnalysis cv);
}
