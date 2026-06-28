using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.LlmFeedback;

public sealed class GenerateLlmFeedbackHandler(
    ILlmFeedbackClient client,
    LlmFeedbackOptions options,
    ILlmFeedbackClock clock,
    ILogger<GenerateLlmFeedbackHandler> logger)
{
    public async Task<GenerateLlmFeedbackResult> HandleAsync(LlmFeedbackRequest request, CancellationToken ct)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var currentOptions = options;

        if (!currentOptions.Enabled || request.SessionToggleState == false)
        {
            return GenerateLlmFeedbackResult.Failure("disabled", 403, "LLM feedback is disabled.");
        }

        LlmFeedbackContext context;
        try
        {
            var cvText = JsonSerializer.Serialize(request.Cv);
            var jobText = JsonSerializer.Serialize(request.Job);
            context = new LlmFeedbackContext(
                request,
                currentOptions.RedactionEnabled ? PiiRedactor.Redact(cvText) : cvText,
                currentOptions.RedactionEnabled ? PiiRedactor.Redact(jobText) : jobText);
        }
        catch (LlmFeedbackRedactionException)
        {
            return GenerateLlmFeedbackResult.Failure("redaction_failure", 500, "Could not redact LLM feedback input.");
        }

        logger.LogInformation(
            "LlmFeedback request cvLength={CvLength} jobLength={JobLength} provider={Provider} model={Model} traceId={TraceId}",
            context.RedactedCvText.Length,
            context.RedactedJobText.Length,
            currentOptions.Provider,
            currentOptions.Model,
            traceId);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, currentOptions.TimeoutMs)));
            var response = await client.GenerateAsync(context, timeout.Token);
            return GenerateLlmFeedbackResult.Success(response);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Degraded("timeout", stopwatch.ElapsedMilliseconds, traceId, currentOptions);
        }
        catch (Exception)
        {
            return Degraded("provider_unavailable", stopwatch.ElapsedMilliseconds, traceId, currentOptions);
        }
    }

    private GenerateLlmFeedbackResult Degraded(string reason, long latencyMs, string traceId, LlmFeedbackOptions currentOptions)
    {
        logger.LogWarning(
            "LlmFeedback degraded reason={Reason} latencyMs={LatencyMs} traceId={TraceId}",
            reason,
            latencyMs,
            traceId);

        return GenerateLlmFeedbackResult.Success(new LlmFeedbackResponse(
            "AI feedback no disponible",
            [],
            [],
            [],
            [],
            [],
            "fake",
            currentOptions.Model,
            clock.UtcNow,
            true));
    }
}
