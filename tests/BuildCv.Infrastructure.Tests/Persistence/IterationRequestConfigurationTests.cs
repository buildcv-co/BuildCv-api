using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class IterationRequestConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public IterationRequestConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType =>
        _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(IterationRequest))!;

    [Fact]
    public void Maps_table_to_iteration_requests_with_snake_case_columns()
    {
        var entityType = EntityType;

        entityType.GetTableName().Should().Be("iteration_requests");

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
    public void Primary_key_is_request_id()
    {
        var entityType = EntityType;

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().ContainSingle().Which.Should().Be(nameof(IterationRequest.RequestId));
    }

    [Fact]
    public void Status_is_stored_as_integer()
    {
        var entityType = EntityType;

        var status = entityType.FindProperty(nameof(IterationRequest.Status))!;
        status.GetColumnType().Should().Be("integer");
    }

    [Fact]
    public void CvText_and_vacancy_text_use_text_column_type()
    {
        var entityType = EntityType;

        entityType.FindProperty(nameof(IterationRequest.CvText))!.GetColumnType().Should().Be("text");
        entityType.FindProperty(nameof(IterationRequest.VacancyText))!.GetColumnType().Should().Be("text");
    }

    [Fact]
    public void Has_index_on_user_id_and_created_at_descending()
    {
        var entityType = EntityType;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(IterationRequest.UserId), nameof(IterationRequest.CreatedAt) }));

        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_iteration_requests_user_created_at");
        var descending = index!.IsDescending ?? [];
        descending.Should().Contain([false, true]);
    }

    [Fact]
    public void Has_index_on_status_and_created_at()
    {
        var entityType = EntityType;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(IterationRequest.Status), nameof(IterationRequest.CreatedAt) })
                && i.GetDatabaseName() != "ix_iteration_requests_user_created_at");

        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_iteration_requests_status_created_at");
    }

    [Fact]
    public void Has_no_xmin_in_domain_properties()
    {
        var entityType = EntityType;

        var xmin = entityType.FindProperty("xmin");
        xmin.Should().NotBeNull();
        xmin!.IsShadowProperty().Should().BeTrue();
    }

    [Fact]
    public void Has_xmin_concurrency_token()
    {
        var entityType = EntityType;

        var xmin = entityType.FindProperty("xmin");
        xmin.Should().NotBeNull();
        xmin!.IsConcurrencyToken.Should().BeTrue();
        xmin.GetColumnType().Should().Be("xid");
    }

    [Fact]
    public void Cascade_deletes_from_user()
    {
        var entityType = EntityType;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(BuildCv.Domain.Auth.User));
    }
}
