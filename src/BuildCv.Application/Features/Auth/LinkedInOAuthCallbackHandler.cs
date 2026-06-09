using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class LinkedInOAuthCallbackHandler(
    IAuthenticationService authService,
    IUserDataService userDataService,
    IRefreshTokenStore refreshTokenStore)
{
    public async Task<Result<OAuthTokenResponse>> HandleAsync(LinkedInOAuthCallbackCommand command, CancellationToken ct)
    {
        var userInfoResult = await authService.ExchangeCodeAsync("linkedin", command.Code, command.RedirectUri, ct);
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

        var response = new OAuthTokenResponse(
            AccessToken: "jwt-placeholder",
            RefreshToken: refreshToken,
            User: new UserProfileResponse(user.Id, user.Provider, user.Email, user.Name));

        return Result.Success(response);
    }
}
