using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class GetCreditBalanceHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly TestCreditConsumptionService _service;
    private readonly GetCreditBalanceHandler _handler;

    public GetCreditBalanceHandlerTests()
    {
        _service = new TestCreditConsumptionService(_ledger);
        _handler = new GetCreditBalanceHandler(_service);
    }

    [Fact]
    public async Task HandleAsync_returns_balance_with_zero_recent_consumption_for_new_user()
    {
        var userId = Guid.NewGuid();

        var view = await _handler.HandleAsync(
            new GetCreditBalanceQuery { UserId = userId },
            CancellationToken.None);

        view.Balance.Should().Be(0);
        view.RecentConsumption.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_returns_current_balance_after_ledger_activity()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 7);

        var view = await _handler.HandleAsync(
            new GetCreditBalanceQuery { UserId = userId },
            CancellationToken.None);

        view.Balance.Should().Be(7);
    }

    [Fact]
    public async Task HandleAsync_counts_recent_consumptions_within_seven_days()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 5);

        for (var i = 0; i < 3; i++)
        {
            await _service.ConsumeForAdaptAsync(userId, Guid.NewGuid(), CancellationToken.None);
        }

        var view = await _handler.HandleAsync(
            new GetCreditBalanceQuery { UserId = userId },
            CancellationToken.None);

        view.Balance.Should().Be(2);
        view.RecentConsumption.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_does_not_count_non_consumption_entries()
    {
        var userId = Guid.NewGuid();

        await SeedEntry(userId, CreditLedgerReason.Welcome, "welcome:u", 3, 3);
        await SeedEntry(userId, CreditLedgerReason.Purchase, "payment:p", 10, 13);
        await SeedEntry(userId, CreditLedgerReason.Refund, "adapt:r:refund", 1, 14);

        var view = await _handler.HandleAsync(
            new GetCreditBalanceQuery { UserId = userId },
            CancellationToken.None);

        view.Balance.Should().Be(14);
        view.RecentConsumption.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_isolates_users()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _ledger.SeedBalance(alice, 5);
        _ledger.SeedBalance(bob, 1);

        var aliceView = await _handler.HandleAsync(
            new GetCreditBalanceQuery { UserId = alice },
            CancellationToken.None);
        var bobView = await _handler.HandleAsync(
            new GetCreditBalanceQuery { UserId = bob },
            CancellationToken.None);

        aliceView.Balance.Should().Be(5);
        bobView.Balance.Should().Be(1);
    }

    private async Task SeedEntry(Guid userId, CreditLedgerReason reason, string reference, int delta, int balanceAfter) =>
        await _ledger.AccreditAsync(
            userId, reason, reference, delta, balanceAfter, null, CancellationToken.None);
}
