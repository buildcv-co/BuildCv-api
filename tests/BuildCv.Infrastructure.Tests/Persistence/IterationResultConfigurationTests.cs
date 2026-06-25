using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class IterationResultConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public IterationResultConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType =>
        _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(IterationResult))!;

    [Fact]
    public void Maps_table_to_iteration_results_with_snake_case_columns()
    {
        var entityType = EntityType;

        entityType.GetTableName().Should().Be("iteration_results");

        var columnNames = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();
        columnNames.Should().Contain([
            "request_id",
            "status",
            "best_step",
            "all_steps",
            "probability_warning",
            "credits_consumed",
            "completed_at",
            "expires_at",
        ]);
    }

    [Fact]
    public void Primary_key_is_request_id()
    {
        var entityType = EntityType;

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().ContainSingle().Which.Should().Be(nameof(IterationResult.RequestId));
    }

    [Fact]
    public void Status_is_stored_as_integer()
    {
        var entityType = EntityType;

        var status = entityType.FindProperty(nameof(IterationResult.Status))!;
        status.GetColumnType().Should().Be("integer");
    }

    [Fact]
    public void Best_step_is_optional_and_stored_as_jsonb()
    {
        var entityType = EntityType;

        var bestStep = entityType.FindProperty(nameof(IterationResult.BestStep))!;
        bestStep.IsNullable.Should().BeTrue();
        bestStep.GetColumnType().Should().Be("jsonb");
    }

    [Fact]
    public void All_steps_is_required_and_stored_as_jsonb()
    {
        var entityType = EntityType;

        var allSteps = entityType.FindProperty(nameof(IterationResult.AllSteps))!;
        allSteps.IsNullable.Should().BeFalse();
        allSteps.GetColumnType().Should().Be("jsonb");
    }

    [Fact]
    public void Probability_warning_is_optional_text()
    {
        var entityType = EntityType;

        var warning = entityType.FindProperty(nameof(IterationResult.ProbabilityWarning))!;
        warning.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Has_index_on_expires_at()
    {
        var entityType = EntityType;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(IterationResult.ExpiresAt) }));

        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_iteration_results_expires_at");
    }

    [Fact]
    public void Cascade_deletes_from_iteration_request()
    {
        var entityType = EntityType;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(IterationRequest));
    }
}
