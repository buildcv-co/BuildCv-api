using BuildCv.Application.Common;
using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Auth;

public sealed record OAuthTokenResponse(string AccessToken, string RefreshToken, UserProfileResponse User);
public sealed record UserProfileResponse(Guid UserId, string Provider, string Email, string Name);

public sealed class GoogleOAuthCallbackHandler(
    IAuthenticationService authService,
    IUserDataService userDataService,
    IRefreshTokenStore refreshTokenStore,
    AccreditWelcomeHandler? welcomeHandler = null,
    ICreditsFeatureFlag? creditsFeature = null,
    ILogger<GoogleOAuthCallbackHandler>? logger = null)
{
    public async Task<Result<OAuthTokenResponse>> HandleAsync(GoogleOAuthCallbackCommand command, CancellationToken ct)
    {
        var userInfoResult = await authService.ExchangeCodeAsync("google", command.Code, command.RedirectUri, ct);
        if (userInfoResult.IsFailure)
        {
            return Result.Failure<OAuthTokenResponse>(userInfoResult.Error);
        }

        var userInfo = userInfoResult.Value;
        var userResult = await userDataService.GetOrCreateAsync(userInfo.Provider, userInfo.ProviderId, userInfo.Email, userInfo.Name, ct);
        if (userResult.IsFailure)
        {
            return Result.Failure<OAuthTokenResponse>(userResult.Error);
        }

        var user = userResult.Value;
        var refreshToken = await refreshTokenStore.CreateAsync(user.Id, ct);

        await TryGrantWelcomeCreditsAsync(user, ct);

        var response = new OAuthTokenResponse(
            AccessToken: "jwt-placeholder",
            RefreshToken: refreshToken,
            User: new UserProfileResponse(user.Id, user.Provider, user.Email, user.Name));

        return Result.Success(response);
    }

    private async Task TryGrantWelcomeCreditsAsync(User user, CancellationToken ct)
    {
        if (welcomeHandler is null || creditsFeature is null || !creditsFeature.IsEnabled)
        {
            return;
        }

        try
        {
            await welcomeHandler.HandleAsync(new AccreditWelcomeCommand { UserId = user.Id }, ct);
            logger?.LogInformation("Welcome credits granted to user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "Welcome credit grant failed for user {UserId}; ledger left untouched",
                user.Id);
        }
    }
}
