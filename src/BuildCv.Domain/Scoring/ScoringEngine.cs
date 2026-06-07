using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Resumes;

namespace BuildCv.Domain.Scoring;

/// <summary>
/// Implementación pura del motor de puntaje (D01). Pesos: C1 45% · C2 20% · C3 20% ·
/// C4 10% · C5 5%. El componente de formato viaja con medibilidad 0.5 en v0 (texto
/// pegado) y la fórmula global renormaliza sobre el peso efectivamente medible.
/// </summary>
public sealed class ScoringEngine(ISkillMatcher matcher, ISkillGazetteer gazetteer) : IScoringEngine
{
    public const string Version = "1.0.0";

    private const string DisclaimerText =
        "Este puntaje mide coincidencia con esta vacante y legibilidad para sistemas automáticos. " +
        "No es un \"puntaje ATS oficial\" ni garantiza empleo.";

    private const double WMatch = 0.45;
    private const double WStructure = 0.20;
    private const double WAchievements = 0.20;
    private const double WFormat = 0.10;
    private const double WLength = 0.05;
    private const double FormatMeasurabilityV0 = 0.5;
    private const double FormatBaselineV0 = 0.75;

    public ScoreResult Score(JobRequirementSet job, CvAnalysis cv)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(cv);

        var matches = job.Requirements
            .Select(requirement => matcher.Match(requirement, cv.Profile))
            .ToList();

        var gates = new List<GateApplied>();

        var components = new List<ComponentScore>
        {
            new(ComponentId.Match, MatchScore(job, matches), WMatch, 1.0,
                job.Requirements.Count >= 4 ? 0.9 : 0.6, MatchSummary(matches)),
            new(ComponentId.Structure, StructureScore(cv, gates), WStructure, 1.0, 0.8, StructureSummary(cv)),
            new(ComponentId.Achievements, AchievementScore(cv), WAchievements, 1.0, 0.6, AchievementSummary(cv)),
            new(ComponentId.Format, FormatScore(gates), WFormat, FormatMeasurabilityV0, 0.3,
                "Evaluación parcial: con texto pegado no se observan columnas, tablas ni imágenes. Sube tu archivo para análisis completo."),
            new(ComponentId.Length, LengthScore(cv, gates), WLength, 1.0, 0.8, LengthSummary(cv)),
        };

