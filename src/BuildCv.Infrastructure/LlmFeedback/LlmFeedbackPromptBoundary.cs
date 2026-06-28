namespace BuildCv.Infrastructure.LlmFeedback;

public static class LlmFeedbackPromptBoundary
{
    private static readonly string[] ForbiddenMarkers = ["tool_use", "function_call"];

    public static bool ContainsForbiddenToolDefinition(string payload) =>
        ForbiddenMarkers.Any(marker => payload.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
