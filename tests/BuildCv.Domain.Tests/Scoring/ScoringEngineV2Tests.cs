using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Scoring;

/// <summary>
/// RED→GREEN del motor de puntaje v2 (PR 3b de 021). Estos tests cubren los
/// escenarios del spec <c>score-section-breakdown</c>: per-section scoring,
/// red flags (gaps, job hopping, missing contact) y la hard-gate por email
/// ausente. Constitution Art. II: la función debe ser pura (sin
/// <c>DateTime.UtcNow</c>, sin <c>Guid.NewGuid</c>, sin IO); Art. VIII:
/// cobertura TDD antes de la implementación.
/// </summary>
public sealed class ScoringEngineV2Tests
{
    [Fact]
    public void ScoreV2_Missing_Email_Returns_Zero_With_MISSING_EMAIL_RedFlag()
    {
        var cv = BuildStrongCv() with { Basics = BuildStrongCv().Basics with { Email = string.Empty } };
        var job = BuildStrongJob();

        var result = ScoringEngine.ScoreV2(cv, job);

        result.OverallScore.Should().Be(0);
        result.Band.Should().Be("Bajo");
        result.RedFlags.Should().Contain(rf => rf.Code == "MISSING_EMAIL");
    }

    [Fact]
    public void ScoreV2_Experience_With_2_Relevant_Jobs_Scores_70_Plus()
    {
        var cv = BuildStrongCv();
        var job = BuildStrongJob();

        var result = ScoringEngine.ScoreV2(cv, job);

        result.PerSection.Experience.Should().BeGreaterThanOrEqualTo(70);
    }

    [Fact]
    public void ScoreV2_Skills_Cover_70_Percent_Of_JobSpec_Requirements_Scores_70_Plus()
    {
        var cv = BuildStrongCv();
        var job = BuildStrongJob();

        var result = ScoringEngine.ScoreV2(cv, job);

        result.PerSection.Skills.Should().BeGreaterThanOrEqualTo(70);
    }

    [Fact]
    public void ScoreV2_Employment_Gap_Greater_Than_6_Months_Adds_EMPLOYMENT_GAP_RedFlag()
    {
        var cv = BuildStrongCv() with
        {
            Work = new[]
            {
                TaggedWork("TechCorp", "Senior Backend Developer", "2020-01", "2022-03"),
                TaggedWork("StartupXYZ", "Backend Developer", "2023-05", "2024-12"),
            },
        };
        var job = BuildStrongJob();

        var result = ScoringEngine.ScoreV2(cv, job);

        result.RedFlags.Should().Contain(rf => rf.Code == "EMPLOYMENT_GAP");
    }

    [Fact]
    public void ScoreV2_More_Than_3_Jobs_In_2_Years_Adds_JOB_HOPPING_RedFlag()
    {
        var cv = BuildStrongCv() with
        {
            Work = new[]
            {
                TaggedWork("Co1", "Engineer", "2023-01", "2023-06"),
                TaggedWork("Co2", "Engineer", "2023-07", "2023-11"),
                TaggedWork("Co3", "Engineer", "2023-12", "2024-04"),
                TaggedWork("Co4", "Engineer", "2024-05", "2024-12"),
            },
        };
        var job = BuildStrongJob();

        var result = ScoringEngine.ScoreV2(cv, job);

        result.RedFlags.Should().Contain(rf => rf.Code == "JOB_HOPPING");
    }

    [Fact]
    public void ScoreV2_Higher_Education_Matches_JobSpec_Requirements_Scores_High()
    {
        var cv = BuildStrongCv();
        var job = new JobInput(
            Title: "Senior Backend Developer",
            Requirements: new[] { "C#", ".NET", "ingeniero de sistemas" });

        var result = ScoringEngine.ScoreV2(cv, job);

        result.PerSection.Education.Should().BeGreaterThanOrEqualTo(70);
    }

    [Fact]
    public void ScoreV2_No_Certificates_Still_Works_No_Exception()
    {
        var cv = BuildStrongCv();
        var job = BuildStrongJob();

        var act = () => ScoringEngine.ScoreV2(cv, job);

        act.Should().NotThrow();
    }

    [Fact]
    public void ScoreV2_Overall_Weighted_Average_Is_Between_Min_And_Max_Of_PerSection()
    {
        var cv = BuildStrongCv();
        var job = BuildStrongJob();

        var result = ScoringEngine.ScoreV2(cv, job);

        var perSection = new[] { 0, 100 }; // range only
        var maxScore = result.PerSection.Experience;
        var minScore = result.PerSection.Experience;
        foreach (var s in new[] { result.PerSection.Education, result.PerSection.Skills, result.PerSection.Contact, result.PerSection.Certifications })
        {
            if (s > maxScore)
            {
                maxScore = s;
            }

            if (s < minScore)
            {
                minScore = s;
            }
        }

        perSection[0] = minScore;
        perSection[1] = maxScore;
        result.OverallScore.Should().BeInRange(perSection[0], perSection[1]);
    }

    private static JobInput BuildStrongJob() => new(
        Title: "Senior Backend Developer",
        Requirements: new[] { "C#", ".NET", "SQL", "Docker" });

    private static CvDocument BuildStrongCv() => new(
        Basics: new Basics(
            Name: "Juan Pérez",
            Email: "juan.perez@example.com",
            Phone: "+57 300 123 4567",
            Location: "Bogotá, Colombia",
            Url: "https://linkedin.com/in/juanperez",
            Profiles: Array.Empty<ResumeProfile>(),
            Summary: "Backend developer with 5 years experience",
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
            TaggedWork("TechCorp", "Senior Backend Developer", "2020-01", "2024-12"),
            TaggedWork("StartupXYZ", "Backend Developer", "2018-03", "2019-12"),
        },
        Education: new[]
        {
            new TaggedResumeEducation(
                new ResumeEducationEntry(
                    Institution: "Universidad Nacional",
                    Area: "Ingeniería de Sistemas",
                    StudyType: "Pregrado",
                    StartDate: "2014-01",
                    EndDate: "2019-12",
                    Score: null),
                new EducationConfidence(
                    Institution: ConfidenceMarker.Explicit,
                    Area: ConfidenceMarker.Explicit,
                    StudyType: ConfidenceMarker.Explicit,
                    StartDate: ConfidenceMarker.Explicit,
                    EndDate: ConfidenceMarker.Explicit,
                    Score: ConfidenceMarker.Inferred)),
        },
        Skills: new[]
        {
            TaggedSkill("C#"),
            TaggedSkill(".NET"),
            TaggedSkill("SQL"),
            TaggedSkill("Docker"),
        },
        Projects: Array.Empty<TaggedResumeProject>(),
        Certificates: Array.Empty<TaggedResumeCertificate>(),
        Languages: Array.Empty<TaggedResumeLanguage>(),
        Meta: new CvMeta("2.0.0"));

    private static TaggedResumeWork TaggedWork(string name, string position, string startDate, string endDate) =>
        new(
            new ResumeWorkEntry(
                Name: name,
                Position: position,
                StartDate: startDate,
                EndDate: endDate,
                Summary: null,
                Highlights: null),
            new WorkConfidence(
                Name: ConfidenceMarker.Explicit,
                Position: ConfidenceMarker.Explicit,
                StartDate: ConfidenceMarker.Explicit,
                EndDate: ConfidenceMarker.Explicit,
                Summary: ConfidenceMarker.Inferred,
                Highlights: ConfidenceMarker.Inferred));

    private static TaggedResumeSkill TaggedSkill(string name) =>
        new(
            new ResumeSkillEntry(name, null),
            new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred));
}
