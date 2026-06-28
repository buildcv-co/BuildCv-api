namespace BuildCv.Application.Features.LlmFeedback;

public sealed class LlmFeedbackOptions
{
    public const string SectionName = "LlmFeedback";

    public bool Enabled { get; set; }

    public string Provider { get; set; } = "fake";

    public string Model { get; set; } = "fake-local-v1";

    public int TimeoutMs { get; set; } = 5000;

    public LlmFeedbackRateLimitOptions RateLimit { get; set; } = new();

    public bool RedactionEnabled { get; set; } = true;
}

public sealed class LlmFeedbackRateLimitOptions
{
    public int RequestsPerWindow { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}
