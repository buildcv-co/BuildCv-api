using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class CachingFeatureFlagDecorator : IFeatureFlag
{
    private readonly IFeatureFlagStore _store;
    private readonly FeatureFlagsOptions _options;
    private readonly ILogger<CachingFeatureFlagDecorator> _logger;
    private readonly IMemoryCache _cache;

    public CachingFeatureFlagDecorator(
        IFeatureFlagStore store,
        IOptions<FeatureFlagsOptions> options,
        ILogger<CachingFeatureFlagDecorator> logger)
        : this(store, options, logger, new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024,
            ExpirationScanFrequency = TimeSpan.FromSeconds(30)
        }))
    {
    }

    internal CachingFeatureFlagDecorator(
        IFeatureFlagStore store,
        IOptions<FeatureFlagsOptions> options,
        ILogger<CachingFeatureFlagDecorator> logger,
        IMemoryCache cache)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(name);
        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        var flag = await _store.GetAsync(name, ct);
        var hasDefault = _options.Defaults.TryGetValue(name, out var appsettingsDefault);

        bool value;
        if (flag is not null)
        {
            value = flag.CurrentValue;
        }
        else if (hasDefault)
        {
            value = appsettingsDefault;
        }
        else
        {
            throw new FeatureFlagNotFoundException(name);
        }

        var entry = _cache.CreateEntry(cacheKey);
        entry.Size = 1;
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CacheTtlSeconds);
        entry.Value = value;
        entry.Dispose();
        return value;
    }

    public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
        => _store.GetAsync(name, ct);

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
        => _store.ListAsync(ct);

    public void Invalidate(string name)
    {
        _cache.Remove(CacheKey(name));
        _logger.LogInformation("Cache invalidated for flag {FlagName}", name);
    }

    private static string CacheKey(string name) => $"feature-flag:{name}";
}
