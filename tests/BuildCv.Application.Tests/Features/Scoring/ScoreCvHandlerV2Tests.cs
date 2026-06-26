using BuildCv.Application.Features.Jobs;
using BuildCv.Application.Features.Scoring;
using BuildCv.Domain.Jobs;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Scoring;

/// <summary>
/// RED→GREEN del dispatch v1/v2 en <see cref="ScoreCvHandler"/> (PR 3c de 021).
/// Constitution Art. II: el handler discrimina por <c>engineVersion</c>; la
/// rama v2 invoca <see cref="ScoringEngine.ScoreV2"/> con adaptadores puros
/// (<c>JobSpec → JobInput</c>) sin duplicar lógica. La rama v1 mantiene el
/// camino legacy intacto para no romper consumidores existentes.
/// </summary>
public sealed class ScoreCvHandlerV2Tests
{
    [Fact]
    public void Handle_StructuredCommand_With_EngineVersion_2_0_0_Calls_ScoreV2_And_Returns_ScoreResultV2()
    {
        var cv = BuildStrongCv();
        var job = BuildValidJobSpec();
        var sut = BuildHandler(out _, out _, out _);

        var outcome = sut.Handle(new StructuredScoreCommand(cv, job));

        outcome.Should().BeOfType<V2ScoreOutcome>();
        var v2 = ((V2ScoreOutcome)outcome).Result;
        v2.Should().NotBeNull();
        v2.EngineVersion.Should().Be("2.0.0");
        v2.PerSection.Should().NotBeNull();
        v2.RedFlags.Should().NotBeNull();
    }

    [Fact]
    public void Handle_StructuredCommand_With_EngineVersion_2_0_0_Maps_JobSpec_To_JobInput_Correctly()
    {
        var cv = BuildStrongCv();
        var job = BuildValidJobSpec() with
        {
            Title = "Senior Backend Developer",
            Requirements = new[] { "C#", ".NET", "PostgreSQL" },
        };
        var sut = BuildHandler(out _, out _, out _);

        var outcome = sut.Handle(new StructuredScoreCommand(cv, job));

        outcome.Should().BeOfType<V2ScoreOutcome>();
        var v2 = ((V2ScoreOutcome)outcome).Result;
        v2.PerSection.Skills.Should().BeGreaterThan(0);
        v2.RedFlags.Should().NotContain(rf => rf.Code == "MISSING_EMAIL");
    }

    [Fact]
    public void Handle_StructuredCommand_With_EngineVersion_2_0_0_Probes_ScoreV2_As_Pure_Function()
    {
        var cv = BuildStrongCv();
        var job = BuildValidJobSpec();
        var sut = BuildHandler(out _, out _, out _);

        var first = (V2ScoreOutcome)sut.Handle(new StructuredScoreCommand(cv, job));
        var second = (V2ScoreOutcome)sut.Handle(new StructuredScoreCommand(cv, job));

        first.Result.PerSection.Experience.Should().Be(second.Result.PerSection.Experience);
        first.Result.OverallScore.Should().Be(second.Result.OverallScore);
        first.Result.Legacy.ContextHash.Should().Be(second.Result.Legacy.ContextHash);
    }

    [Fact]
    public void Handle_LegacyCommand_With_EngineVersion_1_0_0_Uses_ScoreText_Path()
    {
        const string cvText = "Curriculum extenso de un desarrollador con experiencia en C# y .NET.";
        const string jobText = "Buscamos Ingeniero Backend Senior con C#, ASP.NET Core y SQL.";
        var sut = BuildHandler(out var scriptedEngine, out var jobAnalyzer, out var cvAnalyzer);

        var outcome = sut.Handle(new TextScoreCommand(cvText, jobText));

        outcome.Should().BeOfType<V1ScoreOutcome>();
        var v1 = ((V1ScoreOutcome)outcome).Result;
        v1.Overall.Should().Be(72);
        v1.EngineVersion.Should().Be("1.0.0");
        jobAnalyzer.LastJobText.Should().Be(jobText);
        cvAnalyzer.LastCvText.Should().Be(cvText);
        scriptedEngine.V1Calls.Should().Be(1);
    }

    [Fact]
    public void Handle_UnknownEngineVersion_Throws_UnsupportedScoreEngineVersion()
    {
        var sut = BuildHandler(out _, out _, out _);

        var act = () => sut.Handle(new TextScoreCommand("cv", "job") with { EngineVersion = "99.0.0" });

        act.Should().Throw<UnsupportedScoreEngineVersionException>()
            .Where(ex => ex.EngineVersion == "99.0.0");
    }

    private static ScoreCvHandler BuildHandler(
        out ScriptableScoringEngine scriptedEngine,
        out CapturingJobAnalyzer jobAnalyzer,
        out CapturingCvAnalyzer cvAnalyzer)
    {
        scriptedEngine = new ScriptableScoringEngine(overall: 72);
        jobAnalyzer = new CapturingJobAnalyzer();
        cvAnalyzer = new CapturingCvAnalyzer();
        return new ScoreCvHandler(jobAnalyzer, cvAnalyzer, scriptedEngine);
    }

