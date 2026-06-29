namespace BuildCv.Application.Features.LlmFeedback;

public sealed class LlmFeedbackOptions
{
    public const string SectionName = "LlmFeedback";

    public bool Enabled { get; set; }

    public string Provider { get; set; } = "fake";

    public string BaseUrl { get; set; } = "https://api.minimax.io/anthropic";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "MiniMax-M2.7";

    public int TimeoutMs { get; set; } = 5000;

    public int MaxInputLength { get; set; } = 32000;

    public int MaxOutputTokens { get; set; } = 1024;

    public LlmFeedbackRateLimitOptions RateLimit { get; set; } = new();

    public bool RedactionEnabled { get; set; } = true;
}

public sealed class LlmFeedbackRateLimitOptions
{
    public int RequestsPerWindow { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}
