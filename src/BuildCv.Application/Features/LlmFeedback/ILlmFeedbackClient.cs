namespace BuildCv.Application.Features.LlmFeedback;

public interface ILlmFeedbackClient
{
    Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default);
}
