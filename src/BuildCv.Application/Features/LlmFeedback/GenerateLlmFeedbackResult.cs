namespace BuildCv.Application.Features.LlmFeedback;

public sealed record GenerateLlmFeedbackResult(
    LlmFeedbackResponse? Response,
    string? ErrorCode,
    int StatusCode,
    string Detail,
    TimeSpan? RetryAfter = null)
{
    public static GenerateLlmFeedbackResult Success(LlmFeedbackResponse response) =>
        new(response, null, 200, string.Empty);

    public static GenerateLlmFeedbackResult Failure(string errorCode, int statusCode, string detail) =>
        new(null, errorCode, statusCode, detail);

    public static GenerateLlmFeedbackResult RateLimited(TimeSpan? retryAfter) =>
        new(null, "rate_limited", 429, "Provider rate limit reached.", retryAfter);
}
