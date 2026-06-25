using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class InMemoryFeatureFlagStoreTests
{
    [Fact]
    public async Task GetAsync_returns_null_when_flag_not_seeded()
    {
        var store = new InMemoryFeatureFlagStore();

        var result = await store.GetAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_then_GetAsync_round_trips_flag()
    {
        var store = new InMemoryFeatureFlagStore();
        var flag = FeatureFlag.Create("wompi-enabled", defaultValue: true);

        await store.UpsertAsync(flag);

        var result = await store.GetAsync("wompi-enabled");
        result.Should().NotBeNull();
        result!.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_replaces_existing_flag_with_same_name()
    {
        var store = new InMemoryFeatureFlagStore();
        await store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));

        await store.UpsertAsync(new FeatureFlag
        {
            Name = "wompi-enabled",
            DefaultValue = true,
            CurrentValue = false,
            UpdatedAt = DateTime.UtcNow
        });

        var result = await store.GetAsync("wompi-enabled");
        result.Should().NotBeNull();
        result!.CurrentValue.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_returns_all_flags_sorted_by_name()
    {
        var store = new InMemoryFeatureFlagStore();
        await store.UpsertAsync(FeatureFlag.Create("wompi-enabled", true));
        await store.UpsertAsync(FeatureFlag.Create("factus-enabled", true));
        await store.UpsertAsync(FeatureFlag.Create("credits-enabled", false));

        var result = await store.ListAsync();

        result.Select(f => f.Name).Should().ContainInOrder(
            "credits-enabled", "factus-enabled", "wompi-enabled");
    }

    [Fact]
    public async Task AppendAuditLogAsync_stores_entry()
    {
        var store = new InMemoryFeatureFlagStore();
        var log = new FeatureFlagAuditLog
        {
            Id = Guid.NewGuid(),
            FlagName = "wompi-enabled",
            OldValue = true,
            NewValue = false,
            ChangedBy = Guid.NewGuid()
        };

        await store.AppendAuditLogAsync(log);

        var result = await store.GetAuditLogAsync("wompi-enabled", limit: 10, cursor: null);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(log.Id);
    }
}
