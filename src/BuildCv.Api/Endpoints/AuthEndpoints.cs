using System.Security.Claims;
using BuildCv.Api.Contracts;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Auth;
using BuildCv.Infrastructure.Auth;

namespace BuildCv.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/google", async (
            OAuthCallbackRequest request,
            GoogleOAuthCallbackHandler handler,
            JwtTokenAdapter jwtAdapter,
            CancellationToken ct) =>
        {
            var command = new GoogleOAuthCallbackCommand(request.Code, "http://localhost/callback");
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(MapToTokenResponse(result.Value, jwtAdapter))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/auth", title = result.Error.Code, status = 401, detail = result.Error.Message },
                    statusCode: 401);
        })
        .RequireRateLimiting(RateLimiting.AuthPolicy)
        .WithName("GoogleAuth")
        .WithSummary("Google OAuth callback — exchanges code for tokens.");

        app.MapPost("/api/v1/auth/linkedin", async (
            OAuthCallbackRequest request,
            LinkedInOAuthCallbackHandler handler,
            JwtTokenAdapter jwtAdapter,
            CancellationToken ct) =>
        {
            var command = new LinkedInOAuthCallbackCommand(request.Code, "http://localhost/callback");
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(MapToTokenResponse(result.Value, jwtAdapter))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/auth", title = result.Error.Code, status = 401, detail = result.Error.Message },
                    statusCode: 401);
        })
        .RequireRateLimiting(RateLimiting.AuthPolicy)
        .WithName("LinkedInAuth")
        .WithSummary("LinkedIn OAuth callback — exchanges code for tokens.");

        app.MapGet("/api/v1/auth/me", (ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");

            if (userId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new UserProfileResponse(
                Guid.Parse(userId),
                "unknown",
                email ?? "",
                ""));
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimiting.AuthPolicy)
        .WithName("GetMe")
        .WithSummary("Returns the current authenticated user profile.");

        app.MapPost("/api/v1/auth/refresh", async (
            RefreshTokenRequest request,
            RefreshTokenHandler handler,
            JwtTokenAdapter jwtAdapter,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.Json(
                    new { type = "https://buildcv.com/errors/auth", title = "AUTH/MISSING_REFRESH_TOKEN", status = 400, detail = "refreshToken is required" },
                    statusCode: 400);
            }

            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(MapToTokenResponse(result.Value, jwtAdapter))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/auth", title = result.Error.Code, status = 401, detail = result.Error.Message },
                    statusCode: 401);
        })
        .RequireRateLimiting(RateLimiting.AuthPolicy)
        .WithName("RefreshToken")
        .WithSummary("Refreshes an access token using a refresh token.");

        app.MapPost("/api/v1/auth/web-signup", async (
            WebSignupRequest request,
            WebSignupHandler handler,
            CancellationToken ct) =>
        {
            var command = new WebSignupCommand(request.Provider, request.ProviderAccountId, request.Email, request.Name);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(new WebSignupResponse(result.Value.UserId))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/auth", title = result.Error.Code, status = 400, detail = result.Error.Message },
                    statusCode: 400);
        })
        .RequireRateLimiting(RateLimiting.AuthPolicy)
        .WithName("WebSignup")
        .WithSummary("Registers or upserts a user from the NextAuth session (web BFF).");

        app.MapPost("/api/v1/auth/logout", async (
            RefreshTokenRequest? request,
            ClaimsPrincipal user,
            LogoutHandler handler,
            CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            Guid? userId = Guid.TryParse(userIdClaim, out var parsed) ? parsed : null;

            var command = new LogoutCommand(request?.RefreshToken, userId);
            var result = await handler.HandleAsync(command, ct);

            return result.IsSuccess
                ? Results.Ok(new LogoutResponse("Logged out successfully"))
                : Results.Json(
                    new { type = "https://buildcv.com/errors/auth", title = "LOGOUT_FAILED", status = 500, detail = "Logout failed" },
                    statusCode: 500);
        })
        .RequireRateLimiting(RateLimiting.AuthPolicy)
        .WithName("Logout")
        .WithSummary("Revokes refresh tokens (logout). Accepts refreshToken in body OR bearer JWT in Authorization header for full revoke-all.");

        return app;
    }

    private static TokenResponse MapToTokenResponse(OAuthTokenResponse oauthResponse, JwtTokenAdapter jwtAdapter)
    {
        var accessToken = jwtAdapter.GenerateAccessToken(oauthResponse.User.UserId, oauthResponse.User.Email);
        return new TokenResponse(
            AccessToken: accessToken,
            RefreshToken: oauthResponse.RefreshToken,
            User: new UserProfileResponse(
                oauthResponse.User.UserId,
                oauthResponse.User.Provider,
                oauthResponse.User.Email,
                oauthResponse.User.Name));
    }
}

public sealed record RefreshTokenRequest(string? RefreshToken);
