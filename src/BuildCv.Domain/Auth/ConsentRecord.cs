namespace BuildCv.Domain.Auth;

public sealed record ConsentRecord
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int PolicyVersion { get; init; }
    public DateTime ConsentDate { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string Purpose { get; init; } = "";
    public bool IsValid => RevokedAt is null;
}
