namespace BuildCv.Application.Features.LlmFeedback;

public interface ILlmFeedbackClock
{
    DateTimeOffset UtcNow { get; }
}
