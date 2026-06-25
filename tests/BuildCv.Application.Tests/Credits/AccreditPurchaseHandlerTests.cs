using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class AccreditPurchaseHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly AccreditPurchaseHandler _handler;

    public AccreditPurchaseHandlerTests()
    {
        _handler = new AccreditPurchaseHandler(_ledger);
    }

    [Fact]
    public async Task HandleAsync_creates_purchase_entry_with_positive_delta()
    {
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var entry = await _handler.HandleAsync(
            new AccreditPurchaseCommand
            {
                UserId = userId,
                PaymentId = paymentId,
                Credits = 10
            },
            CancellationToken.None);

        entry.Reason.Should().Be(CreditLedgerReason.Purchase);
        entry.Delta.Should().Be(10);
        entry.BalanceAfter.Should().Be(10);
        entry.Reference.Should().Be($"payment:{paymentId}");
        entry.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task HandleAsync_increments_existing_balance()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 5);

        var entry = await _handler.HandleAsync(
            new AccreditPurchaseCommand
            {
                UserId = userId,
                PaymentId = Guid.NewGuid(),
                Credits = 50
            },
            CancellationToken.None);

        entry.BalanceAfter.Should().Be(55);
        entry.Delta.Should().Be(50);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_on_replayed_payment()
    {
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var first = await _handler.HandleAsync(
            new AccreditPurchaseCommand { UserId = userId, PaymentId = paymentId, Credits = 10 },
            CancellationToken.None);

        var second = await _handler.HandleAsync(
            new AccreditPurchaseCommand { UserId = userId, PaymentId = paymentId, Credits = 10 },
            CancellationToken.None);

        second.Id.Should().Be(first.Id);
        second.BalanceAfter.Should().Be(first.BalanceAfter);

        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_stores_metadata_when_provided()
    {
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        const string metadata = """{"wompiTransactionId":"tx-1"}""";

        var entry = await _handler.HandleAsync(
            new AccreditPurchaseCommand
            {
                UserId = userId,
                PaymentId = paymentId,
                Credits = 10,
                Metadata = metadata
            },
            CancellationToken.None);

        entry.Metadata.Should().Be(metadata);
    }

    [Fact]
    public async Task HandleAsync_refuses_zero_credit_purchase()
    {
        var act = async () => await _handler.HandleAsync(
            new AccreditPurchaseCommand
            {
                UserId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                Credits = 0
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Delta*");
    }
}
