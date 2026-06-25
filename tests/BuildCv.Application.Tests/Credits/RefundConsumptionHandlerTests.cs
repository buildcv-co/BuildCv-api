using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class RefundConsumptionHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly TestCreditConsumptionService _service;
    private readonly RefundConsumptionHandler _handler;

    public RefundConsumptionHandlerTests()
    {
        _service = new TestCreditConsumptionService(_ledger);
        _handler = new RefundConsumptionHandler(_service);
    }

    [Fact]
    public async Task HandleAsync_restores_credit_after_consumption()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 3);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(userId, adaptRequestId, CancellationToken.None);
        var balanceAfterConsume = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balanceAfterConsume.Should().Be(2);

        await _handler.HandleAsync(
            new RefundConsumptionCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        var balanceAfterRefund = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balanceAfterRefund.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_throws_when_no_prior_consumption()
    {
        var userId = Guid.NewGuid();
        var adaptRequestId = Guid.NewGuid();

        var act = async () => await _handler.HandleAsync(
            new RefundConsumptionCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Consumption*");
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_on_replayed_refund()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 3);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(userId, adaptRequestId, CancellationToken.None);

        await _handler.HandleAsync(
            new RefundConsumptionCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        await _handler.HandleAsync(
            new RefundConsumptionCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_writes_refund_ledger_entry_with_positive_delta()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 3);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(userId, adaptRequestId, CancellationToken.None);
        await _handler.HandleAsync(
            new RefundConsumptionCommand { UserId = userId, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        var refund = await _ledger.FindByReferenceAsync(
            userId, CreditLedgerReason.Refund, $"adapt:{adaptRequestId}:refund", CancellationToken.None);

        refund.Should().NotBeNull();
        refund!.Delta.Should().Be(1);
        refund.BalanceAfter.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_does_not_affect_other_users()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _ledger.SeedBalance(alice, 3);
        _ledger.SeedBalance(bob, 2);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(alice, adaptRequestId, CancellationToken.None);
        await _handler.HandleAsync(
            new RefundConsumptionCommand { UserId = alice, AdaptRequestId = adaptRequestId },
            CancellationToken.None);

        var aliceBalance = await _ledger.GetBalanceAsync(alice, CancellationToken.None);
        var bobBalance = await _ledger.GetBalanceAsync(bob, CancellationToken.None);
        aliceBalance.Should().Be(3);
        bobBalance.Should().Be(2);
    }
}
