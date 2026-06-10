namespace BuildCv.Infrastructure.Persistence;

public sealed class RefreshToken
{
    public string Token { get; init; } = "";

    public Guid UserId { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime? RevokedAt { get; init; }

    public DateTime CreatedAt { get; init; }
}
