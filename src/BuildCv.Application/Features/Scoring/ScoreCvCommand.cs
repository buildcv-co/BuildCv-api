namespace BuildCv.Application.Features.Scoring;

/// <summary>Entrada del análisis: el CV y la vacante pegados por el usuario.</summary>
public sealed record ScoreCvCommand(string CvText, string JobText);