    private static CvDocument BuildStrongCv() => new(
        Basics: new Basics(
            Name: "Ada Lovelace",
            Email: "ada@example.com",
            Phone: "+44 20 7946 0958",
            Location: "London, UK",
            Url: "https://linkedin.com/in/adalovelace",
            Profiles: Array.Empty<ResumeProfile>(),
            Summary: "Backend developer con 5 años de experiencia",
            DatosPersonales: null,
            Confidence: new BasicsConfidence(
                Name: ConfidenceMarker.Explicit,
                Email: ConfidenceMarker.Explicit,
                Phone: ConfidenceMarker.Explicit,
                Location: ConfidenceMarker.Inferred,
                Url: ConfidenceMarker.Inferred,
                Profiles: ConfidenceMarker.Inferred,
                Summary: ConfidenceMarker.Inferred,
                DatosPersonales: ConfidenceMarker.Inferred)),
        Work: new[]
        {
            new TaggedResumeWork(
                new ResumeWorkEntry(
                    Name: "Acme Corp",
                    Position: "Senior Backend Developer",
                    StartDate: "2020-01",
                    EndDate: "2024-12",
                    Summary: null,
                    Highlights: null),
                new WorkConfidence(
                    Name: ConfidenceMarker.Explicit,
                    Position: ConfidenceMarker.Explicit,
                    StartDate: ConfidenceMarker.Explicit,
                    EndDate: ConfidenceMarker.Explicit,
                    Summary: ConfidenceMarker.Inferred,
                    Highlights: ConfidenceMarker.Inferred)),
        },
        Education: Array.Empty<TaggedResumeEducation>(),
        Skills: new[]
        {
            new TaggedResumeSkill(
                new ResumeSkillEntry("C#", null),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
            new TaggedResumeSkill(
                new ResumeSkillEntry(".NET", null),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
            new TaggedResumeSkill(
                new ResumeSkillEntry("PostgreSQL", null),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
        },
        Projects: Array.Empty<TaggedResumeProject>(),
        Certificates: Array.Empty<TaggedResumeCertificate>(),
        Languages: Array.Empty<TaggedResumeLanguage>(),
        Meta: new CvMeta(EngineVersion: "2.0.0"));

    private static JobSpec BuildValidJobSpec() => new(
        Title: "Senior Backend Developer",
        Company: "Acme S.A.",
        Description: "Buscamos ingeniero backend con experiencia en .NET.",
        Location: "Bogotá, Colombia",
        EmploymentType: EmploymentType.FullTime,
        Requirements: new[] { "C#", ".NET", "PostgreSQL" });
}

internal sealed class ScriptableScoringEngine(int overall) : IScoringEngine
{
    public int V1Calls { get; private set; }
    public string? LastJobText { get; private set; }
    public string? LastCvText { get; private set; }

    public ScoreResult Score(JobRequirementSet job, CvAnalysis cv)
    {
        V1Calls++;
        return new ScoreResult(
            Overall: overall,
            Band: ScoreBand.Bueno,
            Disclaimer: "test disclaimer",
            Components: Array.Empty<ComponentScore>(),
            Keywords: new KeywordAnalysis(
                Array.Empty<KeywordView>(),
                Array.Empty<KeywordView>(),
                Array.Empty<KeywordView>()),
            Recommendations: Array.Empty<Recommendation>(),
            FormatIssues: Array.Empty<FormatIssue>(),
            GatesApplied: Array.Empty<GateApplied>(),
            EngineVersion: "1.0.0",
            LexiconVersion: "test-lex",
            ContextHash: "test-hash");
    }
}

internal sealed class CapturingJobAnalyzer : IJobAnalyzer
{
    public string? LastJobText { get; private set; }

    public JobRequirementSet Analyze(string jobText)
    {
        LastJobText = jobText;
        return new JobRequirementSet(Array.Empty<Requirement>(), ContextHash: "captured-hash");
    }
}

internal sealed class CapturingCvAnalyzer : ICvAnalyzer
{
    public string? LastCvText { get; private set; }

    public CvAnalysis Analyze(string cvText)
    {
        LastCvText = cvText;
        return new CvAnalysis(
            Profile: new CvProfile(
                SkillPlacements: new Dictionary<string, Placement>(),
                Tokens: new HashSet<string>(),
                Stems: new HashSet<string>()),
            SectionsPresent: new HashSet<string> { "experience" },
            HasContact: true,
            HasExperience: true,
            ActionVerbCount: 0,
            QuantifiedAchievementCount: 0,
            WordCount: cvText.Length,
            MaxSkillRepetition: 0);
    }
}
