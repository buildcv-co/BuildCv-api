using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class AccreditWelcomeHandlerTests
{
    private readonly TestCreditLedger _ledger = new();
    private readonly AccreditWelcomeHandler _handler;

    public AccreditWelcomeHandlerTests()
    {
        _handler = new AccreditWelcomeHandler(_ledger);
    }

    [Fact]
    public async Task HandleAsync_grants_three_credits_on_first_signup()
    {
        var userId = Guid.NewGuid();

        var entry = await _handler.HandleAsync(
            new AccreditWelcomeCommand { UserId = userId },
            CancellationToken.None);

        entry.Delta.Should().Be(3);
        entry.BalanceAfter.Should().Be(3);
        entry.Reason.Should().Be(CreditLedgerReason.Welcome);
        entry.Reference.Should().Be($"welcome:{userId}");
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_on_replayed_signup()
    {
        var userId = Guid.NewGuid();

        var first = await _handler.HandleAsync(
            new AccreditWelcomeCommand { UserId = userId },
            CancellationToken.None);

        var second = await _handler.HandleAsync(
            new AccreditWelcomeCommand { UserId = userId },
            CancellationToken.None);

        second.Id.Should().Be(first.Id);
        second.BalanceAfter.Should().Be(3);

        var balance = await _ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_adds_to_existing_balance()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 7);

        var entry = await _handler.HandleAsync(
            new AccreditWelcomeCommand { UserId = userId },
            CancellationToken.None);

        entry.BalanceAfter.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_does_not_affect_other_users()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _ledger.SeedBalance(bob, 2);

        await _handler.HandleAsync(
            new AccreditWelcomeCommand { UserId = alice },
            CancellationToken.None);

        var bobBalance = await _ledger.GetBalanceAsync(bob, CancellationToken.None);
        bobBalance.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_records_metadata_describing_purpose()
    {
        var userId = Guid.NewGuid();

        var entry = await _handler.HandleAsync(
            new AccreditWelcomeCommand { UserId = userId },
            CancellationToken.None);

        entry.Metadata.Should().NotBeNullOrEmpty();
        entry.Metadata.Should().Contain("Welcome");
    }
}
