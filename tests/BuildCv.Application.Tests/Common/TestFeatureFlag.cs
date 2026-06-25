using System.Collections.Concurrent;
using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Tests.Common;

internal sealed class TestFeatureFlag : IFeatureFlag
{
    private readonly ConcurrentDictionary<string, bool> _cache = new();
    private readonly ConcurrentDictionary<string, FeatureFlag> _store = new();

    public int IsEnabledCallCount => _enabledCalls;

    private int _enabledCalls;

    public void Seed(FeatureFlag flag)
    {
        _store[flag.Name] = flag;
        _cache[flag.Name] = flag.CurrentValue;
    }

    public Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _enabledCalls);
        if (_cache.TryGetValue(name, out var cached))
        {
            return Task.FromResult(cached);
        }

        if (_store.TryGetValue(name, out var flag))
        {
            _cache[name] = flag.CurrentValue;
            return Task.FromResult(flag.CurrentValue);
        }

        throw new FeatureFlagNotFoundException(name);
    }

    public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
    {
        _store.TryGetValue(name, out var flag);
        return Task.FromResult(flag);
    }

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<FeatureFlag> snapshot = _store.Values
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(snapshot);
    }
}