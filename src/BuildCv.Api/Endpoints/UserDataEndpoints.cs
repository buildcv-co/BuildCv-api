using System.Security.Claims;
using BuildCv.Api.Contracts;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Auth;

namespace BuildCv.Api.Endpoints;

public static class UserDataEndpoints
{
    public static IEndpointRouteBuilder MapUserDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/user/data", async (
            ClaimsPrincipal user,
            GetUserDataHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var query = new GetUserDataQuery(userId.Value);
            var result = await handler.HandleAsync(query, ct);

            return result.IsSuccess
                ? Results.Ok(MapToUserDataResponse(result.Value))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/arco", title = result.Error.Code, status = 404, detail = result.Error.Message },
                    statusCode: result.Error.Code == "ARCO/DATA_NOT_FOUND" ? 404 : 403);
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimiting.ConsentPolicy)
        .WithName("GetUserData")
        .WithSummary("ARCO: Access — returns all user data.");

        app.MapPut("/api/v1/user/data", async (
            ClaimsPrincipal user,
            RectifyUserDataRequest request,
            RectifyUserDataHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new RectifyUserDataCommand(userId.Value, request.Email, request.Name);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(MapToUserDataResponse(result.Value))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/arco", title = result.Error.Code, status = 403, detail = result.Error.Message },
                    statusCode: 403);
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimiting.ConsentPolicy)
        .WithName("RectifyUserData")
        .WithSummary("ARCO: Rectification — updates user data fields.");

        app.MapDelete("/api/v1/user/data", async (
            ClaimsPrincipal user,
            DeleteUserDataHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new DeleteUserDataCommand(userId.Value);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { message = "User data deleted successfully" })
                : Results.Json(
                    new { type = "https://buildcv.com/errors/arco", title = result.Error.Code, status = 403, detail = result.Error.Message },
                    statusCode: 403);
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimiting.ConsentPolicy)
        .WithName("DeleteUserData")
        .WithSummary("ARCO: Cancellation — deletes all user data and revokes consent.");

        app.MapPost("/api/v1/user/data/consent", async (
            ClaimsPrincipal user,
            ConsentRequest request,
            GrantConsentHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new GrantConsentCommand(userId.Value, request.Purpose, 1);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { message = "Consent granted", consentId = result.Value.Id })
                : Results.Json(
                    new { type = "https://buildcv.com/errors/consent", title = result.Error.Code, status = result.Error.Code == "CONSENT/ALREADY_GRANTED" ? 409 : 403, detail = result.Error.Message },
                    statusCode: result.Error.Code == "CONSENT/ALREADY_GRANTED" ? 409 : 403);
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimiting.ConsentPolicy)
        .WithName("GrantConsent")
        .WithSummary("Grants consent for data processing.");

        app.MapPost("/api/v1/user/data/consent/revoke", async (
            ClaimsPrincipal user,
            ConsentRequest request,
            RevokeConsentHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new RevokeConsentCommand(userId.Value, request.Purpose);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { message = "Consent revoked" })
                : Results.Json(
                    new { type = "https://buildcv.com/errors/consent", title = result.Error.Code, status = 403, detail = result.Error.Message },
                    statusCode: 403);
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimiting.ConsentPolicy)
        .WithName("RevokeConsent")
        .WithSummary("Revokes active consent for data processing.");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }

    private static UserDataResponse MapToUserDataResponse(Domain.Auth.User user) => new(
        user.Id, user.Provider, user.Email, user.Name, user.CreatedAt, user.LastLoginAt);
}
