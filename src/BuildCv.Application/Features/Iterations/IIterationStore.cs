using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public interface IIterationStore
{
    Task SaveRequestAsync(IterationRequest request, CancellationToken ct = default);
    Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default);
    Task SaveResultAsync(IterationResult result, CancellationToken ct = default);
    Task<(IterationRequest?, IterationResult?)> GetByIdAsync(Guid requestId, CancellationToken ct = default);
}
