using System.Collections.Concurrent;
using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Infrastructure.Auth;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, (Guid UserId, DateTime ExpiresAt)> _tokens = new();
    private readonly int _expirySeconds;

    public InMemoryRefreshTokenStore(int expirySeconds = 604800)
    {
        _expirySeconds = expirySeconds;
    }

    public Task<string> CreateAsync(Guid userId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        _tokens[token] = (userId, DateTime.UtcNow.AddSeconds(_expirySeconds));
        return Task.FromResult(token);
    }

    public Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (!_tokens.TryGetValue(token, out var entry))
        {
            return Task.FromResult(Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Refresh token is invalid or revoked")));
        }

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            return Task.FromResult(Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Refresh token has expired")));
        }

        return Task.FromResult(Result.Success(entry.UserId));
    }

    public Task RevokeAsync(string token, CancellationToken ct = default)
    {
        _tokens.TryRemove(token, out _);
        return Task.CompletedTask;
    }
}
