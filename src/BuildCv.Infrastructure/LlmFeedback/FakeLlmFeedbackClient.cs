using BuildCv.Application.Features.LlmFeedback;
using BuildCv.Domain.Resumes;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.LlmFeedback;

public sealed class FakeLlmFeedbackClient : ILlmFeedbackClient
{
    private readonly LlmFeedbackOptions options;
    private readonly ILlmFeedbackClock clock;

    public FakeLlmFeedbackClient(IOptions<LlmFeedbackOptions> options, ILlmFeedbackClock clock)
    {
        this.options = options.Value;
        this.clock = clock;
    }

    public Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = BuildResponse(context.Request);

        return Task.FromResult(response);
    }

    private LlmFeedbackResponse BuildResponse(LlmFeedbackRequest request)
    {
        var strengths = BuildStrengths(request.ConfidenceMarkers);
        var risks = BuildRisks(request.ConfidenceMarkers);
        var missingKeywords = FindMissingKeywords(request);
        var suggestions = missingKeywords
            .Select(keyword => new LlmFeedbackSuggestion(
                "keywords",
                $"If accurate, add evidence for '{keyword}' using facts already present in the CV.",
                LlmFeedbackSeverity.Medium))
            .ToArray();

        return new LlmFeedbackResponse(
            BuildSummary(request),
            strengths,
            risks,
            suggestions,
            missingKeywords,
            [],
            "fake",
            options.Model,
            clock.UtcNow,
            false);
    }

    private static string BuildSummary(LlmFeedbackRequest request)
    {
        var score = request.ScoreContext?.Score ?? 0;
        var version = request.ScoreContext?.Version ?? request.Cv.Meta.EngineVersion;

        return $"Fake local feedback for score {score} using engine {version}.";
    }

    private static string[] BuildStrengths(IReadOnlyDictionary<string, ConfidenceMarker> markers) =>
        markers
            .Where(pair => pair.Value is ConfidenceMarker.UserConfirmed or ConfidenceMarker.Explicit)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value == ConfidenceMarker.UserConfirmed
                ? $"Confirmed field: {pair.Key}"
                : $"Strong signal: {pair.Key}")
            .ToArray();

    private static string[] BuildRisks(IReadOnlyDictionary<string, ConfidenceMarker> markers) =>
        markers
            .Where(pair => pair.Value == ConfidenceMarker.Inferred)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"Tentative field: {pair.Key}")
            .ToArray();

    private static string[] FindMissingKeywords(LlmFeedbackRequest request)
    {
        var cvTokens = BuildCvSearchText(request.Cv);

        return request.Job.Requirements
            .Where(requirement => !cvTokens.Contains(requirement, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildCvSearchText(CvDocument cv)
    {
        var parts = new List<string>
        {
            cv.Basics.Name,
            cv.Basics.Summary ?? string.Empty,
        };
        parts.AddRange(cv.Skills.Select(skill => skill.Entry.Name));
        parts.AddRange(cv.Work.Select(work => work.Entry.Summary ?? string.Empty));
        parts.AddRange(cv.Work.SelectMany(work => work.Entry.Highlights ?? []));

        return string.Join(' ', parts);
    }
}
