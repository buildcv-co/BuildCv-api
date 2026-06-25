using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class AddCreditLedgerMigrationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public AddCreditLedgerMigrationTests()
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

        migrations.Should().Contain(m => m.Contains("AddCreditLedger"));
    }

    [Fact]
    public void Migration_creates_credit_ledger_entries_table_with_constraints()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(CreditLedgerEntry))!;

        entityType.GetTableName().Should().Be("credit_ledger_entries");

        var constraints = entityType.GetCheckConstraints().Select(c => c.Name).ToList();
        constraints.Should().Contain(["ck_credit_ledger_delta_nonzero", "ck_credit_ledger_balance_nonneg"]);
    }

    [Fact]
    public void Migration_adds_credit_balance_to_users()
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var userType = model.FindEntityType(typeof(BuildCv.Domain.Auth.User))!;
        var creditBalance = userType.FindProperty(nameof(BuildCv.Domain.Auth.User.CreditBalance));

        creditBalance.Should().NotBeNull();
        creditBalance!.GetColumnName().Should().Be("credit_balance");
        userType.GetCheckConstraints().Should().Contain(c => c.Name == "ck_users_credit_balance_nonneg");
    }
}
