using BuildCv.Api.Filters;
using BuildCv.Application.Features.LlmFeedback;

namespace BuildCv.Api.Endpoints;

public static class LlmFeedbackEndpoint
{
    public static IEndpointRouteBuilder MapLlmFeedbackEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/llm/feedback", async Task<IResult> (
            LlmFeedbackRequest request,
            GenerateLlmFeedbackHandler handler,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var validation = Validate(request);
            if (validation is not null)
            {
                return validation;
            }

            var result = await handler.HandleAsync(request, ct);
            if (result.Response is not null)
            {
                return Results.Ok(result.Response);
            }

            if (result.RetryAfter is not null)
            {
                httpContext.Response.Headers.RetryAfter = Math.Ceiling(result.RetryAfter.Value.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Json(
                    new { error = result.ErrorCode, detail = result.Detail },
                    statusCode: result.StatusCode);
            }

            return Results.Json(
                new { error = result.ErrorCode, detail = result.Detail },
                statusCode: result.StatusCode);
        })
        .AddEndpointFilter<LlmFeedbackRateLimitFilter>()
        .WithName("GenerateLlmFeedback")
        .WithSummary("Genera feedback LLM opcional separado del puntaje determinista.")
        .WithDescription("Constitution Art. II: no calcula ni modifica el score. Art. III/V: redacción PII y entrada como DATA.");

        return app;
    }

    private static IResult? Validate(LlmFeedbackRequest? request)
    {
        if (request?.Cv is null || request.Job is null)
        {
            return Results.Json(
                new { error = "validation_error", detail = "cv and job are required." },
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }
}
