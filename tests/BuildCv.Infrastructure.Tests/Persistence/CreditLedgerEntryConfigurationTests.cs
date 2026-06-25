using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Persistence;
using BuildCv.Infrastructure.Persistence.Configurations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class CreditLedgerEntryConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public CreditLedgerEntryConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType => _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CreditLedgerEntry))!;

    [Fact]
    public void Maps_table_to_credit_ledger_entries_with_snake_case_columns()
    {
        var entityType = EntityType;

        entityType.GetTableName().Should().Be("credit_ledger_entries");

        var columnNames = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();
        columnNames.Should().Contain(["id", "user_id", "reason", "reference", "delta", "balance_after", "metadata", "created_at"]);
    }

    [Fact]
    public void Reason_is_stored_as_string_with_max_length_50()
    {
        var entityType = EntityType;
        var reasonProperty = entityType.FindProperty(nameof(CreditLedgerEntry.Reason))!;

        reasonProperty.GetColumnType().Should().Be("character varying(50)");
    }

    [Fact]
    public void Reference_has_max_length_200()
    {
        var entityType = EntityType;
        var referenceProperty = entityType.FindProperty(nameof(CreditLedgerEntry.Reference))!;

        referenceProperty.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public void Has_unique_index_on_user_id_reason_reference()
    {
        var entityType = EntityType;

        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(CreditLedgerEntry.UserId), nameof(CreditLedgerEntry.Reason), nameof(CreditLedgerEntry.Reference) }));

        uniqueIndex.Should().NotBeNull();
        uniqueIndex!.GetDatabaseName().Should().Be("ux_credit_ledger_user_reason_reference");
    }

    [Fact]
    public void Has_descending_index_on_user_id_created_at()
    {
        var entityType = EntityType;

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(CreditLedgerEntry.UserId), nameof(CreditLedgerEntry.CreatedAt) }));

        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_credit_ledger_user_created_at");
    }

    [Fact]
    public void Has_check_constraint_delta_nonzero()
    {
        var entityType = EntityType;

        var constraint = entityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_credit_ledger_delta_nonzero");
        constraint.Should().NotBeNull();
        constraint!.Sql.Should().Be("delta <> 0");
    }

    [Fact]
    public void Has_check_constraint_balance_nonneg()
    {
        var entityType = EntityType;

        var constraint = entityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_credit_ledger_balance_nonneg");
        constraint.Should().NotBeNull();
        constraint!.Sql.Should().Be("balance_after >= 0");
    }

    [Fact]
    public void Cascade_deletes_from_user()
    {
        var entityType = EntityType;

        var fk = entityType.GetForeignKeys().Single();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(User));
    }
}
