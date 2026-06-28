namespace BuildCv.Application.Features.LlmFeedback;

/// <summary>
/// Carrier object for provider calls. Later PRs add redacted payload and trace metadata here.
/// </summary>
public sealed record LlmFeedbackContext(LlmFeedbackRequest Request);
