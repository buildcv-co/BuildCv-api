using BuildCv.Application.Features.Jobs;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;

namespace BuildCv.Application.Features.LlmFeedback;

/// <summary>
/// Immutable request boundary for optional LLM feedback. The deterministic score context is read-only
/// metadata and never changes the scoring engine output (Constitution Art. II).
/// </summary>
public sealed record LlmFeedbackRequest(
    CvDocument Cv,
    JobSpec Job,
    string? Provider,
    string? ProviderAccountId,
    LlmFeedbackScoreContext? ScoreContext,
    IReadOnlyDictionary<string, ConfidenceMarker> ConfidenceMarkers,
    bool? SessionToggleState);

/// <summary>
/// Read-only score snapshot passed to the feedback provider for explanation context.
/// </summary>
public sealed record LlmFeedbackScoreContext(int Score, PerSectionScore Components, string Version);
