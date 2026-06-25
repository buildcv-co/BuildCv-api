using BuildCv.Domain.FeatureFlags;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class AddFeatureFlagsMigrationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public AddFeatureFlagsMigrationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public void Migration_class_is_registered()
    {
        var migrator = _dbContext.GetService<IMigrator>();
        var migrations = _dbContext.Database.GetMigrations();

        migrations.Should().Contain(m => m.Contains("AddFeatureFlags"));
    }

    [Fact]
    public void Migration_creates_feature_flags_table_with_constraints()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(FeatureFlag))!;

        entityType.GetTableName().Should().Be("feature_flags");

        var constraints = entityType.GetCheckConstraints().Select(c => c.Name).ToList();
        constraints.Should().Contain("ck_feature_flags_current_value_not_null");
    }

    [Fact]
    public void Migration_creates_feature_flag_audit_log_table_with_index()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(FeatureFlagAuditLog))!;

        entityType.GetTableName().Should().Be("feature_flag_audit_log");

        var constraints = entityType.GetCheckConstraints().Select(c => c.Name).ToList();
        constraints.Should().Contain("ck_feature_flag_audit_log_new_value_not_null");

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "ix_feature_flag_audit_log_flag_name_changed_at");
        index.Should().NotBeNull();
    }

    [Fact]
    public void Migration_sets_feature_flag_name_as_primary_key()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(FeatureFlag))!;

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().ContainSingle().Which.Should().Be(nameof(FeatureFlag.Name));
    }
}
