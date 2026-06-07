using BuildCv.Api.Contracts;
using BuildCv.Api.Filters;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Scoring;

namespace BuildCv.Api.Endpoints;

public static class ScoringEndpoints
{
    /// <summary>
    /// <c>POST /api/v1/score</c> — análisis determinista (sin auth, rate-limited).
    /// </summary>
    public static IEndpointRouteBuilder MapScoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/score", (ScoreCvCommand command, ScoreCvHandler handler) =>
            {
                var result = handler.Handle(command);
                return Results.Ok(ScoreResponseMapper.Map(result));
            })
            .AddEndpointFilter<ValidationFilter<ScoreCvCommand>>()
            .RequireRateLimiting(RateLimiting.ScorePolicy)
            .WithName("ScoreCv")
            .WithSummary("Calcula el puntaje de coincidencia y legibilidad de un CV frente a una vacante.");

        return app;
    }
}
