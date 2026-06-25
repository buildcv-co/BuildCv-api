using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class BuildCvDbContextCreditLedgerTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public BuildCvDbContextCreditLedgerTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public void DbContext_exposes_CreditLedgerEntries_DbSet()
    {
        _dbContext.CreditLedgerEntries.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_model_includes_credit_ledger_entries_entity()
    {
        var entityType = _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CreditLedgerEntry));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("credit_ledger_entries");
    }

    [Fact]
    public void DbContext_applies_credit_ledger_entry_configuration()
    {
        var entityType = _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CreditLedgerEntry))!;

        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(CreditLedgerEntry.UserId), nameof(CreditLedgerEntry.Reason), nameof(CreditLedgerEntry.Reference) }));

        uniqueIndex.Should().NotBeNull();
    }
}
