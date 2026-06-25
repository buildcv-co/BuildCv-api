namespace BuildCv.Domain.Iterations;

public sealed record IterationRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string CvText { get; init; } = "";
    public string VacancyText { get; init; } = "";
    public int IterationCount { get; init; } = 5;
    public int ProbabilityThreshold { get; init; } = 50;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public RequestStatus Status { get; init; } = RequestStatus.Running;

    public static IterationRequest Create(Guid userId, string cvText, string vacancyText, int iterationCount, int threshold, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(cvText))
        {
            throw new ArgumentException("CV text required", nameof(cvText));
        }

        if (string.IsNullOrWhiteSpace(vacancyText))
        {
            throw new ArgumentException("Vacancy text required", nameof(vacancyText));
        }

        if (iterationCount < 1 || iterationCount > 20)
        {
            throw new ArgumentException("Iteration count must be 1-20", nameof(iterationCount));
        }

        if (threshold < 0 || threshold > 100)
        {
            throw new ArgumentException("Threshold must be 0-100", nameof(threshold));
        }

        return new IterationRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = userId,
            CvText = cvText,
            VacancyText = vacancyText,
            IterationCount = iterationCount,
            ProbabilityThreshold = threshold,
            CreatedAt = now,
            Status = RequestStatus.Running,
        };
    }
}
