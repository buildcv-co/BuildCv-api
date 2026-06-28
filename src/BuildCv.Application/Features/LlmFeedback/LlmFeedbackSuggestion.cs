namespace BuildCv.Application.Features.LlmFeedback;

public enum LlmFeedbackSeverity
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Actionable suggestion emitted by LLM feedback without inventing candidate facts.
/// </summary>
public sealed record LlmFeedbackSuggestion(string Category, string Text, LlmFeedbackSeverity Severity);
