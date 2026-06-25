using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class GetCreditHistoryHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly TestCreditConsumptionService _service;
    private readonly GetCreditHistoryHandler _handler;

    public GetCreditHistoryHandlerTests()
    {
        _service = new TestCreditConsumptionService(_ledger);
        _handler = new GetCreditHistoryHandler(_service);
    }

    [Fact]
    public async Task HandleAsync_returns_empty_page_for_new_user()
    {
        var userId = Guid.NewGuid();

        var page = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId },
            CancellationToken.None);

        page.Entries.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_returns_entries_newest_first()
    {
        var userId = Guid.NewGuid();
        await SeedEntry(userId, CreditLedgerReason.Welcome, "welcome:u", 3, 3, daysAgo: 5);
        await SeedEntry(userId, CreditLedgerReason.Consumption, "adapt:a", -1, 2, daysAgo: 1);
        await SeedEntry(userId, CreditLedgerReason.Purchase, "payment:p", 10, 12, daysAgo: 0);

        var page = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId, Limit = 10 },
            CancellationToken.None);

        page.Entries.Should().HaveCount(3);
        page.Entries[0].Reference.Should().Be("payment:p");
        page.Entries[1].Reference.Should().Be("adapt:a");
        page.Entries[2].Reference.Should().Be("welcome:u");
    }

    [Fact]
    public async Task HandleAsync_respects_limit_and_signals_more_pages()
    {
        var userId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await SeedEntry(userId, CreditLedgerReason.Consumption, $"adapt:{i}", -1, 4 - i, daysAgo: i);
        }

        var page = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId, Limit = 2 },
            CancellationToken.None);

        page.Entries.Should().HaveCount(2);
        page.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_paginates_with_cursor()
    {
        var userId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await SeedEntry(userId, CreditLedgerReason.Consumption, $"adapt:{i}", -1, 4 - i, daysAgo: i);
        }

        var first = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId, Limit = 2 },
            CancellationToken.None);
        first.NextCursor.Should().NotBeNullOrEmpty();

        var second = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId, Limit = 2, Cursor = first.NextCursor },
            CancellationToken.None);

        second.Entries.Should().HaveCount(2);
        var firstRefs = first.Entries.Select(e => e.Reference).ToList();
        var secondRefs = second.Entries.Select(e => e.Reference).ToList();
        firstRefs.Should().NotIntersectWith(secondRefs);
    }

    [Fact]
    public async Task HandleAsync_clamps_limit_to_max_200()
    {
        var userId = Guid.NewGuid();
        await SeedEntry(userId, CreditLedgerReason.Welcome, "welcome:u", 3, 3, daysAgo: 0);

        var page = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId, Limit = 10_000 },
            CancellationToken.None);

        page.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_returns_null_next_cursor_when_no_more_pages()
    {
        var userId = Guid.NewGuid();
        await SeedEntry(userId, CreditLedgerReason.Welcome, "welcome:u", 3, 3, daysAgo: 0);

        var page = await _handler.HandleAsync(
            new GetCreditHistoryQuery { UserId = userId, Limit = 50 },
            CancellationToken.None);

        page.Entries.Should().HaveCount(1);
        page.NextCursor.Should().BeNull();
    }

    private async Task SeedEntry(Guid userId, CreditLedgerReason reason, string reference, int delta, int balanceAfter, int daysAgo)
    {
        var when = DateTime.UtcNow.AddDays(-daysAgo);
        var entry = CreditLedgerEntry.Create(
            userId: userId,
            reason: reason,
            reference: reference,
            delta: delta,
            balanceAfter: balanceAfter,
            metadata: null,
            createdAt: when);

        await _ledger.AccreditAsync(userId, reason, reference, delta, balanceAfter, null, CancellationToken.None);
    }
}
