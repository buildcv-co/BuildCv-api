using BuildCv.Domain.Jobs;

namespace BuildCv.Domain.Scoring;

/// <summary>Resuelve la mejor coincidencia de un requisito contra el CV (cascada de niveles).</summary>
public interface ISkillMatcher
{
    MatchResult Match(Requirement requirement, CvProfile cv);
}
