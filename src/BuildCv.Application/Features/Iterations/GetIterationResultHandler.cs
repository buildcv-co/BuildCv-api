using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public sealed class GetIterationResultHandler(IIterationStore store)
{
    public Task<IterationResult?> HandleAsync(Guid requestId, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId required", nameof(requestId));
        }

        return GetAsync(requestId, ct);
    }

    private async Task<IterationResult?> GetAsync(Guid requestId, CancellationToken ct)
    {
        var (_, result) = await store.GetByIdAsync(requestId, ct);
        return result;
    }
}
