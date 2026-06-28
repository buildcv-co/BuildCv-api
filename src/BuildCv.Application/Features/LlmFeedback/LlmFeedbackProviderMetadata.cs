namespace BuildCv.Application.Features.LlmFeedback;

/// <summary>
/// Provider metadata safe to expose in API responses and logs.
/// </summary>
public sealed record LlmFeedbackProviderMetadata(
    string Provider,
    string Model,
    DateTimeOffset GeneratedAt,
    bool Degraded);
