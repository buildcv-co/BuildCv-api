using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public sealed class IterationService(
    IterateAdaptationHandler iterateHandler,
    GetIterationResultHandler getHandler) : IIterationService
{
    public Task<IterationResult> RunAsync(Guid userId, string cvText, string vacancyText, int iterationCount, int threshold, CancellationToken ct = default)
        => iterateHandler.HandleAsync(userId, cvText, vacancyText, iterationCount, threshold, ct);

    public Task<IterationResult?> GetAsync(Guid requestId, CancellationToken ct = default)
        => getHandler.HandleAsync(requestId, ct);
}