        return new ScoreResult(
            ComputeOverall(components),
            ToBand(ComputeOverall(components)),
            DisclaimerText,
            components,
            BuildKeywordAnalysis(matches),
            BuildRecommendations(job, matches, cv),
            BuildFormatIssues(cv),
            gates,
            Version,
            gazetteer.Version,
            job.ContextHash);
    }

    private static int ComputeOverall(IReadOnlyList<ComponentScore> components)
    {
        double numerator = 0;
        double denominator = 0;
        foreach (var component in components)
        {
            numerator += component.Weight * component.Measurability * component.SubScore;
            denominator += component.Weight * component.Measurability;
        }

        var value = denominator <= 0 ? 0 : 100 * numerator / denominator;
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static double MatchScore(JobRequirementSet job, IReadOnlyList<MatchResult> matches)
    {
        var totalWeight = job.Requirements.Sum(requirement => requirement.Weight);
        if (totalWeight <= 0)
        {
            return 0;
        }

        var credited = matches.Sum(match => match.Requirement.Weight * match.Credit);
        return Math.Clamp(credited / totalWeight, 0, 1);
    }

    private static double StructureScore(CvAnalysis cv, List<GateApplied> gates)
    {
        var core = 0;
        if (cv.SectionsPresent.Contains("experience"))
        {
            core++;
        }

        if (cv.SectionsPresent.Contains("education"))
        {
            core++;
        }

        if (cv.SectionsPresent.Contains("skills"))
        {
            core++;
        }

        var score = core / 3.0;
        if (cv.HasContact)
        {
            score += 0.1;
        }

        if (cv.SectionsPresent.Contains("summary"))
        {
            score += 0.1;
        }

        score = Math.Min(1.0, score);

        if (!cv.HasContact)
        {
            score = Math.Min(score, 0.5);
            gates.Add(new GateApplied(ComponentId.Structure, 0.5, "no-contact",
                "No se detectaron datos de contacto; revisa que tu correo o teléfono sean legibles."));
        }

        if (!cv.HasExperience)
        {
            score = Math.Min(score, 0.4);
            gates.Add(new GateApplied(ComponentId.Structure, 0.4, "no-experience",
                "No se detectó una sección de experiencia."));
        }

        return score;
    }

    private static double AchievementScore(CvAnalysis cv)
    {
        var verbs = Math.Min(1.0, cv.ActionVerbCount / 5.0);
        var metrics = Math.Min(1.0, cv.QuantifiedAchievementCount / 3.0);
        return (0.5 * verbs) + (0.5 * metrics);
    }

    private static double FormatScore(List<GateApplied> gates)
    {
        gates.Add(new GateApplied(ComponentId.Format, 0.5, "partial-measurement",
            "Formato evaluado parcialmente por entrada de solo texto (v0)."));
        return FormatBaselineV0;
    }

    private static double LengthScore(CvAnalysis cv, List<GateApplied> gates)
    {
        var words = cv.WordCount;
        var band = words switch
        {
            < 150 => 0.4,
            < 250 => 0.7,
            <= 900 => 1.0,
            <= 1200 => 0.8,
            _ => 0.6,
        };

        var penalty = 0.0;
        if (cv.MaxSkillRepetition > 5)
        {
            penalty = Math.Min(0.5, (cv.MaxSkillRepetition - 5) * 0.1);
            gates.Add(new GateApplied(ComponentId.Length, 1 - penalty, "keyword-stuffing",
                "Una habilidad se repite en exceso; el relleno de keywords no mejora el puntaje."));
        }

        return Math.Clamp(band * (1 - penalty), 0, 1);
    }

    private static KeywordAnalysis BuildKeywordAnalysis(IReadOnlyList<MatchResult> matches)
    {
        var present = matches
            .Where(match => match.Tier is MatchTier.Exact or MatchTier.Alias)
            .OrderByDescending(match => match.Requirement.Weight)
            .ThenBy(match => match.Requirement.CanonicalId, StringComparer.Ordinal)
            .Select(ToView)
            .ToList();

        var partial = matches
            .Where(match => match.Tier is MatchTier.Related or MatchTier.Lemma or MatchTier.Fuzzy)
            .OrderByDescending(match => match.Requirement.Weight)
            .ThenBy(match => match.Requirement.CanonicalId, StringComparer.Ordinal)
            .Select(ToView)
            .ToList();

        var missing = matches
            .Where(match => match.Tier == MatchTier.None)
            .OrderByDescending(match => match.Requirement.Weight)
            .ThenBy(match => match.Requirement.CanonicalId, StringComparer.Ordinal)
            .Select(ToView)
            .ToList();

        return new KeywordAnalysis(present, missing, partial);
    }

    private static KeywordView ToView(MatchResult match) => new(
        match.Requirement.Display,
        match.Requirement.Category,
        match.Requirement.Section,
        match.Requirement.Weight,
        match.Tier,
        match.Placement,
        match.Credit,
        NoteFor(match));

    private static string NoteFor(MatchResult match) => match.Tier switch
    {
        MatchTier.None => "Requisito ausente en tu CV.",
        _ when match.Placement == Placement.Buried => "Presente pero poco visible.",
        _ => "Coincidencia directa.",
    };

    private List<Recommendation> BuildRecommendations(
        JobRequirementSet job,
        IReadOnlyList<MatchResult> matches,
        CvAnalysis cv)
    {
        var totalWeight = Math.Max(0.0001, job.Requirements.Sum(requirement => requirement.Weight));
        var recommendations = new List<Recommendation>();

        foreach (var match in matches.Where(m => m.Tier == MatchTier.None))
        {
            var gain = Math.Max(1, (int)Math.Round(match.Requirement.Weight / totalWeight * 100 * WMatch));
            recommendations.Add(new Recommendation(
                $"\"{match.Requirement.Display}\" es un requisito ausente. Apréndelo o añádelo solo si realmente lo cumples.",
                RecommendationType.Learn,
                ComponentId.Match,
                gain,
                Invents: false,
                "Brecha real: la adaptación no fabricará esta habilidad."));
        }

        foreach (var match in matches.Where(m => m.Placement == Placement.Buried && m.Tier is MatchTier.Exact or MatchTier.Alias))
        {
            var gain = Math.Max(1, (int)Math.Round(0.4 * match.Requirement.Weight / totalWeight * 100 * WMatch));
            recommendations.Add(new Recommendation(
                $"Sube \"{match.Requirement.Display}\" a tu sección de Habilidades; hoy está enterrada en el texto.",
                RecommendationType.Surface,
                ComponentId.Match,
                gain,
                Invents: false,
                "Solo reubica algo que ya está en tu CV."));
        }

        if (cv.QuantifiedAchievementCount < 2)
        {
            recommendations.Add(new Recommendation(
                "Añade métricas a tus logros (p. ej. \"reduje la latencia 30%\").",
                RecommendationType.AddMetric,
                ComponentId.Achievements,
                4,
                Invents: false,
                "Usa cifras reales de tu experiencia; no inventes."));
        }

        if (!cv.HasContact)
        {
            recommendations.Add(new Recommendation(
                "Añade datos de contacto legibles (correo y teléfono) cerca del encabezado.",
                RecommendationType.FixFormat,
                ComponentId.Structure,
                3,
                Invents: false,
                "Mejora la legibilidad para sistemas automáticos."));
        }

        return recommendations
            .OrderByDescending(recommendation => recommendation.EstimatedGain)
            .ThenBy(recommendation => recommendation.Action, StringComparer.Ordinal)
            .Take(8)
            .ToList();
    }

    private static List<FormatIssue> BuildFormatIssues(CvAnalysis cv)
    {
        var issues = new List<FormatIssue>();
        if (cv.QuantifiedAchievementCount < 2)
        {
            issues.Add(new FormatIssue("few-quantified-achievements", "info", "Pocos logros incluyen métricas cuantificadas."));
        }

        if (!cv.HasContact)
        {
            issues.Add(new FormatIssue("missing-contact", "warn", "No se detectaron datos de contacto."));
        }

        return issues;
    }

    private static ScoreBand ToBand(int overall) => overall switch
    {
        < 40 => ScoreBand.Bajo,
        < 65 => ScoreBand.Medio,
        < 85 => ScoreBand.Bueno,
        _ => ScoreBand.Fuerte,
    };

    private static string MatchSummary(IReadOnlyList<MatchResult> matches)
    {
        var covered = matches.Count(match => match.Tier is not MatchTier.None);
        return $"Cubres {covered} de {matches.Count} requisitos de la vacante.";
    }

    private static string StructureSummary(CvAnalysis cv)
    {
        var sections = cv.SectionsPresent.Count == 0
            ? "ninguna"
            : string.Join(", ", cv.SectionsPresent.OrderBy(section => section, StringComparer.Ordinal));
        return $"Secciones detectadas: {sections}.";
    }

    private static string AchievementSummary(CvAnalysis cv)
        => $"{cv.ActionVerbCount} verbos de acción y {cv.QuantifiedAchievementCount} logros con métricas.";

    private static string LengthSummary(CvAnalysis cv)
        => $"{cv.WordCount} palabras.";
}
