using System.Security.Claims;
using BuildCv.Application.Features.Credits;

namespace BuildCv.Api.Endpoints;

public static class CreditEndpoints
{
    public static IEndpointRouteBuilder MapCreditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/credits")
            .RequireAuthorization()
            .WithTags("Credits");

        group.MapGet("/balance", async (
            ClaimsPrincipal user,
            GetCreditBalanceHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var view = await handler.HandleAsync(new GetCreditBalanceQuery { UserId = userId.Value }, ct);
            return Results.Ok(new { balance = view.Balance, recentConsumption = view.RecentConsumption });
        })
        .WithName("GetCreditBalance")
        .WithSummary("Returns the authenticated user's current credit balance and recent consumption (last 7 days).");

        group.MapGet("/history", async (
            ClaimsPrincipal user,
            GetCreditHistoryHandler handler,
            int? limit,
            string? cursor,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var page = await handler.HandleAsync(
                new GetCreditHistoryQuery { UserId = userId.Value, Limit = limit ?? 50, Cursor = cursor },
                ct);

            return Results.Ok(new
            {
                entries = page.Entries,
                nextCursor = page.NextCursor,
            });
        })
        .WithName("GetCreditHistory")
        .WithSummary("Returns the authenticated user's credit ledger history (newest first), cursor-paginated.");

        group.MapPost("/gift", async (
            ClaimsPrincipal user,
            GiftCreditsRequest request,
            GrantManualCreditHandler handler,
            CancellationToken ct) =>
        {
            var adminId = GetUserId(user);
            if (adminId is null)
            {
                return Results.Unauthorized();
            }

            if (request.Amount == 0)
            {
                return Results.Json(
                    new { error = "CREDIT/INVALID_AMOUNT", message = "Amount must be non-zero." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var entry = await handler.HandleAsync(
                new GrantManualCreditCommand
                {
                    UserId = request.UserId,
                    AdminId = adminId.Value,
                    Delta = request.Amount,
                    Reason = request.Reason,
                    Reference = request.Reference,
                },
                ct);

            return Results.Ok(new
            {
                entryId = entry.Id,
                newBalance = entry.BalanceAfter,
            });
        })
        .RequireAuthorization(policy => policy.RequireRole("admin"))
        .WithName("GiftCredits")
        .WithSummary("Operator tool: manually credit or debit a user (admin only).");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }
}

public sealed record GiftCreditsRequest(Guid UserId, int Amount, string? Reason, string? Reference);
