namespace BuildCv.Application.Features.LlmFeedback;

public class LlmFeedbackUnavailableException(string message) : Exception(message);

public sealed class LlmFeedbackValidationException(string message) : Exception(message);

public sealed class LlmFeedbackTimeoutException(string message) : Exception(message);

public sealed class LlmFeedbackRateLimitedException(TimeSpan? retryAfter) : Exception("Provider rate limit reached.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
