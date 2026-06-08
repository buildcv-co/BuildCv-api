using BuildCv.Api.Contracts;
using BuildCv.Api.Filters;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Adapt;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildCv.Api.Endpoints;

public static class AdaptEndpoints
{
    public const string AdaptPolicy = "ai";

    public static IEndpointRouteBuilder MapAdaptEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/adapt", async Task<IResult> (
            AdaptCvCommand command,
            AdaptCvHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(command, ct);

            if (result.IsFailure)
            {
                if (result.Error.Code == "AI_UNAVAILABLE")
                {
                    return Results.Problem(
                        detail: result.Error.Message,
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Adaptación no disponible");
                }
                return Results.Problem(
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Ok(AdaptResponseMapper.Map(result.Value));
        })
        .AddEndpointFilter<ValidationFilter<AdaptCvCommand>>()
        .RequireRateLimiting(AdaptPolicy)
        .WithName("AdaptCv")
        .WithSummary("Adapta el CV a la vacante usando LLM con cero invención (Constitution Art. I).")
        .WithDescription("Rate-limited 5/h por IP. Devuelve el CV adaptado y el reporte de validación post-IA.");

        return app;
    }
}
