namespace BuildCv.Application.Features.LlmFeedback;

public sealed record GenerateLlmFeedbackResult(
    LlmFeedbackResponse? Response,
    string? ErrorCode,
    int StatusCode,
    string Detail)
{
    public static GenerateLlmFeedbackResult Success(LlmFeedbackResponse response) =>
        new(response, null, 200, string.Empty);

    public static GenerateLlmFeedbackResult Failure(string errorCode, int statusCode, string detail) =>
        new(null, errorCode, statusCode, detail);
}
