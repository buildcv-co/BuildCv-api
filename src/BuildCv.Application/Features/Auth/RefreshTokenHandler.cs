using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class RefreshTokenHandler(
    IUserDataService userDataService,
    IRefreshTokenStore refreshTokenStore)
{
    public async Task<Result<OAuthTokenResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken ct)
    {
        var validateResult = await refreshTokenStore.ValidateAsync(command.RefreshToken, ct);
        if (validateResult.IsFailure)
        {
            return Result.Failure<OAuthTokenResponse>(validateResult.Error);
        }

        var userId = validateResult.Value;
        var userResult = await userDataService.GetByIdAsync(userId, ct);
        if (userResult.IsFailure)
        {
            return Result.Failure<OAuthTokenResponse>(userResult.Error);
        }

        var user = userResult.Value;
        await refreshTokenStore.RevokeAsync(command.RefreshToken, ct);
        var newRefreshToken = await refreshTokenStore.CreateAsync(userId, ct);

        var response = new OAuthTokenResponse(
            AccessToken: "jwt-placeholder",
            RefreshToken: newRefreshToken,
            User: new UserProfileResponse(user.Id, user.Provider, user.Email, user.Name));

        return Result.Success(response);
    }
}
