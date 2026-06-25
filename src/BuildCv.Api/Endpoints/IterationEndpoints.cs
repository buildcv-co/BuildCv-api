using System.Security.Claims;
using BuildCv.Api.Contracts;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Iterations;

namespace BuildCv.Api.Endpoints;

public static class IterationEndpoints
{
    public static IEndpointRouteBuilder MapIterationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/adapt/iterate")
            .RequireAuthorization()
            .WithTags("Iterations");

        group.MapPost("/", IterateHandler)
            .RequireRateLimiting(RateLimiting.IteratePolicy)
            .WithName("StartIteration")
            .WithSummary("Inicia una iteración best-of-N: adapta el CV hasta N veces y devuelve el mejor resultado (Constitution Art. I + IV).")
            .Produces<IterationResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status402PaymentRequired)
            .Produces(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{requestId:guid}", GetIterationHandler)
            .WithName("GetIteration")
            .WithSummary("Devuelve el resultado cacheado de una iteración (TTL 24h).")
            .Produces<IterationResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> IterateHandler(
        ClaimsPrincipal user,
        IterateRequestDto body,
        IIterationService service,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Json(
                new { error = "AUTH/UNAUTHENTICATED" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var iterationCount = body.IterationCount ?? 5;
        var threshold = body.ProbabilityThreshold ?? 50;

        try
        {
            var result = await service.RunAsync(
                userId.Value,
                body.CvText,
                body.VacancyText,
                iterationCount,
                threshold,
                ct);
            return Results.Ok(IterationResultMapper.Map(result));
        }
        catch (ArgumentException ex)
        {
            return Results.Json(
                new { error = "VALIDATION/INVALID_INPUT", message = ex.Message },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InsufficientCreditsException)
        {
            return Results.Json(
                new { error = "CREDIT/INSUFFICIENT", message = "No tenés créditos suficientes para esta iteración." },
                statusCode: StatusCodes.Status402PaymentRequired);
        }
    }

    private static async Task<IResult> GetIterationHandler(
        Guid requestId,
        ClaimsPrincipal user,
        IIterationService service,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Json(
                new { error = "AUTH/UNAUTHENTICATED" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await service.GetAsync(requestId, ct);
        return result is null
            ? Results.Json(
                new { error = "ITERATION/NOT_FOUND", message = "La iteración solicitada no existe o ya expiró (TTL 24h)." },
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(IterationResultMapper.Map(result));
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }
}
