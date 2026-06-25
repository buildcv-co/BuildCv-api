using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class AddIterationResultsMigrationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public AddIterationResultsMigrationTests()
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

        migrations.Should().Contain(m => m.Contains("AddIterationResults"));
    }

    [Fact]
    public void Migration_creates_iteration_requests_table_with_columns_and_indexes()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(IterationRequest))!;

        entityType.GetTableName().Should().Be("iteration_requests");

        var indexes = entityType.GetIndexes().Select(i => i.GetDatabaseName()).ToList();
        indexes.Should().Contain("ix_iteration_requests_user_created_at");
        indexes.Should().Contain("ix_iteration_requests_status_created_at");

        var columnNames = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();
        columnNames.Should().Contain([
            "request_id",
            "user_id",
            "cv_text",
            "vacancy_text",
            "iteration_count",
            "probability_threshold",
            "created_at",
            "status",
        ]);
    }

    [Fact]
    public void Migration_creates_iteration_results_table_with_jsonb_columns()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(IterationResult))!;

        entityType.GetTableName().Should().Be("iteration_results");

        entityType.FindProperty(nameof(IterationResult.BestStep))!.GetColumnType().Should().Be("jsonb");
        entityType.FindProperty(nameof(IterationResult.AllSteps))!.GetColumnType().Should().Be("jsonb");
        entityType.FindProperty(nameof(IterationResult.ExpiresAt))!.GetColumnType().Should().Be("timestamp with time zone");

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "ix_iteration_results_expires_at");
        index.Should().NotBeNull();
    }

    [Fact]
    public void Iteration_request_has_xmin_concurrency_token()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(IterationRequest))!;

        var xmin = entityType.FindProperty("xmin");
        xmin.Should().NotBeNull();
        xmin!.IsConcurrencyToken.Should().BeTrue();
        xmin.GetColumnType().Should().Be("xid");
    }

    [Fact]
    public void Iteration_requests_FK_to_users_uses_cascade_delete()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(IterationRequest))!;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(BuildCv.Domain.Auth.User));
    }

    [Fact]
    public void Iteration_results_FK_to_requests_uses_cascade_delete()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(IterationResult))!;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(IterationRequest));
    }
}
