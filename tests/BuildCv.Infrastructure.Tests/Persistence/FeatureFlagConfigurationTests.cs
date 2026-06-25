using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class FeatureFlagConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public FeatureFlagConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType =>
        _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(FeatureFlag))!;

    [Fact]
    public void Maps_table_to_feature_flags_with_snake_case_columns()
    {
        var entityType = EntityType;

        entityType.GetTableName().Should().Be("feature_flags");

        var columnNames = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();
        columnNames.Should().Contain(
        [
            "name",
            "default_value",
            "current_value",
            "updated_at",
            "updated_by"
        ]);
    }

    [Fact]
    public void Has_primary_key_on_name()
    {
        var entityType = EntityType;

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().ContainSingle().Which.Should().Be(nameof(FeatureFlag.Name));
    }

    [Fact]
    public void Name_column_has_max_length_100()
    {
        var entityType = EntityType;
        var nameProperty = entityType.FindProperty(nameof(FeatureFlag.Name))!;

        nameProperty.GetMaxLength().Should().Be(100);
    }

    [Fact]
    public void Current_and_default_values_are_required()
    {
        var entityType = EntityType;

        entityType.FindProperty(nameof(FeatureFlag.DefaultValue))!.IsNullable.Should().BeFalse();
        entityType.FindProperty(nameof(FeatureFlag.CurrentValue))!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Has_xmin_concurrency_token()
    {
        var entityType = EntityType;

        var xmin = entityType.FindProperty("xmin");
        xmin.Should().NotBeNull();
        xmin!.IsConcurrencyToken.Should().BeTrue();
        xmin.GetColumnType().Should().Be("xid");
        xmin.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }
}

public sealed class FeatureFlagAuditLogConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public FeatureFlagAuditLogConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType =>
        _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(FeatureFlagAuditLog))!;

    [Fact]
    public void Maps_table_to_feature_flag_audit_log_with_snake_case_columns()
    {
        var entityType = EntityType;

        entityType.GetTableName().Should().Be("feature_flag_audit_log");

        var columnNames = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();
        columnNames.Should().Contain(
        [
            "id",
            "flag_name",
            "old_value",
            "new_value",
            "changed_by",
            "changed_at",
            "reason"
        ]);
    }

    [Fact]
    public void Has_primary_key_on_id()
    {
        var entityType = EntityType;

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().ContainSingle().Which.Should().Be(nameof(FeatureFlagAuditLog.Id));
    }

    [Fact]
    public void Flag_name_column_has_max_length_100()
    {
        var entityType = EntityType;
        var flagNameProperty = entityType.FindProperty(nameof(FeatureFlagAuditLog.FlagName))!;

        flagNameProperty.GetMaxLength().Should().Be(100);
    }

    [Fact]
    public void New_value_and_changed_by_are_required()
    {
        var entityType = EntityType;

        entityType.FindProperty(nameof(FeatureFlagAuditLog.NewValue))!.IsNullable.Should().BeFalse();
        entityType.FindProperty(nameof(FeatureFlagAuditLog.ChangedBy))!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Old_value_is_nullable_for_seed_entries()
    {
        var entityType = EntityType;

        entityType.FindProperty(nameof(FeatureFlagAuditLog.OldValue))!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Reason_column_has_max_length_500()
    {
        var entityType = EntityType;
        var reasonProperty = entityType.FindProperty(nameof(FeatureFlagAuditLog.Reason))!;

        reasonProperty.GetMaxLength().Should().Be(500);
    }

    [Fact]
    public void Has_descending_index_on_flag_name_changed_at()
    {
        var entityType = EntityType;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(FeatureFlagAuditLog.FlagName), nameof(FeatureFlagAuditLog.ChangedAt) }));

        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_feature_flag_audit_log_flag_name_changed_at");
    }
}
