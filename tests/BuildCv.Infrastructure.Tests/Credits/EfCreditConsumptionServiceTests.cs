using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Credits;

public sealed class EfCreditConsumptionServiceTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfCreditConsumptionService _service;
    private readonly Guid _userId;

    public EfCreditConsumptionServiceTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _userId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = _userId,
            Provider = "google",
            ProviderId = "google-1",
            Email = "user@example.com",
            Name = "Test User",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            CreditBalance = 0
        });
        _dbContext.SaveChanges();
        var ledger = new EfCreditLedger(_dbContext, NullLogger<EfCreditLedger>.Instance);
        _service = new EfCreditConsumptionService(ledger, NullLogger<EfCreditConsumptionService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ConsumeForAdaptAsync_deducts_one_credit_when_balance_positive()
    {
        await SeedBalance(5);

        var result = await _service.ConsumeForAdaptAsync(_userId, Guid.NewGuid(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(4);
        result.ErrorCode.Should().BeNull();

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(4);
    }

    [Fact]
    public async Task ConsumeForAdaptAsync_fails_with_credit_insufficient_when_balance_zero()
    {
        await SeedBalance(0);

        var result = await _service.ConsumeForAdaptAsync(_userId, Guid.NewGuid(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDIT/INSUFFICIENT");
        result.BalanceAfter.Should().Be(0);
    }

    [Fact]
    public async Task ConsumeForAdaptAsync_is_idempotent_for_same_adapt_request_id()
    {
        await SeedBalance(5);
        var adaptRequestId = Guid.NewGuid();

        var first = await _service.ConsumeForAdaptAsync(_userId, adaptRequestId, CancellationToken.None);
        var replay = await _service.ConsumeForAdaptAsync(_userId, adaptRequestId, CancellationToken.None);

        first.Success.Should().BeTrue();
        first.BalanceAfter.Should().Be(4);
        replay.Success.Should().BeTrue();
        replay.BalanceAfter.Should().Be(4);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(4);

        var consumptionEntries = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId && e.Reason == CreditLedgerReason.Consumption)
            .ToListAsync();
        consumptionEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task RefundConsumptionAsync_restores_credit()
    {
        await SeedBalance(5);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(_userId, adaptRequestId, CancellationToken.None);

        await _service.RefundConsumptionAsync(_userId, adaptRequestId, CancellationToken.None);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(5);

        var entries = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
        entries.Should().HaveCount(2);
        entries[0].Reason.Should().Be(CreditLedgerReason.Consumption);
        entries[0].Delta.Should().Be(-1);
        entries[1].Reason.Should().Be(CreditLedgerReason.Refund);
        entries[1].Delta.Should().Be(1);
    }

    [Fact]
    public async Task RefundConsumptionAsync_is_idempotent_for_same_adapt_request_id()
    {
        await SeedBalance(5);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(_userId, adaptRequestId, CancellationToken.None);
        await _service.RefundConsumptionAsync(_userId, adaptRequestId, CancellationToken.None);
        await _service.RefundConsumptionAsync(_userId, adaptRequestId, CancellationToken.None);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(5);

        var refundEntries = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId && e.Reason == CreditLedgerReason.Refund)
            .ToListAsync();
        refundEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task RefundConsumptionAsync_throws_when_no_prior_consume()
    {
        var act = async () => await _service.RefundConsumptionAsync(_userId, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetBalanceAsync_returns_balance_and_recent_consumption()
    {
        await SeedBalance(10);
        var adaptRequestId = Guid.NewGuid();
        await _service.ConsumeForAdaptAsync(_userId, adaptRequestId, CancellationToken.None);

        var view = await _service.GetBalanceAsync(_userId, CancellationToken.None);

        view.Balance.Should().Be(9);
        view.RecentConsumption.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_entries_newest_first()
    {
        await SeedBalance(10);
        await _service.ConsumeForAdaptAsync(_userId, Guid.NewGuid(), CancellationToken.None);
        await _service.ConsumeForAdaptAsync(_userId, Guid.NewGuid(), CancellationToken.None);

        var page = await _service.GetHistoryAsync(_userId, 50, null, CancellationToken.None);

        page.Entries.Should().HaveCount(2);
        page.Entries[0].Reason.Should().Be(CreditLedgerReason.Consumption);
        page.Entries[1].Reason.Should().Be(CreditLedgerReason.Consumption);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_encodes_cursor_for_next_page()
    {
        await SeedBalance(10);
        for (var i = 0; i < 5; i++)
        {
            _dbContext.CreditLedgerEntries.Add(CreditLedgerEntry.Create(
                _userId, CreditLedgerReason.Purchase, $"payment:{i}", 1, i + 1, null, DateTime.UtcNow.AddSeconds(i)));
        }
        await _dbContext.SaveChangesAsync();

        var page = await _service.GetHistoryAsync(_userId, 2, null, CancellationToken.None);

        page.Entries.Should().HaveCount(2);
        page.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_decodes_cursor_and_returns_next_page()
    {
        await SeedBalance(10);
        for (var i = 0; i < 5; i++)
        {
            _dbContext.CreditLedgerEntries.Add(CreditLedgerEntry.Create(
                _userId, CreditLedgerReason.Purchase, $"payment:{i}", 1, i + 1, null, DateTime.UtcNow.AddSeconds(i)));
        }
        await _dbContext.SaveChangesAsync();

        var firstPage = await _service.GetHistoryAsync(_userId, 2, null, CancellationToken.None);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();

        var secondPage = await _service.GetHistoryAsync(_userId, 2, firstPage.NextCursor, CancellationToken.None);

        secondPage.Entries.Should().HaveCount(2);
        secondPage.Entries[0].Id.Should().NotBe(firstPage.Entries[0].Id);
        secondPage.Entries[0].Id.Should().NotBe(firstPage.Entries[1].Id);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_empty_page_for_invalid_cursor()
    {
        var page = await _service.GetHistoryAsync(_userId, 50, "not-a-valid-cursor", CancellationToken.None);

        page.Entries.Should().BeEmpty();
    }

    private async Task SeedBalance(int balance)
    {
        var user = await _dbContext.Users.FindAsync(_userId);
        _dbContext.Entry(user!).CurrentValues["CreditBalance"] = balance;
        await _dbContext.SaveChangesAsync();
    }
}
