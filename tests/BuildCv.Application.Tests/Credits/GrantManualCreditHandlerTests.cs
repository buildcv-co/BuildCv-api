using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class GrantManualCreditHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly GrantManualCreditHandler _handler;

    public GrantManualCreditHandlerTests()
    {
        _handler = new GrantManualCreditHandler(_ledger);
    }

    [Fact]
    public async Task HandleAsync_grants_positive_adjustment_with_unique_admin_reference()
    {
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var entry = await _handler.HandleAsync(
            new GrantManualCreditCommand
            {
                UserId = userId,
                AdminId = adminId,
                Delta = 5,
                Reason = "Customer support credit"
            },
            CancellationToken.None);

        entry.Delta.Should().Be(5);
        entry.BalanceAfter.Should().Be(5);
        entry.Reason.Should().Be(CreditLedgerReason.ManualAdjustment);
        entry.Reference.Should().StartWith($"admin:{adminId}:");
    }

    [Fact]
    public async Task HandleAsync_supports_negative_adjustment()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 5);

        var entry = await _handler.HandleAsync(
            new GrantManualCreditCommand
            {
                UserId = userId,
                AdminId = Guid.NewGuid(),
                Delta = -2,
                Reason = "Refund processed externally"
            },
            CancellationToken.None);

        entry.Delta.Should().Be(-2);
        entry.BalanceAfter.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_throws_when_adjustment_would_make_balance_negative()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 1);

        var act = async () => await _handler.HandleAsync(
            new GrantManualCreditCommand
            {
                UserId = userId,
                AdminId = Guid.NewGuid(),
                Delta = -5,
                Reason = "Bad"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public async Task HandleAsync_throws_when_delta_is_zero()
    {
        var act = async () => await _handler.HandleAsync(
            new GrantManualCreditCommand
            {
                UserId = Guid.NewGuid(),
                AdminId = Guid.NewGuid(),
                Delta = 0,
                Reason = "Nothing"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_uses_explicit_reference_when_provided()
    {
        var userId = Guid.NewGuid();
        const string reference = "support:ticket-42";

        var entry = await _handler.HandleAsync(
            new GrantManualCreditCommand
            {
                UserId = userId,
                AdminId = Guid.NewGuid(),
                Delta = 10,
                Reason = "Promo",
                Reference = reference
            },
            CancellationToken.None);

        entry.Reference.Should().Be(reference);
    }
}
