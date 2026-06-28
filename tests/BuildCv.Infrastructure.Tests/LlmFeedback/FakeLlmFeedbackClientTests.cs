using BuildCv.Application.Features.Jobs;
using BuildCv.Application.Features.LlmFeedback;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using BuildCv.Infrastructure.LlmFeedback;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.LlmFeedback;

public sealed class FakeLlmFeedbackClientTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 27, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_ReturnsDeterministicV2FeedbackForSameInput()
    {
        var client = CreateClient();
        var context = CreateContext([".NET", "PostgreSQL", "Docker"]);

        var first = await client.GenerateAsync(context);
        var second = await client.GenerateAsync(context);

        first.Should().BeEquivalentTo(second);
        first.Summary.Should().Be("Fake local feedback for score 82 using engine 2.0.0.");
        first.Strengths.Should().Contain("Confirmed field: basics.name");
        first.MissingKeywords.Should().Contain("Docker");
        first.Suggestions.Should().Contain(suggestion =>
            suggestion.Category == "keywords" && suggestion.Text.Contains("Docker", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateAsync_MapsUserConfirmedMarkersToStrengthsAndInferredMarkersToRisks()
    {
        var client = CreateClient();
        var context = CreateContext([".NET"]);

        var response = await client.GenerateAsync(context);

        response.Strengths.Should().Contain("Confirmed field: basics.name");
        response.Strengths.Should().Contain("Strong signal: skills[0].name");
        response.Risks.Should().Contain("Tentative field: work[0].summary");
    }

    [Fact]
    public async Task GenerateAsync_AlwaysReturnsFakeMetadataAndNotDegraded()
    {
        var client = CreateClient();
        var response = await client.GenerateAsync(CreateContext([".NET"]));

        response.Provider.Should().Be("fake");
        response.Model.Should().Be("fake-local-v1");
        response.GeneratedAt.Should().Be(FixedNow);
        response.Degraded.Should().BeFalse();
    }

    private static FakeLlmFeedbackClient CreateClient() =>
        new(
            Options.Create(new LlmFeedbackOptions { Model = "fake-local-v1" }),
            new FixedClock(FixedNow));

    private static LlmFeedbackContext CreateContext(IReadOnlyList<string> requirements)
    {
        var cv = new CvDocument(
            new Basics(
                "Ada Lovelace",
                "ada@example.com",
                null,
                null,
                null,
                [],
                "Backend engineer with .NET APIs",
                null,
                new BasicsConfidence(
                    ConfidenceMarker.UserConfirmed,
                    ConfidenceMarker.Explicit,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Inferred,
                    ConfidenceMarker.Explicit,
                    ConfidenceMarker.Inferred)),
            [],
            [],
            [new TaggedResumeSkill(new ResumeSkillEntry(".NET", "Advanced"), new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred))],
            [],
            [],
            [],
            new CvMeta("2.0.0"));

        var request = new LlmFeedbackRequest(
            cv,
            new JobSpec("Backend Engineer", "BuildCv", "APIs", "Remote", EmploymentType.FullTime, requirements),
            null,
            null,
            new LlmFeedbackScoreContext(82, PerSectionScore.Zero.WithSkills(90), "2.0.0"),
            new Dictionary<string, ConfidenceMarker>
            {
                ["basics.name"] = ConfidenceMarker.UserConfirmed,
                ["skills[0].name"] = ConfidenceMarker.Explicit,
                ["work[0].summary"] = ConfidenceMarker.Inferred,
            },
            true);

        return new LlmFeedbackContext(request);
    }

    private sealed class FixedClock(DateTimeOffset now) : ILlmFeedbackClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
