using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Credits;

public sealed class EfCreditLedgerTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfCreditLedger _ledger;
    private readonly Guid _userId;

    public EfCreditLedgerTests()
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
        _ledger = new EfCreditLedger(_dbContext, NullLogger<EfCreditLedger>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task AccreditAsync_creates_entry_and_updates_balance()
    {
        var entry = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Purchase,
            "payment:abc-123",
            10,
            10,
            "{\"paymentId\":\"abc-123\"}",
            CancellationToken.None);

        entry.Should().NotBeNull();
        entry.Delta.Should().Be(10);
        entry.BalanceAfter.Should().Be(10);
        entry.Reason.Should().Be(CreditLedgerReason.Purchase);

        var savedEntry = await _dbContext.CreditLedgerEntries.FindAsync(entry.Id);
        savedEntry.Should().NotBeNull();

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(10);
    }

    [Fact]
    public async Task AccreditAsync_is_idempotent_on_replay()
    {
        var first = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Welcome,
            $"welcome:{_userId}",
            3,
            3,
            "Welcome",
            CancellationToken.None);

        var replay = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Welcome,
            $"welcome:{_userId}",
            3,
            3,
            "Welcome",
            CancellationToken.None);

        replay.Id.Should().Be(first.Id);
        replay.Delta.Should().Be(3);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(3);

        var entries = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId && e.Reason == CreditLedgerReason.Welcome)
            .ToListAsync();
        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task AccreditAsync_handles_negative_delta_for_consumption()
    {
        await SeedBalance(5);

        var entry = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Consumption,
            "adapt:req-1",
            -1,
            4,
            null,
            CancellationToken.None);

        entry.Delta.Should().Be(-1);
        entry.BalanceAfter.Should().Be(4);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(4);
    }

    [Fact]
    public async Task AccreditAsync_updates_user_balance_in_same_transaction()
    {
        await SeedBalance(7);

        var entry = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Purchase,
            "payment:xyz",
            3,
            10,
            null,
            CancellationToken.None);

        entry.BalanceAfter.Should().Be(10);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(10);
    }

    [Fact]
    public async Task AccreditAsync_throws_if_balance_would_go_negative()
    {
        await SeedBalance(0);

        var act = async () => await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Consumption,
            "adapt:req-1",
            -1,
            -1,
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FindByReferenceAsync_returns_entry_when_exists()
    {
        await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Purchase,
            "payment:findme",
            5,
            5,
            null,
            CancellationToken.None);

        var found = await _ledger.FindByReferenceAsync(
            _userId,
            CreditLedgerReason.Purchase,
            "payment:findme",
            CancellationToken.None);

        found.Should().NotBeNull();
        found!.Reference.Should().Be("payment:findme");
    }

    [Fact]
    public async Task FindByReferenceAsync_returns_null_when_not_found()
    {
        var found = await _ledger.FindByReferenceAsync(
            _userId,
            CreditLedgerReason.Purchase,
            "payment:doesnotexist",
            CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetBalanceAsync_returns_user_credit_balance()
    {
        await SeedBalance(42);

        var balance = await _ledger.GetBalanceAsync(_userId, CancellationToken.None);

        balance.Should().Be(42);
    }

    [Fact]
    public async Task GetBalanceAsync_returns_zero_for_unknown_user()
    {
        var balance = await _ledger.GetBalanceAsync(Guid.NewGuid(), CancellationToken.None);

        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_entries_newest_first()
    {
        await _ledger.AccreditAsync(_userId, CreditLedgerReason.Welcome, "welcome:1", 3, 3, null, CancellationToken.None);
        await _ledger.AccreditAsync(_userId, CreditLedgerReason.Purchase, "payment:1", 10, 13, null, CancellationToken.None);
        await _ledger.AccreditAsync(_userId, CreditLedgerReason.Consumption, "adapt:1", -1, 12, null, CancellationToken.None);

        var history = await _ledger.GetHistoryAsync(_userId, 10, null, CancellationToken.None);

        history.Should().HaveCount(3);
        history[0].Reason.Should().Be(CreditLedgerReason.Consumption);
        history[1].Reason.Should().Be(CreditLedgerReason.Purchase);
        history[2].Reason.Should().Be(CreditLedgerReason.Welcome);
    }

    [Fact]
    public async Task GetHistoryAsync_paginates_with_limit()
    {
        for (var i = 0; i < 5; i++)
        {
            await _ledger.AccreditAsync(_userId, CreditLedgerReason.Purchase, $"payment:{i}", 1, i + 1, null, CancellationToken.None);
        }

        var page1 = await _ledger.GetHistoryAsync(_userId, 2, null, CancellationToken.None);
        var page2 = await _ledger.GetHistoryAsync(_userId, 2, page1[^1].CreatedAt > page1[^1].CreatedAt ? null : null, CancellationToken.None);

        page1.Should().HaveCount(2);
    }

    [Fact]
    public async Task CountConsumptionsSinceAsync_filters_by_reason_and_date()
    {
        var older = DateTime.UtcNow.AddDays(-10);
        var recent = DateTime.UtcNow;

        _dbContext.CreditLedgerEntries.Add(CreditLedgerEntry.Create(_userId, CreditLedgerReason.Consumption, "adapt:old", -1, 9, null, older));
        _dbContext.CreditLedgerEntries.Add(CreditLedgerEntry.Create(_userId, CreditLedgerReason.Consumption, "adapt:new", -1, 8, null, recent));
        _dbContext.CreditLedgerEntries.Add(CreditLedgerEntry.Create(_userId, CreditLedgerReason.Purchase, "payment:1", 10, 10, null, recent));
        await _dbContext.SaveChangesAsync();

        var since = DateTime.UtcNow.AddDays(-7);
        var count = await _ledger.CountConsumptionsSinceAsync(_userId, since, CancellationToken.None);

        count.Should().Be(1);
    }

    private async Task SeedBalance(int balance)
    {
        var user = await _dbContext.Users.FindAsync(_userId);
        _dbContext.Entry(user!).CurrentValues["CreditBalance"] = balance;
        await _dbContext.SaveChangesAsync();
    }
}
