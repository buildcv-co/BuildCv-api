using BuildCv.Application.Features.Scoring;
using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Scoring;

namespace BuildCv.Api.Contracts;

/// <summary>Mapea el <see cref="ScoreResult"/> de dominio al DTO del contrato (enums → strings honestos).</summary>
public static class ScoreResponseMapper
{
    private const string StructuredInputGateV2 = "StructuredInputV2";

    private const string V2HonestyNotice =
        "Resultado generado a partir de CV estructurado (JSON Resume).";

    public static ScoreResponse Map(ScoreOutcome outcome) => outcome switch
    {
        V1ScoreOutcome v1 => Map(v1.Result),
        V2ScoreOutcome v2 => Map(v2.Result),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "ScoreOutcome desconocido."),
    };

    public static ScoreResponse Map(ScoreResult result) => MapCore(
        result.Overall,
        result.Band,
        result.Disclaimer,
        result.EngineVersion,
        result.LexiconVersion,
        result.ContextHash,
        result.Components,
        result.Keywords,
        result.Recommendations,
        result.FormatIssues,
        result.GatesApplied.Select(gate => new GateResponse(ComponentName(gate.Component), gate.Cap, gate.Reason, gate.Message)).ToList(),
        PerSection: null,
        RedFlags: null);

    public static ScoreResponse Map(ScoreResultV2 result) => MapCore(
        result.OverallScore,
        result.Legacy.Band,
        V2HonestyNotice,
        result.EngineVersion,
        result.Legacy.LexiconVersion,
        result.Legacy.ContextHash,
        result.Legacy.Components,
        result.Legacy.Keywords,
        result.Legacy.Recommendations,
        result.Legacy.FormatIssues,
        new[] { new GateResponse(StructuredInputGateV2, 1.0, "structured-input", V2HonestyNotice) },
        PerSection: new PerSectionResponse(
            Experience: result.PerSection.Experience,
            Education: result.PerSection.Education,
            Skills: result.PerSection.Skills,
            Certifications: result.PerSection.Certifications,
            Contact: result.PerSection.Contact),
        RedFlags: result.RedFlags.Select(flag => new RedFlagResponse(flag.Code, flag.Severity.ToString(), flag.Message)).ToList());

    private static ScoreResponse MapCore(
        int overallScore,
        ScoreBand band,
        string honestyNotice,
        string engineVersion,
        string lexiconVersion,
        string contextId,
        IReadOnlyList<ComponentScore> components,
        KeywordAnalysis keywords,
        IReadOnlyList<Recommendation> recommendations,
        IReadOnlyList<FormatIssue> formatIssues,
        IReadOnlyList<GateResponse> gatesApplied,
        PerSectionResponse? PerSection,
        IReadOnlyList<RedFlagResponse>? RedFlags) => new(
            overallScore,
            BandName(band),
            honestyNotice,
            engineVersion,
            lexiconVersion,
            contextId,
            components.Select(ToComponent).ToList(),
            new KeywordAnalysisResponse(
                keywords.Present.Select(ToKeyword).ToList(),
                keywords.Missing.Select(ToKeyword).ToList(),
                keywords.Partial.Select(ToKeyword).ToList()),
            recommendations.Select(ToRecommendation).ToList(),
            formatIssues.Select(issue => new FormatIssueResponse(issue.Code, issue.Severity, issue.Message)).ToList(),
            gatesApplied,
            PerSection,
            RedFlags);

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
        ScoreBand.Alto => "Coincidencia alta (motor 2.0.0)",
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
