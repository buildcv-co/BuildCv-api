using System.Collections.Concurrent;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class InMemoryNumberingRangeStore : INumberingRangeStore
{
    private readonly ConcurrentDictionary<Guid, NumberingRange> _ranges = new();

    public Task AddAsync(NumberingRange range, CancellationToken ct = default)
    {
        _ranges[range.Id] = range;
        return Task.CompletedTask;
    }

    public Task<NumberingRange?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _ranges.TryGetValue(id, out var range);
        return Task.FromResult(range);
    }

    public Task<NumberingRange?> GetByProviderIdAsync(int providerId, CancellationToken ct = default)
    {
        var range = _ranges.Values.FirstOrDefault(r => r.ProviderId == providerId);
        return Task.FromResult(range);
    }

    public Task<NumberingRange?> GetByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var range = _ranges.Values.FirstOrDefault(r => r.Prefix == prefix);
        return Task.FromResult(range);
    }

    public Task<IReadOnlyList<NumberingRange>> GetAllAsync(CancellationToken ct = default)
    {
        var ranges = _ranges.Values.ToList();
        return Task.FromResult<IReadOnlyList<NumberingRange>>(ranges);
    }

    public Task UpdateAsync(NumberingRange range, CancellationToken ct = default)
    {
        _ranges[range.Id] = range;
        return Task.CompletedTask;
    }
}
