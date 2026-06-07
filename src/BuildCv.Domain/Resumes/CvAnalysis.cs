using BuildCv.Domain.Scoring;

namespace BuildCv.Domain.Resumes;

/// <summary>
/// Resultado del análisis determinista del CV. Incluye el <see cref="Profile"/> que
/// consume el matcher y las señales para los componentes de estructura (C2), logros (C3)
/// y longitud/densidad (C5) del motor de puntaje.
/// </summary>
public sealed record CvAnalysis(
    CvProfile Profile,
    IReadOnlySet<string> SectionsPresent,
    bool HasContact,
    bool HasExperience,
    int ActionVerbCount,
    int QuantifiedAchievementCount,
    int WordCount,
    int MaxSkillRepetition);
