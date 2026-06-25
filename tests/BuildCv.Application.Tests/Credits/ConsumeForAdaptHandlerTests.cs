using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class ConsumeForAdaptHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly TestCreditConsumptionService _service;
    private readonly ConsumeForAdaptHandler _handler;

    public ConsumeForAdaptHandlerTests()
    {
        _service = new TestCreditConsumptionService(_ledger);
        _handler = new ConsumeForAdaptHandler(_service);
    }

    [Fact]
    public async Task HandleAsync_decrements_balance_by_one_when_balance_positive()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 5);

        var result = await _handler.HandleAsync(
            new ConsumeForAdaptCommand
            {
                UserId = userId,
                AdaptRequestId = Guid.NewGuid()
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(4);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_returns_insufficient_when_balance_zero()
    {
        var userId = Guid.NewGuid();

        var result = await _handler.HandleAsync(
            new ConsumeForAdaptCommand
            {
                UserId = userId,
                AdaptRequestId = Guid.NewGuid()
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDIT/INSUFFICIENT");
        result.BalanceAfter.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_on_replayed_adapt_request()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 5);
        var adaptRequestId = Guid.NewGuid();

        var first = await _handler.HandleAsync(
            new ConsumeForAdaptCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        var second = await _handler.HandleAsync(
            new ConsumeForAdaptCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.BalanceAfter.Should().Be(4);

        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(4);
    }

    [Fact]
    public async Task HandleAsync_writes_consumption_ledger_entry()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 3);
        var adaptRequestId = Guid.NewGuid();

        await _handler.HandleAsync(
            new ConsumeForAdaptCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        var stored = await _ledger.FindByReferenceAsync(
            userId, CreditLedgerReason.Consumption, $"adapt:{adaptRequestId}", CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.Delta.Should().Be(-1);
        stored.BalanceAfter.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_does_not_deduct_when_insufficient()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 0);

        var result = await _handler.HandleAsync(
            new ConsumeForAdaptCommand
            {
                UserId = userId,
                AdaptRequestId = Guid.NewGuid()
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(0);
        _ledger.AllEntries.Should().BeEmpty();
    }
}
