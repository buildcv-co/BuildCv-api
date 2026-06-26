using BuildCv.Domain.Scoring;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Scoring;

public sealed class ScoreResultV2Tests
{
    [Fact]
    public void PerSectionScore_Defaults_To_All_Zero_When_Constructed()
    {
        var perSection = new PerSectionScore();

        perSection.Experience.Should().Be(0);
        perSection.Education.Should().Be(0);
        perSection.Skills.Should().Be(0);
        perSection.Certifications.Should().Be(0);
        perSection.Contact.Should().Be(0);
    }

    [Fact]
    public void RedFlag_Requires_Severity_Code_And_Message()
    {
        var actCode = () => new RedFlag(string.Empty, RedFlagSeverity.Low, "msg");
        var actMessage = () => new RedFlag("CODE", RedFlagSeverity.Low, "   ");

        actCode.Should().Throw<ArgumentException>();
        actMessage.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RedFlag_Severity_Accepts_Only_Low_Medium_High()
    {
        var accepted = () => Enum.Parse<RedFlagSeverity>("High");
        var rejected = () => Enum.Parse<RedFlagSeverity>("Critical");

        accepted.Should().NotThrow();
        rejected.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ScoreResultV2_Exposes_PerSection_And_RedFlags()
    {
        var legacy = CreateLegacy();
        var v2 = new ScoreResultV2
        {
            Legacy = legacy,
            PerSection = PerSectionScore.Zero.WithExperience(80).WithSkills(70),
            RedFlags = new[] { new RedFlag("GAP", RedFlagSeverity.Medium, "14 month gap") },
        };

        v2.PerSection.Experience.Should().Be(80);
        v2.PerSection.Skills.Should().Be(70);
        v2.RedFlags.Should().HaveCount(1);
        v2.RedFlags[0].Code.Should().Be("GAP");
    }

    [Fact]
    public void ScoreResultV2_EngineVersion_Is_2_0_0()
    {
        var legacy = CreateLegacy();
        var v2 = ScoreResultV2.FromLegacy(legacy);

        v2.EngineVersion.Should().Be("2.0.0");
        ScoreResultV2.CurrentEngineVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void ScoreResultV2_Can_Be_Constructed_From_Legacy_ScoreResult()
    {
        var legacy = CreateLegacy();
        var v2 = ScoreResultV2.FromLegacy(legacy);

        v2.Legacy.Should().BeSameAs(legacy);
        v2.OverallScore.Should().Be(legacy.Overall);
        v2.Band.Should().Be(legacy.Band.ToString());
        v2.PerSection.Should().Be(PerSectionScore.Zero);
        v2.RedFlags.Should().BeEmpty();
    }

    private static ScoreResult CreateLegacy() => new(
        Overall: 72,
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
        ContextHash: "ctx-hash");
}
