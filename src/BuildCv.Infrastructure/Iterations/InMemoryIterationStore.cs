using System.Collections.Concurrent;
using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;

namespace BuildCv.Infrastructure.Iterations;

public sealed class InMemoryIterationStore : IIterationStore, IIterationCleanupCapable
{
    private readonly ConcurrentDictionary<Guid, IterationRequest> _requests = new();
    private readonly ConcurrentDictionary<Guid, IterationResult> _results = new();

    public async Task SaveRequestAsync(IterationRequest request, CancellationToken ct = default)
    {
        _requests[request.RequestId] = request;
        await Task.CompletedTask;
    }

    public async Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default)
    {
        if (_requests.TryGetValue(requestId, out var existing))
        {
            _requests[requestId] = existing with { Status = status };
        }

        await Task.CompletedTask;
    }

    public async Task SaveResultAsync(IterationResult result, CancellationToken ct = default)
    {
        var withExpiry = result.ExpiresAt == default
            ? result with { ExpiresAt = DateTime.UtcNow.AddHours(24) }
            : result;
        _results[result.RequestId] = withExpiry;
        await Task.CompletedTask;
    }

    public Task<(IterationRequest?, IterationResult?)> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        _requests.TryGetValue(requestId, out var req);
        _results.TryGetValue(requestId, out var res);
        return Task.FromResult((req, res));
    }

    public Task<int> DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var expired = _results
            .Where(kvp => kvp.Value.ExpiresAt < olderThan)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
        {
            _results.TryRemove(key, out _);
        }

        return Task.FromResult(expired.Count);
    }
}
