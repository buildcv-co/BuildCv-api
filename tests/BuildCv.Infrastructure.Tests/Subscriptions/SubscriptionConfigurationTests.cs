using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class SubscriptionConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public SubscriptionConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType => _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Subscription))!;

    [Fact]
    public void Maps_table_to_subscriptions_with_snake_case_columns()
    {
        var entityType = EntityType;

        entityType.GetTableName().Should().Be("subscriptions");

        var columnNames = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();
        columnNames.Should().Contain([
            "id", "user_id", "plan", "payment_source_id", "status",
            "started_at", "current_period_start", "current_period_end",
            "canceled_at", "last_charge_at", "next_charge_at", "retry_count", "xmin"
        ]);
    }

    [Fact]
    public void PaymentSourceId_has_max_length_200()
    {
        var entityType = EntityType;
        var prop = entityType.FindProperty(nameof(Subscription.PaymentSourceId))!;

        prop.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public void Plan_and_status_are_stored_as_int()
    {
        var entityType = EntityType;

        entityType.FindProperty(nameof(Subscription.Plan))!.GetColumnType().Should().Be("integer");
        entityType.FindProperty(nameof(Subscription.Status))!.GetColumnType().Should().Be("integer");
    }

    [Fact]
    public void Has_unique_partial_index_on_user_active_subscriptions()
    {
        var entityType = EntityType;

        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Subscription.UserId) }));

        uniqueIndex.Should().NotBeNull();
        var resolved = uniqueIndex!;
        resolved.GetDatabaseName().Should().Be("ux_subscriptions_user_active");
        resolved.GetFilter()!.Should().Be("status != 3");
    }

    [Fact]
    public void Has_index_on_status_and_next_charge_at()
    {
        var entityType = EntityType;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(Subscription.Status), nameof(Subscription.NextChargeAt) }));

        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_subscriptions_status_next_charge");
    }

    [Fact]
    public void Has_check_constraints_for_status_plan_and_retry_count()
    {
        var entityType = EntityType;

        var constraints = entityType.GetCheckConstraints().Select(c => c.Name).ToList();
        constraints.Should().Contain(["ck_subscriptions_status", "ck_subscriptions_plan", "ck_subscriptions_retry_count"]);

        entityType.GetCheckConstraints().Single(c => c.Name == "ck_subscriptions_status").Sql.Should().Be("status IN (1,2,3)");
        entityType.GetCheckConstraints().Single(c => c.Name == "ck_subscriptions_plan").Sql.Should().Be("plan IN (1,2)");
        entityType.GetCheckConstraints().Single(c => c.Name == "ck_subscriptions_retry_count").Sql
            .Should().Be("retry_count >= 0 AND retry_count <= 3");
    }

    [Fact]
    public void Cascade_deletes_from_user()
    {
        var entityType = EntityType;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(BuildCv.Domain.Auth.User));
    }

    [Fact]
    public void RetryCount_has_default_zero()
    {
        var entityType = EntityType;

        entityType.FindProperty(nameof(Subscription.RetryCount))!.GetDefaultValue().Should().Be(0);
    }
}
