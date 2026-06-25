using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public interface IIterationService
{
    Task<IterationResult> RunAsync(Guid userId, string cvText, string vacancyText, int iterationCount, int threshold, CancellationToken ct = default);
    Task<IterationResult?> GetAsync(Guid requestId, CancellationToken ct = default);
}
