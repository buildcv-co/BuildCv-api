using System.Security.Claims;
using BuildCv.Api.Contracts;
using BuildCv.Api.Filters;
using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Credits;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildCv.Api.Endpoints;

public static class AdaptEndpoints
{
    public const string AdaptPolicy = "ai";

    private static Guid DeriveDeterministicGuid(string key)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"adapt:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static IEndpointRouteBuilder MapAdaptEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/adapt", async Task<IResult> (
            AdaptCvCommand command,
            AdaptCvHandler handler,
            ConsumeForAdaptHandler consumeHandler,
            RefundConsumptionHandler refundHandler,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
            {
                return Results.Unauthorized();
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
            var adaptRequestId = string.IsNullOrWhiteSpace(idempotencyKey)
                ? Guid.NewGuid()
                : DeriveDeterministicGuid(idempotencyKey);
            var consume = await consumeHandler.HandleAsync(
                new ConsumeForAdaptCommand { UserId = parsedUserId, AdaptRequestId = adaptRequestId },
                ct);

            if (!consume.Success)
            {
                httpContext.Response.Headers["X-Credit-Balance"] = consume.BalanceAfter.ToString();
                httpContext.Response.Headers["Retry-After"] = "0";
                return Results.Json(
                    new
                    {
                        type = "https://buildcv.com/errors/credit-insufficient",
                        title = "INSUFFICIENT_CREDITS",
                        status = StatusCodes.Status402PaymentRequired,
                        code = consume.ErrorCode,
                        balance = consume.BalanceAfter,
                        required = 1,
                    },
                    statusCode: StatusCodes.Status402PaymentRequired);
            }

            var result = await handler.Handle(command, ct);

            if (result.IsFailure)
            {
                if (result.Error.Code == "AI_UNAVAILABLE")
                {
                    await refundHandler.HandleAsync(
                        new RefundConsumptionCommand { UserId = parsedUserId, AdaptRequestId = adaptRequestId },
                        ct);
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
        .RequireAuthorization()
        .RequireCredits(1)
        .RequireRateLimiting(AdaptPolicy)
        .WithName("AdaptCv")
        .WithSummary("Adapta el CV a la vacante usando LLM con cero invención (Constitution Art. I).")
        .WithDescription("Authenticated (Art. VII), credit-gated (1 credit per call, Art. IX), rate-limited 5/h por IP.");

        return app;
    }
}
