using System.Collections.Concurrent;
using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Tests.Features.Iterations;

internal sealed class TestIterationStore : IIterationStore
{
    private readonly ConcurrentDictionary<Guid, IterationRequest> _requests = new();
    private readonly ConcurrentDictionary<Guid, IterationResult> _results = new();

    public IReadOnlyList<IterationRequest> AllRequests => _requests.Values.OrderBy(r => r.CreatedAt).ToList();
    public IReadOnlyList<IterationResult> AllResults => _results.Values.OrderBy(r => r.CompletedAt).ToList();

    public Task SaveRequestAsync(IterationRequest request, CancellationToken ct = default)
    {
        _requests[request.RequestId] = request;
        return Task.CompletedTask;
    }

    public Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default)
    {
        if (_requests.TryGetValue(requestId, out var existing))
        {
            _requests[requestId] = existing with { Status = status };
        }

        return Task.CompletedTask;
    }

    public Task SaveResultAsync(IterationResult result, CancellationToken ct = default)
    {
        _results[result.RequestId] = result;
        return Task.CompletedTask;
    }

    public Task<(IterationRequest?, IterationResult?)> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var hasRequest = _requests.TryGetValue(requestId, out var request);
        var hasResult = _results.TryGetValue(requestId, out var result);
        return Task.FromResult((hasRequest ? request : null, hasResult ? result : null));
    }
}
