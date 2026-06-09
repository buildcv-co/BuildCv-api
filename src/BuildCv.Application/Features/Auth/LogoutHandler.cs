using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class LogoutHandler(IRefreshTokenStore refreshTokenStore)
{
    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken ct)
    {
        await refreshTokenStore.RevokeAsync(command.RefreshToken, ct);
        return Result.Success();
    }
}
