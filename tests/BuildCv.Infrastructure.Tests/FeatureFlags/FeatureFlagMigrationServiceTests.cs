using BuildCv.Application.Common;
using BuildCv.Infrastructure.FeatureFlags;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class FeatureFlagMigrationServiceTests
{
    [Fact]
    public async Task StartAsync_seeds_three_rows_from_appsettings_defaults()
    {
        var flags = new Dictionary<string, bool>
        {
            ["factus-enabled"] = true,
            ["wompi-enabled"] = true,
            ["credits-enabled"] = false
        };
        var store = new InMemoryFeatureFlagStore();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureFlagStore>(store);
        var sp = services.BuildServiceProvider();

        var options = Options.Create(new FeatureFlagsOptions
        {
            CacheTtlSeconds = 60,
            Defaults = new Dictionary<string, bool>(flags)
        });
        var hosted = new FeatureFlagMigrationService(sp, options, NullLogger<FeatureFlagMigrationService>.Instance);

        await hosted.StartAsync(CancellationToken.None);

        var all = await store.ListAsync();
        all.Should().HaveCount(3);
        all.Select(f => f.Name).Should().BeEquivalentTo(
            new[] { "factus-enabled", "wompi-enabled", "credits-enabled" });
        var credits = await store.GetAsync("credits-enabled");
        credits!.CurrentValue.Should().BeFalse();
        credits.DefaultValue.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_is_idempotent_on_rerun()
    {
        var flags = new Dictionary<string, bool>
        {
            ["wompi-enabled"] = true,
            ["factus-enabled"] = true
        };
        var store = new InMemoryFeatureFlagStore();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureFlagStore>(store);
        var sp = services.BuildServiceProvider();

        var options = Options.Create(new FeatureFlagsOptions
        {
            CacheTtlSeconds = 60,
            Defaults = new Dictionary<string, bool>(flags)
        });
        var hosted = new FeatureFlagMigrationService(sp, options, NullLogger<FeatureFlagMigrationService>.Instance);

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StartAsync(CancellationToken.None);

        var all = await store.ListAsync();
        all.Should().HaveCount(2, "rerunning the migration must not duplicate rows");
    }

    [Fact]
    public async Task StartAsync_logs_but_does_not_throw_when_seed_fails()
    {
        var throwing = new ThrowingFeatureFlagStore();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureFlagStore>(throwing);
        var sp = services.BuildServiceProvider();

        var options = Options.Create(new FeatureFlagsOptions
        {
            CacheTtlSeconds = 60,
            Defaults = new Dictionary<string, bool> { ["wompi-enabled"] = true }
        });
        var hosted = new FeatureFlagMigrationService(sp, options, NullLogger<FeatureFlagMigrationService>.Instance);

        var act = async () => await hosted.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync("a seed failure must be logged and swallowed");
    }

    [Fact]
    public async Task StartAsync_does_nothing_when_appsettings_defaults_is_empty()
    {
        var store = new InMemoryFeatureFlagStore();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureFlagStore>(store);
        var sp = services.BuildServiceProvider();

        var options = Options.Create(new FeatureFlagsOptions
        {
            CacheTtlSeconds = 60,
            Defaults = new Dictionary<string, bool>()
        });
        var hosted = new FeatureFlagMigrationService(sp, options, NullLogger<FeatureFlagMigrationService>.Instance);

        await hosted.StartAsync(CancellationToken.None);

        var all = await store.ListAsync();
        all.Should().BeEmpty();
    }

    private sealed class ThrowingFeatureFlagStore : IFeatureFlagStore
    {
        public Task<BuildCv.Domain.FeatureFlags.FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated store failure");

        public Task<IReadOnlyList<BuildCv.Domain.FeatureFlags.FeatureFlag>> ListAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("simulated store failure");

        public Task UpsertAsync(BuildCv.Domain.FeatureFlags.FeatureFlag flag, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated store failure");

        public Task AppendAuditLogAsync(BuildCv.Domain.FeatureFlags.FeatureFlagAuditLog log, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated store failure");

        public Task<IReadOnlyList<BuildCv.Domain.FeatureFlags.FeatureFlagAuditLog>> GetAuditLogAsync(
            string flagName, int limit, string? cursor, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated store failure");
    }
}
