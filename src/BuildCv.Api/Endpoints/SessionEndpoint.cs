using BuildCv.Application.Features.Auth;
using BuildCv.Infrastructure.Auth;

namespace BuildCv.Api.Endpoints;

public static class SessionEndpoint
{
    public static IEndpointRouteBuilder MapSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/session", async (
                HttpContext httpContext,
                NextAuthJwtValidator nextAuthValidator,
                IUserDataStore userStore,
                JwtTokenAdapter jwtAdapter,
                CancellationToken ct) =>
            {
                if (!TryExtractBearerToken(httpContext, out var nextAuthJwt))
                {
                    return Results.Unauthorized();
                }

                var userId = nextAuthValidator.TryExtractUserId(nextAuthJwt);
                if (userId is null)
                {
                    return Results.Unauthorized();
                }

                var userResult = await userStore.GetByIdAsync(userId.Value, ct);
                if (!userResult.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                var user = userResult.Value;
                var backendJwt = jwtAdapter.GenerateAccessToken(user.Id, user.Email, user.Name);
                var expiresAt = DateTime.UtcNow.AddMinutes(15);

                return Results.Ok(new SessionResponse(
                    Jwt: backendJwt,
                    ExpiresAt: expiresAt,
                    User: new SessionUserInfo(user.Id, user.Email, user.Name)));
            })
            .WithName("GetSession")
            .WithSummary("Exchanges a NextAuth-signed session JWT for a short-lived backend access JWT.")
            .Produces<SessionResponse>(200)
            .Produces(401);

        return app;
    }

    private static bool TryExtractBearerToken(HttpContext httpContext, out string token)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authHeader.StartsWith(bearerPrefix, StringComparison.Ordinal))
        {
            token = string.Empty;
            return false;
        }

        token = authHeader[bearerPrefix.Length..].Trim();
        return !string.IsNullOrEmpty(token);
    }
}

public sealed record SessionResponse(string Jwt, DateTime ExpiresAt, SessionUserInfo User);

public sealed record SessionUserInfo(Guid Id, string Email, string Name);
