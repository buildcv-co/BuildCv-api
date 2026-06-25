namespace BuildCv.Domain.Auth;

public sealed record User
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime LastLoginAt { get; init; }
    public int CreditBalance { get; init; } = 0;
}
