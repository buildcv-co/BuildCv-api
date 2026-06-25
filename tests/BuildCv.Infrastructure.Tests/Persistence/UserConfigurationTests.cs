using BuildCv.Domain.Auth;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class UserConfigurationTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;

    public UserConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql("Host=localhost;Database=ignored")
            .Options;
        _dbContext = new BuildCvDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private IEntityType EntityType => _dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(User))!;

    [Fact]
    public void Maps_credit_balance_to_snake_case_column()
    {
        var property = EntityType.FindProperty(nameof(User.CreditBalance))!;
        property.GetColumnName().Should().Be("credit_balance");
    }

    [Fact]
    public void Credit_balance_defaults_to_zero()
    {
        var property = EntityType.FindProperty(nameof(User.CreditBalance))!;
        property.GetDefaultValue().Should().Be(0);
    }

    [Fact]
    public void Credit_balance_is_required()
    {
        var property = EntityType.FindProperty(nameof(User.CreditBalance))!;
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Has_check_constraint_credit_balance_nonneg()
    {
        var entityType = EntityType;
        var constraint = entityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_users_credit_balance_nonneg");
        constraint.Should().NotBeNull();
        constraint!.Sql.Should().Be("credit_balance >= 0");
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
