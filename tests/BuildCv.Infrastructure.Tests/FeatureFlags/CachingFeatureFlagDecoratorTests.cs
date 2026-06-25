using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.FeatureFlags;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class CachingFeatureFlagDecoratorTests
{
    [Fact]
    public async Task IsEnabledAsync_returns_db_value_and_caches_for_ttl()
    {
        var store = new FakeFeatureFlagStore();
        await store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var options = Options.Create(new FeatureFlagsOptions { CacheTtlSeconds = 60 });
        var decorator = new CachingFeatureFlagDecorator(
            store, options, NullLogger<CachingFeatureFlagDecorator>.Instance, cache);

        var first = await decorator.IsEnabledAsync("wompi-enabled");
        var second = await decorator.IsEnabledAsync("wompi-enabled");

        first.Should().BeTrue();
        second.Should().BeTrue();
        store.GetCallCount.Should().Be(1, "second call should hit the cache, not the store");
    }

    [Fact]
    public async Task Invalidate_removes_cache_entry_so_next_call_refetches()
    {
        var store = new FakeFeatureFlagStore();
        await store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var options = Options.Create(new FeatureFlagsOptions { CacheTtlSeconds = 60 });
        var decorator = new CachingFeatureFlagDecorator(
            store, options, NullLogger<CachingFeatureFlagDecorator>.Instance, cache);

        await decorator.IsEnabledAsync("wompi-enabled");
        decorator.Invalidate("wompi-enabled");
        var result = await decorator.IsEnabledAsync("wompi-enabled");

        result.Should().BeTrue();
        store.GetCallCount.Should().Be(2, "after invalidation, store must be hit again");
    }

    [Fact]
    public async Task IsEnabledAsync_falls_back_to_appsettings_default_when_store_returns_null()
    {
        var store = new FakeFeatureFlagStore();
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var options = Options.Create(new FeatureFlagsOptions
        {
            CacheTtlSeconds = 60,
            Defaults = new Dictionary<string, bool> { ["factus-enabled"] = true }
        });
        var decorator = new CachingFeatureFlagDecorator(
            store, options, NullLogger<CachingFeatureFlagDecorator>.Instance, cache);

        var result = await decorator.IsEnabledAsync("factus-enabled");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_throws_FeatureFlagNotFound_when_neither_db_nor_appsettings_has_flag()
    {
        var store = new FakeFeatureFlagStore();
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var options = Options.Create(new FeatureFlagsOptions { CacheTtlSeconds = 60 });
        var decorator = new CachingFeatureFlagDecorator(
            store, options, NullLogger<CachingFeatureFlagDecorator>.Instance, cache);

        var act = async () => await decorator.IsEnabledAsync("unknown-flag");

        await act.Should().ThrowAsync<FeatureFlagNotFoundException>()
            .Where(e => e.FlagName == "unknown-flag");
    }

    [Fact]
    public async Task IsEnabledAsync_db_value_overrides_appsettings_default()
    {
        var store = new FakeFeatureFlagStore();
        await store.UpsertAsync(new FeatureFlag
        {
            Name = "wompi-enabled",
            DefaultValue = false,
            CurrentValue = true,
            UpdatedAt = DateTime.UtcNow
        });
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var options = Options.Create(new FeatureFlagsOptions
        {
            CacheTtlSeconds = 60,
            Defaults = new Dictionary<string, bool> { ["wompi-enabled"] = false }
        });
        var decorator = new CachingFeatureFlagDecorator(
            store, options, NullLogger<CachingFeatureFlagDecorator>.Instance, cache);

        var result = await decorator.IsEnabledAsync("wompi-enabled");

        result.Should().BeTrue("DB value (true) wins over appsettings default (false)");
    }

    [Fact]
    public async Task Ttl_expires_refetches_value_from_store()
    {
        var store = new FakeFeatureFlagStore();
        await store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var options = Options.Create(new FeatureFlagsOptions { CacheTtlSeconds = 1 });
        var decorator = new CachingFeatureFlagDecorator(
            store, options, NullLogger<CachingFeatureFlagDecorator>.Instance, cache);

        await decorator.IsEnabledAsync("wompi-enabled");
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        var result = await decorator.IsEnabledAsync("wompi-enabled");

        result.Should().BeTrue();
        store.GetCallCount.Should().BeGreaterOrEqualTo(2, "after TTL expiry, store must be hit again");
    }

    private sealed class FakeFeatureFlagStore : IFeatureFlagStore
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FeatureFlag> _flags = new();

        public int GetCallCount { get; private set; }

        public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
        {
            GetCallCount++;
            _flags.TryGetValue(name, out var flag);
            return Task.FromResult(flag);
        }

        public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
        {
            IReadOnlyList<FeatureFlag> snapshot = _flags.Values
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(snapshot);
        }

        public Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
        {
            _flags[flag.Name] = flag;
            return Task.CompletedTask;
        }

        public Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
            string flagName, int limit, string? cursor, CancellationToken ct = default)
        {
            IReadOnlyList<FeatureFlagAuditLog> snapshot = [];
            return Task.FromResult(snapshot);
        }
    }
}
