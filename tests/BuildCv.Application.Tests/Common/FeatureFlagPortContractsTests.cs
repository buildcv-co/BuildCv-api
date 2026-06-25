using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Application.Tests.Common;

public sealed class FeatureFlagPortContractsTests
{
    [Fact]
    public async Task IFeatureFlag_GetAsync_returns_flag_when_exists()
    {
        var flags = new TestFeatureFlag();
        flags.Seed(FeatureFlag.Create("wompi-enabled", defaultValue: true));

        var result = await flags.GetAsync("wompi-enabled");

        result.Should().NotBeNull();
        result!.Name.Should().Be("wompi-enabled");
        result.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task IFeatureFlagStore_UpsertAsync_adds_new_flag()
    {
        var store = new TestFeatureFlagStore();
        var flag = FeatureFlag.Create("factus-enabled", defaultValue: true);

        await store.UpsertAsync(flag);

        var stored = await store.GetAsync("factus-enabled");
        stored.Should().NotBeNull();
        stored!.DefaultValue.Should().BeTrue();
        stored.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task IFeatureFlagStore_UpsertAsync_replaces_existing_flag()
    {
        var store = new TestFeatureFlagStore();
        await store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        await store.UpsertAsync(new FeatureFlag
        {
            Name = "wompi-enabled",
            DefaultValue = true,
            CurrentValue = false,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = Guid.NewGuid()
        });

        var stored = await store.GetAsync("wompi-enabled");
        stored.Should().NotBeNull();
        stored!.CurrentValue.Should().BeFalse();
    }

    [Fact]
    public async Task IFeatureFlagStore_AppendAuditLogAsync_writes_entry()
    {
        var store = new TestFeatureFlagStore();
        var changedBy = Guid.NewGuid();
        var log = new FeatureFlagAuditLog
        {
            FlagName = "wompi-enabled",
            OldValue = true,
            NewValue = false,
            ChangedBy = changedBy,
            Reason = "incident P1-273"
        };

        await store.AppendAuditLogAsync(log);

        store.AllAuditEntries.Should().ContainSingle();
        store.AllAuditEntries.Single().ChangedBy.Should().Be(changedBy);
        store.AllAuditEntries.Single().Reason.Should().Be("incident P1-273");
    }

    [Fact]
    public async Task IFeatureFlagStore_GetAuditLogAsync_returns_entries_newest_first()
    {
        var store = new TestFeatureFlagStore();
        var now = DateTime.UtcNow;
        await store.AppendAuditLogAsync(new FeatureFlagAuditLog
        {
            FlagName = "wompi-enabled",
            OldValue = null,
            NewValue = true,
            ChangedBy = Guid.NewGuid(),
            ChangedAt = now.AddSeconds(-30)
        });
        await store.AppendAuditLogAsync(new FeatureFlagAuditLog
        {
            FlagName = "wompi-enabled",
            OldValue = true,
            NewValue = false,
            ChangedBy = Guid.NewGuid(),
            ChangedAt = now.AddSeconds(-10)
        });
        await store.AppendAuditLogAsync(new FeatureFlagAuditLog
        {
            FlagName = "wompi-enabled",
            OldValue = false,
            NewValue = true,
            ChangedBy = Guid.NewGuid(),
            ChangedAt = now
        });

        var entries = await store.GetAuditLogAsync("wompi-enabled", limit: 10, cursor: null);

        entries.Should().HaveCount(3);
        entries[0].ChangedAt.Should().BeOnOrAfter(entries[1].ChangedAt);
        entries[1].ChangedAt.Should().BeOnOrAfter(entries[2].ChangedAt);
    }

    [Fact]
    public void FeatureFlagsOptions_defaults_cache_ttl_to_sixty_seconds()
    {
        var options = new FeatureFlagsOptions();

        options.CacheTtlSeconds.Should().Be(60);
    }
}
