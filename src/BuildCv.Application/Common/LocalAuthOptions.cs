namespace BuildCv.Application.Common;

public sealed class LocalAuthOptions
{
    public bool Enabled { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public int InitialCredits { get; init; } = 1000;
}
