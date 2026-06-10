using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Persistence;

public sealed class EfRefreshTokenStore(BuildCvDbContext dbContext) : IRefreshTokenStore
{
    private const int ExpirySeconds = 604800; // 7 days

    public async Task<string> CreateAsync(Guid userId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    + Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var refreshToken = new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ExpirySeconds),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(ct);

        return token;
    }

    public async Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default)
    {
        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (refreshToken is null)
        {
            return Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Refresh token is invalid or revoked"));
        }

        if (refreshToken.RevokedAt.HasValue)
        {
            return Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Refresh token has been revoked"));
        }

        if (DateTime.UtcNow > refreshToken.ExpiresAt)
        {
            return Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Refresh token has expired"));
        }

        return Result.Success(refreshToken.UserId);
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (refreshToken is not null)
        {
            dbContext.Entry(refreshToken).Property(t => t.RevokedAt).CurrentValue = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
