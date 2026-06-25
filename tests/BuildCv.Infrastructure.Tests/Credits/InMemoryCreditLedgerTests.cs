using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Credits;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Credits;

public sealed class InMemoryCreditLedgerTests
{
    private readonly InMemoryCreditLedger _ledger = new();

    [Fact]
    public async Task AccreditAsync_creates_entry_and_updates_balance()
    {
        var userId = Guid.NewGuid();

        var entry = await _ledger.AccreditAsync(
            userId,
            CreditLedgerReason.Purchase,
            "payment:abc",
            10,
            balanceAfter: 10,
            metadata: null,
            CancellationToken.None);

        entry.UserId.Should().Be(userId);
        entry.Delta.Should().Be(10);
        entry.BalanceAfter.Should().Be(10);
        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(10);
    }

    [Fact]
    public async Task AccreditAsync_is_idempotent_on_replay()
    {
        var userId = Guid.NewGuid();

        var first = await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "welcome:1", 3, 3, null, CancellationToken.None);
        var second = await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "welcome:1", 3, 3, null, CancellationToken.None);

        first.Id.Should().Be(second.Id);
        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(3);
    }

    [Fact]
    public async Task AccreditAsync_rejects_zero_delta()
    {
        var act = () => _ledger.AccreditAsync(
            Guid.NewGuid(), CreditLedgerReason.Purchase, "p", 0, 0, null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AccreditAsync_rejects_negative_balance_after()
    {
        var act = () => _ledger.AccreditAsync(
            Guid.NewGuid(), CreditLedgerReason.Purchase, "p", 5, -1, null, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FindByReferenceAsync_returns_null_when_missing()
    {
        var result = await _ledger.FindByReferenceAsync(
            Guid.NewGuid(), CreditLedgerReason.Purchase, "missing", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByReferenceAsync_returns_entry_when_present()
    {
        var userId = Guid.NewGuid();
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Purchase, "payment:1", 10, 10, null, CancellationToken.None);

        var found = await _ledger.FindByReferenceAsync(
            userId, CreditLedgerReason.Purchase, "payment:1", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Reference.Should().Be("payment:1");
    }

    [Fact]
    public async Task GetHistoryAsync_returns_paginated_descending_by_date()
    {
        var userId = Guid.NewGuid();
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "w", 3, 3, null, CancellationToken.None);

        await Task.Delay(5);
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Purchase, "p1", 10, 13, null, CancellationToken.None);

        await Task.Delay(5);
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Consumption, "c1", -1, 12, null, CancellationToken.None);

        var history = await _ledger.GetHistoryAsync(userId, 2, null, CancellationToken.None);

        history.Should().HaveCount(2);
        history[0].Reason.Should().Be(CreditLedgerReason.Consumption);
        history[1].Reason.Should().Be(CreditLedgerReason.Purchase);
    }

    [Fact]
    public async Task CountConsumptionsSinceAsync_counts_only_consumption_in_window()
    {
        var userId = Guid.NewGuid();
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "w", 5, 5, null, CancellationToken.None);
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Consumption, "c1", -1, 4, null, CancellationToken.None);
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Consumption, "c2", -1, 3, null, CancellationToken.None);

        var since = DateTime.UtcNow.AddMinutes(-1);
        var count = await _ledger.CountConsumptionsSinceAsync(userId, since, CancellationToken.None);

        count.Should().Be(2);
    }

    [Fact]
    public async Task SeedBalance_then_GetBalance_returns_seeded_value()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 99);
        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(99);
    }

    [Fact]
    public async Task RemoveAllForUser_clears_entries_and_balance()
    {
        var userId = Guid.NewGuid();
        await _ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "w", 3, 3, null, CancellationToken.None);

        _ledger.RemoveAllForUser(userId);

        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(0);
        _ledger.AllEntries.Should().BeEmpty();
    }
}
