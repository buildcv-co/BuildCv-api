using BuildCv.Application.Features.Jobs;
using BuildCv.Application.Features.LlmFeedback;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.LlmFeedback;

public sealed class LlmFeedbackContractsTests
{
    [Fact]
    public void LlmFeedbackRequest_BindsStructuredCvJobScoreContextAndMarkers()
    {
        var cv = CreateCv(ConfidenceMarker.UserConfirmed, ConfidenceMarker.Explicit);
        var job = new JobSpec(
            "Backend Engineer",
            "BuildCv",
            "Build APIs with .NET and PostgreSQL",
            "Remote",
            EmploymentType.FullTime,
            [".NET", "PostgreSQL"]);
        var scoreContext = new LlmFeedbackScoreContext(
            82,
            new PerSectionScore().WithSkills(90).WithExperience(70),
            "2.0.0");
        var markers = new Dictionary<string, ConfidenceMarker>
        {
            ["basics.name"] = ConfidenceMarker.UserConfirmed,
            ["skills[0].name"] = ConfidenceMarker.Explicit,
        };

        var request = new LlmFeedbackRequest(
            cv,
            job,
            "web",
            "local-user",
            scoreContext,
            markers,
            true);

        request.Cv.Should().BeSameAs(cv);
        request.Job.Should().BeSameAs(job);
        request.Provider.Should().Be("web");
        request.ProviderAccountId.Should().Be("local-user");
        request.ScoreContext.Should().Be(scoreContext);
        request.ConfidenceMarkers.Should().ContainKey("basics.name")
            .WhoseValue.Should().Be(ConfidenceMarker.UserConfirmed);
        request.SessionToggleState.Should().BeTrue();
    }

    [Fact]
    public void LlmFeedbackResponse_BindsTenFieldContractWithProviderMetadata()
    {
        var generatedAt = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
        var suggestion = new LlmFeedbackSuggestion("skills", "Mention PostgreSQL impact.", LlmFeedbackSeverity.Medium);
        var metadata = new LlmFeedbackProviderMetadata("fake", "fake-local-v1", generatedAt, false);

        var response = new LlmFeedbackResponse(
            "Good match for the role.",
            ["Confirmed .NET experience"],
            ["PostgreSQL is inferred only"],
            [suggestion],
            ["PostgreSQL"],
            [],
            metadata.Provider,
            metadata.Model,
            metadata.GeneratedAt,
            metadata.Degraded);

        response.Summary.Should().Be("Good match for the role.");
        response.Strengths.Should().ContainSingle().Which.Should().Be("Confirmed .NET experience");
        response.Risks.Should().ContainSingle().Which.Should().Be("PostgreSQL is inferred only");
        response.Suggestions.Should().ContainSingle().Which.Should().Be(suggestion);
        response.MissingKeywords.Should().ContainSingle().Which.Should().Be("PostgreSQL");
        response.Questions.Should().BeEmpty();
        response.Provider.Should().Be("fake");
        response.Model.Should().Be("fake-local-v1");
        response.GeneratedAt.Should().Be(generatedAt);
        response.Degraded.Should().BeFalse();
    }

    [Fact]
    public async Task ILlmFeedbackClient_GenerateAsync_ReceivesContextAndCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var context = new LlmFeedbackContext(
            new LlmFeedbackRequest(
                CreateCv(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred),
                new JobSpec("Backend Engineer", "BuildCv", ".NET APIs", "Remote", EmploymentType.FullTime, [".NET"]),
                null,
                null,
                null,
                new Dictionary<string, ConfidenceMarker>(),
                true));
        ILlmFeedbackClient client = new CapturingLlmFeedbackClient();

        var response = await client.GenerateAsync(context, cts.Token);

        response.Provider.Should().Be("fake");
        ((CapturingLlmFeedbackClient)client).CapturedContext.Should().BeSameAs(context);
        ((CapturingLlmFeedbackClient)client).CapturedCancellationToken.Should().Be(cts.Token);
    }

    private static CvDocument CreateCv(ConfidenceMarker nameConfidence, ConfidenceMarker skillConfidence) =>
        new(
            new Basics(
                "Ada Lovelace",
                "ada@example.com",
                "+57 300 123 4567",
                "Bogotá",
                "https://example.com",
                [],
                "Backend engineer",
                null,
                new BasicsConfidence(
                    nameConfidence,
                    ConfidenceMarker.Explicit,
                    ConfidenceMarker.Explicit,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Explicit,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Inferred)),
            [],
            [],
            [new TaggedResumeSkill(new ResumeSkillEntry(".NET", "Advanced"), new SkillConfidence(skillConfidence, ConfidenceMarker.Inferred))],
            [],
            [],
            [],
            new CvMeta("2.0.0"));

    private sealed class CapturingLlmFeedbackClient : ILlmFeedbackClient
    {
        public LlmFeedbackContext? CapturedContext { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default)
        {
            CapturedContext = context;
            CapturedCancellationToken = ct;
            return Task.FromResult(new LlmFeedbackResponse(
                "Captured",
                [],
                [],
                [],
                [],
                [],
                "fake",
                "fake-local-v1",
                DateTimeOffset.UnixEpoch,
                false));
        }
    }
}
