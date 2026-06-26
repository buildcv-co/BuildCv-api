using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

    /// <summary>Versión del motor v2. Se sella en el contrato de respuesta
    /// (<see cref="ScoreResultV2.EngineVersion"/>). El bump del <see cref="Version"/>
    /// legacy se difiere a PR 3c; mientras conviven los dos en el binario.</summary>
    public const string VersionV2 = "2.0.0";

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

    private const double WExperience = 0.40;
    private const double WEducation = 0.20;
    private const double WSkills = 0.30;
    private const double WContact = 0.10;

    private const int GapThresholdMonths = 6;
    private const int JobHoppingWindowYears = 2;
    private const int JobHoppingMaxEmployers = 3;
    private const int SkillMismatchThresholdPct = 50;

    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    private static readonly Regex PhoneDigitsRegex = new(@"\d", RegexOptions.Compiled);

    private static readonly Regex TokenSplitRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);

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

    /// <summary>
    /// Motor de puntaje v2 (PR 3b): consume <see cref="CvDocument"/> y
    /// <see cref="JobInput"/> directamente, sin regex sobre texto pegado
    /// (Constitution Art. II FR-037). Función pura: sin IO, sin
    /// <c>DateTime.UtcNow</c>, sin <c>Guid.NewGuid</c>, sin <c>Random</c>;
    /// mismo input + misma versión ⇒ mismo score (verificado en PR 3d
    /// con 1000 ejecuciones paralelas).
    /// <para>
    /// Hard-gate por email ausente (FR-018): si <c>cv.Basics.Email</c> no
    /// pasa la regex, <c>overallScore = 0</c>, <c>band = "Bajo"</c>, y se
    /// emite red flag <c>MISSING_EMAIL</c>. Las demás secciones se siguen
    /// calculando para diagnóstico, pero no alimentan el overall.
    /// </para>
    /// <para>
    /// Overall = promedio ponderado 0.40·experience + 0.20·education +
    /// 0.30·skills + 0.10·contact; <c>certifications</c> viaja en
    /// <c>perSection</c> como señal informativa pero no entra al overall.
    /// Band: <c>Bajo</c> &lt;40, <c>Medio</c> 40–69, <c>Alto</c> ≥70.
    /// </para>
    /// </summary>
    public static ScoreResultV2 ScoreV2(CvDocument cv, JobInput job)
    {
        ArgumentNullException.ThrowIfNull(cv);
        ArgumentNullException.ThrowIfNull(job);

        var redFlags = new List<RedFlag>();

        if (!IsValidEmail(cv.Basics.Email))
        {
            redFlags.Add(new RedFlag(
                "MISSING_EMAIL",
                RedFlagSeverity.High,
                "El CV no tiene un correo electrónico válido; el puntaje se detiene en cero."));

            var legacyZero = BuildLegacyZero(cv, job, redFlags);
            return new ScoreResultV2
            {
                Legacy = legacyZero,
                PerSection = new PerSectionScore()
                    .WithExperience(0)
                    .WithEducation(0)
                    .WithSkills(0)
                    .WithCertifications(0)
                    .WithContact(0),
                RedFlags = redFlags,
            };
        }

        if (!IsValidPhone(cv.Basics.Phone))
        {
            redFlags.Add(new RedFlag(
                "MISSING_PHONE",
                RedFlagSeverity.Low,
                "No detectamos un teléfono válido; algunos reclutadores no podrán contactarte."));
        }

        var relevantJobs = CountRelevantJobs(cv.Work, job.Title);
        var experienceScore = relevantJobs switch
        {
            >= 3 => 95,
            2 => 80,
            1 => 60,
            _ => 30,
        };

        var educationScore = ComputeEducationScore(cv.Education, job.Requirements);

        var skillCoverage = ComputeSkillCoverage(cv.Skills, job.Requirements);
        var skillsScore = job.Requirements.Count == 0
            ? Math.Max(CountCertificates(cv.Certificates) * 25, 50)
            : (int)Math.Round(skillCoverage);

        if (job.Requirements.Count > 0 && skillCoverage < SkillMismatchThresholdPct)
        {
            redFlags.Add(new RedFlag(
                "SKILL_MISMATCH",
                RedFlagSeverity.Medium,
                $"Cubres menos del {SkillMismatchThresholdPct}% de los requisitos técnicos de la vacante."));
        }

        var certificationsScore = Math.Min(100, CountCertificates(cv.Certificates) * 50);

        var contactScore = ComputeContactScore(cv.Basics);

        var gap = DetectEmploymentGap(cv.Work);
        if (gap.HasValue)
        {
            redFlags.Add(new RedFlag(
                "EMPLOYMENT_GAP",
                RedFlagSeverity.Medium,
                $"Hay un vacío laboral de {gap.Value} meses entre dos experiencias consecutivas."));
        }

        if (DetectJobHopping(cv.Work))
        {
            redFlags.Add(new RedFlag(
                "JOB_HOPPING",
                RedFlagSeverity.Low,
                $"Más de {JobHoppingMaxEmployers} empleadores en los últimos {JobHoppingWindowYears} años del CV."));
        }

        var overall = (int)Math.Round(
            (experienceScore * WExperience)
            + (educationScore * WEducation)
            + (skillsScore * WSkills)
            + (contactScore * WContact));
        overall = Math.Clamp(overall, PerSectionScore.Min, PerSectionScore.Max);

        var band = ToV2Band(overall);

        var perSection = new PerSectionScore()
            .WithExperience(experienceScore)
            .WithEducation(educationScore)
            .WithSkills(skillsScore)
            .WithCertifications(certificationsScore)
            .WithContact(contactScore);

        var legacy = new ScoreResult(
            Overall: overall,
            Band: band,
            Disclaimer: DisclaimerText,
            Components: Array.Empty<ComponentScore>(),
            Keywords: new KeywordAnalysis(
                Array.Empty<KeywordView>(),
                Array.Empty<KeywordView>(),
                Array.Empty<KeywordView>()),
            Recommendations: Array.Empty<Recommendation>(),
            FormatIssues: Array.Empty<FormatIssue>(),
            GatesApplied: Array.Empty<GateApplied>(),
            EngineVersion: VersionV2,
            LexiconVersion: VersionV2,
            ContextHash: ComputeV2ContextHash(cv, job));

        return new ScoreResultV2
        {
            Legacy = legacy,
            PerSection = perSection,
            RedFlags = redFlags,
        };
    }

    private static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);

    private static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        return PhoneDigitsRegex.Matches(phone).Count >= 10;
    }

    private static int CountRelevantJobs(IReadOnlyList<TaggedResumeWork> work, string jobTitle)
    {
        if (work.Count == 0)
        {
            return 0;
        }

        var jobTokens = Tokenize(jobTitle);
        if (jobTokens.Count == 0)
        {
            return 0;
        }

        var hits = 0;
        foreach (var entry in work)
        {
            var entryTokens = Tokenize($"{entry.Entry.Position} {entry.Entry.Name}");
            if (jobTokens.Any(token => entryTokens.Contains(token)))
            {
                hits++;
            }
        }

        return hits;
    }

    private static int ComputeEducationScore(
        IReadOnlyList<TaggedResumeEducation> education,
        IReadOnlyList<string> requirements)
    {
        if (education.Count == 0)
        {
            return 0;
        }

        var requirementTokens = requirements
            .SelectMany(Tokenize)
            .ToHashSet(StringComparer.Ordinal);

        var hasOverlap = education.Any(entry =>
        {
            var entryTokens = Tokenize(
                $"{entry.Entry.Institution} {entry.Entry.Area} {entry.Entry.StudyType}");
            return entryTokens.Any(requirementTokens.Contains);
        });

        return hasOverlap ? 90 : 60;
    }

    private static double ComputeSkillCoverage(
        IReadOnlyList<TaggedResumeSkill> skills,
        IReadOnlyList<string> requirements)
    {
        if (requirements.Count == 0)
        {
            return 0;
        }

        var requirementTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in requirements)
        {
            foreach (var token in Tokenize(requirement))
            {
                requirementTokens.Add(token);
            }
        }

        if (requirementTokens.Count == 0)
        {
            return 0;
        }

        var skillTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            foreach (var token in Tokenize(skill.Entry.Name))
            {
                skillTokens.Add(token);
            }
        }

        var matched = 0;
        foreach (var token in requirementTokens)
        {
            if (skillTokens.Contains(token))
            {
                matched++;
            }
        }

        return 100.0 * matched / requirementTokens.Count;
    }

    private static int CountCertificates(IReadOnlyList<TaggedResumeCertificate> certificates)
        => certificates.Count;

    private static int ComputeContactScore(Basics basics)
    {
        var hasEmail = IsValidEmail(basics.Email);
        var hasPhone = IsValidPhone(basics.Phone);
        var hasUrl = !string.IsNullOrWhiteSpace(basics.Url);

        if (hasEmail && hasPhone && hasUrl)
        {
            return 100;
        }

        if (hasEmail && hasPhone)
        {
            return 70;
        }

        if (hasEmail)
        {
            return 40;
        }

        return 0;
    }

    /// <summary>
    /// Detecta el mayor vacío laboral en meses entre dos experiencias
    /// consecutivas. Devuelve <c>null</c> si no hay vacío mayor al umbral.
    /// Puro: solo compara <c>StartDate</c>/<c>EndDate</c> (formato
    /// <c>YYYY-MM</c>), sin reloj de pared.
    /// </summary>
    private static int? DetectEmploymentGap(IReadOnlyList<TaggedResumeWork> work)
    {
        if (work.Count < 2)
        {
            return null;
        }

        var dated = work
            .Where(entry => TryParseYearMonth(entry.Entry.StartDate, out _))
            .OrderBy(entry => entry.Entry.StartDate, StringComparer.Ordinal)
            .ToList();

        if (dated.Count < 2)
        {
            return null;
        }

        int? largestGap = null;
        for (var i = 1; i < dated.Count; i++)
        {
            var previous = dated[i - 1];
            var current = dated[i];
            if (!TryParseYearMonth(previous.Entry.EndDate, out var previousEnd))
            {
                continue;
            }

            if (!TryParseYearMonth(current.Entry.StartDate, out var currentStart))
            {
                continue;
            }

            var months = (currentStart.Year - previousEnd.Year) * 12
                + (currentStart.Month - previousEnd.Month);

            if (months > GapThresholdMonths)
            {
                largestGap = largestGap.HasValue ? Math.Max(largestGap.Value, months) : months;
            }
        }

        return largestGap;
    }

    /// <summary>
    /// Detecta el patrón de "job hopping": más de
    /// <see cref="JobHoppingMaxEmployers"/> empleadores distintos cuyos
    /// <c>StartDate</c> caen en la ventana de
    /// <see cref="JobHoppingWindowYears"/> años contados desde la fecha de
    /// inicio más reciente del CV. Puro: ancla en <c>max(StartDate)</c>,
    /// sin <c>DateTime.UtcNow</c>.
    /// </summary>
    private static bool DetectJobHopping(IReadOnlyList<TaggedResumeWork> work)
    {
        if (work.Count <= JobHoppingMaxEmployers)
        {
            return false;
        }

        var dated = work
            .Where(entry => TryParseYearMonth(entry.Entry.StartDate, out _))
            .ToList();

        if (dated.Count <= JobHoppingMaxEmployers)
        {
            return false;
        }

        var anchor = dated.Max(entry =>
            ParseYearMonth(entry.Entry.StartDate));

        var windowStart = anchor.AddYears(-JobHoppingWindowYears);
        var inWindow = dated.Count(entry => ParseYearMonth(entry.Entry.StartDate) >= windowStart);

        return inWindow > JobHoppingMaxEmployers;
    }

    private static ScoreBand ToV2Band(int overall) => overall switch
    {
        < 40 => ScoreBand.Bajo,
        < 70 => ScoreBand.Medio,
        _ => ScoreBand.Alto,
    };

    private static ScoreResult BuildLegacyZero(CvDocument cv, JobInput job, IReadOnlyList<RedFlag> redFlags) =>
        new(
            Overall: 0,
            Band: ScoreBand.Bajo,
            Disclaimer: DisclaimerText,
            Components: Array.Empty<ComponentScore>(),
            Keywords: new KeywordAnalysis(
                Array.Empty<KeywordView>(),
                Array.Empty<KeywordView>(),
                Array.Empty<KeywordView>()),
            Recommendations: Array.Empty<Recommendation>(),
            FormatIssues: Array.Empty<FormatIssue>(),
            GatesApplied: Array.Empty<GateApplied>(),
            EngineVersion: VersionV2,
            LexiconVersion: VersionV2,
            ContextHash: ComputeV2ContextHash(cv, job));

    private static string ComputeV2ContextHash(CvDocument cv, JobInput job)
    {
        var canonical = string.Join(
            "|",
            "v2",
            cv.Basics.Email,
            cv.Work.Count.ToString(CultureInfo.InvariantCulture),
            cv.Education.Count.ToString(CultureInfo.InvariantCulture),
            cv.Skills.Count.ToString(CultureInfo.InvariantCulture),
            cv.Certificates.Count.ToString(CultureInfo.InvariantCulture),
            job.Title,
            string.Join(",", job.Requirements.OrderBy(r => r, StringComparer.Ordinal)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    private static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var lower = text.ToLowerInvariant();
        var raw = TokenSplitRegex.Split(lower);
        var tokens = new List<string>(raw.Length);
        foreach (var token in raw)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static bool TryParseYearMonth(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParseExact(
            value,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    private static DateTime ParseYearMonth(string value)
        => DateTime.ParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None);
}
