using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class LogoutHandler(IRefreshTokenStore refreshTokenStore)
{
    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            await refreshTokenStore.RevokeAsync(command.RefreshToken, ct);
            return Result.Success();
        }

        if (command.UserId.HasValue)
        {
            await refreshTokenStore.RevokeAllForUserAsync(command.UserId.Value, ct);
            return Result.Success();
        }

        return Result.Failure(new Error("AUTH/LOGOUT_INVALID", "Either refreshToken or authenticated user is required"));
    }
}
