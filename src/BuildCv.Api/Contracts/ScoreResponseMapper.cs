using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Scoring;

namespace BuildCv.Api.Contracts;

/// <summary>Mapea el <see cref="ScoreResult"/> de dominio al DTO del contrato (enums → strings honestos).</summary>
public static class ScoreResponseMapper
{
    public static ScoreResponse Map(ScoreResult result) => new(
        result.Overall,
        BandName(result.Band),
        result.Disclaimer,
        result.EngineVersion,
        result.LexiconVersion,
        result.ContextHash,
        result.Components.Select(ToComponent).ToList(),
        new KeywordAnalysisResponse(
            result.Keywords.Present.Select(ToKeyword).ToList(),
            result.Keywords.Missing.Select(ToKeyword).ToList(),
            result.Keywords.Partial.Select(ToKeyword).ToList()),
        result.Recommendations.Select(ToRecommendation).ToList(),
        result.FormatIssues.Select(issue => new FormatIssueResponse(issue.Code, issue.Severity, issue.Message)).ToList(),
        result.GatesApplied.Select(gate => new GateResponse(ComponentName(gate.Component), gate.Cap, gate.Reason, gate.Message)).ToList());

    private static ComponentResponse ToComponent(ComponentScore component) => new(
        ComponentName(component.Id),
        Label(component.Id),
        (int)Math.Round(component.SubScore * 100, MidpointRounding.AwayFromZero),
        component.Weight,
        component.Measurability,
        ConfidenceName(component.Confidence),
        component.Summary);

    private static KeywordResponse ToKeyword(KeywordView keyword) => new(
        keyword.CanonicalTerm,
        CategoryName(keyword.Category),
        SectionName(keyword.Section),
        keyword.Weight,
        MatchLevelName(keyword.MatchLevel),
        LocationName(keyword.Location),
        keyword.Credit,
        keyword.Note);

    private static RecommendationResponse ToRecommendation(Recommendation recommendation) => new(
        recommendation.Action,
        TypeName(recommendation.Type),
        ComponentName(recommendation.Component),
        recommendation.EstimatedGain,
        recommendation.Invents,
        recommendation.HonestyNote);

    private static string BandName(ScoreBand band) => band switch
    {
        ScoreBand.Bajo => "Coincidencia baja",
        ScoreBand.Medio => "Coincidencia media",
        ScoreBand.Bueno => "Coincidencia alta",
        ScoreBand.Fuerte => "Coincidencia muy alta",
        _ => "Coincidencia media",
    };

    private static string ComponentName(ComponentId id) => id switch
    {
        ComponentId.Match => "match",
        ComponentId.Structure => "structure",
        ComponentId.Achievements => "achievements",
        ComponentId.Format => "format",
        ComponentId.Length => "length",
        _ => "match",
    };

    private static string Label(ComponentId id) => id switch
    {
        ComponentId.Match => "Coincidencia de keywords/skills",
        ComponentId.Structure => "Estructura parseable",
        ComponentId.Achievements => "Verbos de acción y logros cuantificados",
        ComponentId.Format => "Formato seguro",
        ComponentId.Length => "Longitud y densidad",
        _ => "Coincidencia",
    };

    private static string ConfidenceName(double confidence) => confidence switch
    {
        < 0.5 => "low",
        < 0.8 => "medium",
        _ => "high",
    };

    private static string CategoryName(SkillCategory category) => category switch
    {
        SkillCategory.HardSkill => "hardSkill",
        SkillCategory.Tool => "tool",
        SkillCategory.SoftSkill => "softSkill",
        _ => "generic",
    };

    private static string SectionName(RequirementSection section) => section switch
    {
        RequirementSection.MustHave => "requisitos",
        RequirementSection.NiceToHave => "deseables",
        RequirementSection.Responsibility => "responsabilidades",
        _ => "titulo",
    };

    private static string MatchLevelName(MatchTier tier) => tier switch
    {
        MatchTier.Exact => "exact",
        MatchTier.Alias => "alias",
        MatchTier.Lemma => "stem",
        MatchTier.Related => "related",
        MatchTier.Fuzzy => "fuzzy",
        _ => "none",
    };

    private static string LocationName(Placement placement) => placement switch
    {
        Placement.Prominent => "prominent",
        Placement.Buried => "buried",
        _ => "absent",
    };

    private static string TypeName(RecommendationType type) => type switch
    {
        RecommendationType.Surface => "resurface",
        RecommendationType.Rewrite => "rewrite",
        RecommendationType.AddMetric => "addMetric",
        RecommendationType.FixFormat => "fixFormat",
        RecommendationType.Learn => "learnAdd",
        _ => "rewrite",
    };
}
