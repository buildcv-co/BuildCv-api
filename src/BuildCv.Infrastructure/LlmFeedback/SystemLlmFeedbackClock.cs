using BuildCv.Application.Features.LlmFeedback;

namespace BuildCv.Infrastructure.LlmFeedback;

public sealed class SystemLlmFeedbackClock : ILlmFeedbackClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
