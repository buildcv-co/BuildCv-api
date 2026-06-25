namespace BuildCv.Domain.Iterations;

public sealed record IterationResult
{
    public Guid RequestId { get; init; }
    public RequestStatus Status { get; init; } = RequestStatus.Running;
    public IterationStep? BestStep { get; init; }
    public IReadOnlyList<IterationStep> AllSteps { get; init; } = Array.Empty<IterationStep>();
    public string? ProbabilityWarning { get; init; }
    public int CreditsConsumed { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddDays(1);
}
