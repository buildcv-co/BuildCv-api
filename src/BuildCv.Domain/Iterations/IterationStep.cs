namespace BuildCv.Domain.Iterations;

public sealed record IterationStep
{
    public int IterationNumber { get; init; }
    public string AdaptedCvText { get; init; } = "";
    public int Score { get; init; }
    public bool PassedArtI { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan Duration { get; init; }
}
