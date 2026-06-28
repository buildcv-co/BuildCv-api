namespace BuildCv.Application.Features.LlmFeedback;

/// <summary>
/// Structured feedback contract returned by providers. It is separate from score contracts and cannot
/// mutate deterministic score fields (Constitution Art. II and VI).
/// </summary>
public sealed record LlmFeedbackResponse(
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Risks,
    IReadOnlyList<LlmFeedbackSuggestion> Suggestions,
    IReadOnlyList<string> MissingKeywords,
    IReadOnlyList<string> Questions,
    string Provider,
    string Model,
    DateTimeOffset GeneratedAt,
    bool Degraded);
