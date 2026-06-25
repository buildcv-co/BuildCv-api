using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class AddSubscriptionsMigrationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public AddSubscriptionsMigrationTests()
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
        var migrations = _dbContext.Database.GetMigrations();

        migrations.Should().Contain(m => m.Contains("AddSubscriptions"));
    }

    [Fact]
    public void Migration_creates_subscriptions_table_with_constraints()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Subscription))!;

        entityType.GetTableName().Should().Be("subscriptions");

        var constraints = entityType.GetCheckConstraints().Select(c => c.Name).ToList();
        constraints.Should().Contain(["ck_subscriptions_status", "ck_subscriptions_plan", "ck_subscriptions_retry_count"]);
    }

    [Fact]
    public void Migration_creates_unique_partial_index_on_user_active()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Subscription))!;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "ux_subscriptions_user_active");

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("status != 3");
    }

    [Fact]
    public void Migration_creates_index_on_status_next_charge_at()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Subscription))!;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "ix_subscriptions_status_next_charge");

        index.Should().NotBeNull();
        index!.Properties.Select(p => p.Name).Should().Contain(["Status", "NextChargeAt"]);
    }

    [Fact]
    public void Migration_sets_xmin_concurrency_token()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Subscription))!;

        var xmin = entityType.FindProperty("xmin");

        xmin.Should().NotBeNull();
        xmin!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void Migration_declares_FK_to_users_with_cascade_delete()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Subscription))!;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(BuildCv.Domain.Auth.User));
    }
}
