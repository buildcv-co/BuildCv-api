using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class EfFeatureFlagStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfFeatureFlagStore _store;

    public EfFeatureFlagStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfFeatureFlagStore(_dbContext, NullLogger<EfFeatureFlagStore>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetAsync_returns_flag_when_exists()
    {
        var flag = FeatureFlag.Create("wompi-enabled", defaultValue: true);
        _dbContext.FeatureFlags.Add(flag);
        await _dbContext.SaveChangesAsync();

        var result = await _store.GetAsync("wompi-enabled");

        result.Should().NotBeNull();
        result!.Name.Should().Be("wompi-enabled");
        result.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_returns_null_when_not_found()
    {
        var result = await _store.GetAsync("nonexistent-flag");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_inserts_new_flag()
    {
        var flag = FeatureFlag.Create("factus-enabled", defaultValue: true);

        await _store.UpsertAsync(flag);

        var saved = await _dbContext.FeatureFlags.FindAsync("factus-enabled");
        saved.Should().NotBeNull();
        saved!.DefaultValue.Should().BeTrue();
        saved.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_updates_existing_flag()
    {
        var initial = FeatureFlag.Create("credits-enabled", defaultValue: false);
        _dbContext.FeatureFlags.Add(initial);
        await _dbContext.SaveChangesAsync();

        var updated = initial with { CurrentValue = true, UpdatedBy = Guid.NewGuid() };
        await _store.UpsertAsync(updated);

        var result = await _store.GetAsync("credits-enabled");
        result.Should().NotBeNull();
        result!.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task ListAsync_returns_all_flags_sorted_by_name()
    {
        _dbContext.FeatureFlags.Add(FeatureFlag.Create("wompi-enabled", true));
        _dbContext.FeatureFlags.Add(FeatureFlag.Create("factus-enabled", true));
        _dbContext.FeatureFlags.Add(FeatureFlag.Create("credits-enabled", false));
        await _dbContext.SaveChangesAsync();

        var result = await _store.ListAsync();

        result.Select(f => f.Name).Should().ContainInOrder(
            "credits-enabled", "factus-enabled", "wompi-enabled");
    }

    [Fact]
    public async Task AppendAuditLogAsync_persists_entry()
    {
        var changedBy = Guid.NewGuid();
        var log = new FeatureFlagAuditLog
        {
            Id = Guid.NewGuid(),
            FlagName = "wompi-enabled",
            OldValue = true,
            NewValue = false,
            ChangedBy = changedBy,
            Reason = "incident P1-273"
        };

        await _store.AppendAuditLogAsync(log);

        var saved = await _dbContext.FeatureFlagAuditLogs.FindAsync(log.Id);
        saved.Should().NotBeNull();
        saved!.ChangedBy.Should().Be(changedBy);
        saved.Reason.Should().Be("incident P1-273");
    }

    [Fact]
    public async Task GetAuditLogAsync_returns_entries_newest_first()
    {
        var older = AuditEntry("wompi-enabled", DateTime.UtcNow.AddMinutes(-5), true, false);
        var newer = AuditEntry("wompi-enabled", DateTime.UtcNow, false, true);
        await _store.AppendAuditLogAsync(older);
        await _store.AppendAuditLogAsync(newer);

        var result = await _store.GetAuditLogAsync("wompi-enabled", limit: 10, cursor: null);

        result.Should().HaveCount(2);
        result[0].ChangedAt.Should().BeAfter(result[1].ChangedAt);
    }

    [Fact]
    public async Task GetAuditLogAsync_paginates_with_cursor()
    {
        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await _store.AppendAuditLogAsync(AuditEntry(
                "wompi-enabled",
                baseTime.AddMinutes(-i),
                oldValue: i % 2 == 0,
                newValue: i % 2 == 1));
        }

        var page1 = await _store.GetAuditLogAsync("wompi-enabled", limit: 2, cursor: null);

        page1.Should().HaveCount(2);

        var lastChangedAt = page1[^1].ChangedAt.Ticks;
        var lastId = page1[^1].Id;
        var cursor = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{lastChangedAt}:{lastId}"));

        var page2 = await _store.GetAuditLogAsync("wompi-enabled", limit: 2, cursor: cursor);

        page2.Should().HaveCount(2);
        page2[0].ChangedAt.Should().BeBefore(page1[^1].ChangedAt);
    }

    [Fact]
    public async Task GetAuditLogAsync_clamps_limit_to_200()
    {
        var log = AuditEntry("wompi-enabled", DateTime.UtcNow, null, true);
        await _store.AppendAuditLogAsync(log);

        var result = await _store.GetAuditLogAsync("wompi-enabled", limit: 5000, cursor: null);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAuditLogAsync_filters_by_flag_name()
    {
        await _store.AppendAuditLogAsync(AuditEntry("wompi-enabled", DateTime.UtcNow, true, false));
        await _store.AppendAuditLogAsync(AuditEntry("factus-enabled", DateTime.UtcNow, false, true));

        var result = await _store.GetAuditLogAsync("wompi-enabled", limit: 50, cursor: null);

        result.Should().ContainSingle();
        result[0].FlagName.Should().Be("wompi-enabled");
    }

    private static FeatureFlagAuditLog AuditEntry(
        string flagName, DateTime at, bool? oldValue, bool newValue) =>
        new()
        {
            Id = Guid.NewGuid(),
            FlagName = flagName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = Guid.NewGuid(),
            ChangedAt = at,
            Reason = null
        };
}
