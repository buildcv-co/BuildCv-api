using System.Security.Claims;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/subscriptions")
            .RequireAuthorization()
            .WithTags("Subscriptions");

        group.MapPost("/", SubscribeHandler)
            .RequireRateLimiting(RateLimiting.SubscriptionPolicy)
            .WithName("Subscribe")
            .WithSummary("Crea una suscripción mensual. Requiere feature flag activo (Art. IX FR-046).");

        group.MapGet("/me", GetSubscriptionHandler)
            .WithName("GetMySubscription")
            .WithSummary("Devuelve la suscripción activa o cancelada del usuario autenticado.");

        group.MapDelete("/me", CancelSubscriptionHandler)
            .RequireRateLimiting(RateLimiting.SubscriptionCancelPolicy)
            .WithName("CancelMySubscription")
            .WithSummary("Cancela la suscripción activa. Sin reembolso para el período actual (Art. IV).");

        return app;
    }

    private static async Task<IResult> SubscribeHandler(
        ClaimsPrincipal user,
        SubscribeRequest body,
        SubscribeHandler subscribe,
        ISubscriptionFeatureFlag featureFlag,
        CancellationToken ct)
    {
        if (!featureFlag.IsEnabled)
        {
            return Results.Json(
                new { error = "SUBSCRIPTION/DISABLED" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var sub = await subscribe.HandleAsync(userId.Value, body.Plan, body.PaymentSourceId, ct);
            return Results.Created($"/api/v1/subscriptions/{sub.Id}", SubscriptionDto.FromDomain(sub));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already has", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = "SUBSCRIPTION/ALREADY_ACTIVE" },
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> GetSubscriptionHandler(
        ClaimsPrincipal user,
        GetSubscriptionHandler getSubscription,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var sub = await getSubscription.HandleAsync(userId.Value, ct);
        return sub is null
            ? Results.Json(
                new { error = "SUBSCRIPTION/NOT_FOUND" },
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(SubscriptionDto.FromDomain(sub));
    }

    private static async Task<IResult> CancelSubscriptionHandler(
        ClaimsPrincipal user,
        CancelSubscriptionHandler cancel,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var canceled = await cancel.HandleAsync(userId.Value, ct);
            return Results.Ok(new CancelSubscriptionResponse(
                Status: "canceled",
                AccessUntil: canceled.CurrentPeriodEnd));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                                                  || ex.Message.Contains("No subscription", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = "SUBSCRIPTION/NOT_FOUND" },
                statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }
}

public sealed record SubscribeRequest(SubscriptionPlan Plan, string PaymentSourceId);

public sealed record CancelSubscriptionResponse(string Status, DateTime AccessUntil);

public sealed record SubscriptionDto(
    Guid Id,
    string Plan,
    string Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime NextChargeAt,
    DateTime? CanceledAt)
{
    public static SubscriptionDto FromDomain(Subscription s) => new(
        s.Id,
        s.Plan.ToString().ToLowerInvariant(),
        StatusToWire(s.Status),
        s.CurrentPeriodStart,
        s.CurrentPeriodEnd,
        s.NextChargeAt,
        s.CanceledAt);

    private static string StatusToWire(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Active => "active",
        SubscriptionStatus.PastDue => "past_due",
        SubscriptionStatus.Canceled => "canceled",
        _ => status.ToString().ToLowerInvariant(),
    };
}
