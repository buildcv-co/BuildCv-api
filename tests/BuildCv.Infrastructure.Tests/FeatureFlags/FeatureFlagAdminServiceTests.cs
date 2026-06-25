using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class FeatureFlagAdminServiceTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfFeatureFlagStore _store;
    private readonly FeatureFlagAdminService _adminService;

    public FeatureFlagAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfFeatureFlagStore(_dbContext, NullLogger<EfFeatureFlagStore>.Instance);
        _adminService = new FeatureFlagAdminService(_dbContext, _store, NullLogger<FeatureFlagAdminService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task UpdateAsync_updates_current_value_and_writes_audit_log()
    {
        await _store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        var changedBy = Guid.NewGuid();

        var updated = await _adminService.UpdateAsync(
            "wompi-enabled", newValue: false, changedBy, reason: "incident P1-273");

        updated.CurrentValue.Should().BeFalse();
        updated.UpdatedBy.Should().Be(changedBy);

        var auditEntries = await _dbContext.FeatureFlagAuditLogs
            .Where(l => l.FlagName == "wompi-enabled")
            .ToListAsync();
        auditEntries.Should().ContainSingle();
        auditEntries[0].OldValue.Should().BeTrue();
        auditEntries[0].NewValue.Should().BeFalse();
        auditEntries[0].ChangedBy.Should().Be(changedBy);
        auditEntries[0].Reason.Should().Be("incident P1-273");
    }

    [Fact]
    public async Task UpdateAsync_throws_FeatureFlagNotFound_when_flag_not_registered()
    {
        var act = async () => await _adminService.UpdateAsync(
            "unknown-flag", newValue: true, Guid.NewGuid(), reason: null);

        await act.Should().ThrowAsync<FeatureFlagNotFoundException>()
            .Where(e => e.FlagName == "unknown-flag");
    }

    [Fact]
    public async Task UpdateAsync_returns_flag_with_utc_updated_at()
    {
        await _store.UpsertAsync(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        var before = DateTime.UtcNow.AddSeconds(-1);

        var updated = await _adminService.UpdateAsync(
            "wompi-enabled", newValue: false, Guid.NewGuid(), reason: null);

        updated.UpdatedAt.Should().BeOnOrAfter(before);
        updated.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task UpdateAsync_persists_audit_log_with_old_value_captured_before_update()
    {
        await _store.UpsertAsync(new FeatureFlag
        {
            Name = "wompi-enabled",
            DefaultValue = true,
            CurrentValue = true,
            UpdatedAt = DateTime.UtcNow
        });

        await _adminService.UpdateAsync(
            "wompi-enabled", newValue: false, Guid.NewGuid(), reason: null);

        var audit = await _dbContext.FeatureFlagAuditLogs.SingleAsync();
        audit.OldValue.Should().BeTrue();
        audit.NewValue.Should().BeFalse();
    }
}
