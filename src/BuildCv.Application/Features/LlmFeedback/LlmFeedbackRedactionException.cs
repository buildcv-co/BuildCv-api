namespace BuildCv.Application.Features.LlmFeedback;

public sealed class LlmFeedbackRedactionException : Exception
{
    public LlmFeedbackRedactionException()
        : base("LLM feedback redaction failed before provider boundary.")
    {
    }
}
