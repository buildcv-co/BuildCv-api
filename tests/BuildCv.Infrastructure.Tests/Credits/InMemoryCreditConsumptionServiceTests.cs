using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Credits;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Credits;

public sealed class InMemoryCreditConsumptionServiceTests
{
    private readonly InMemoryCreditLedger _ledger = new();
    private readonly InMemoryCreditConsumptionService _service;

    public InMemoryCreditConsumptionServiceTests()
    {
        _service = new InMemoryCreditConsumptionService(_ledger);
    }

    [Fact]
    public async Task ConsumeForAdaptAsync_decrements_balance_by_one()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 5);

        var result = await _service.ConsumeForAdaptAsync(userId, Guid.NewGuid(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(4);
    }

    [Fact]
    public async Task ConsumeForAdaptAsync_returns_insufficient_when_balance_zero()
    {
        var userId = Guid.NewGuid();

        var result = await _service.ConsumeForAdaptAsync(userId, Guid.NewGuid(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDIT/INSUFFICIENT");
        result.BalanceAfter.Should().Be(0);
    }

    [Fact]
    public async Task ConsumeForAdaptAsync_is_idempotent_on_replay()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 3);
        var adaptRequestId = Guid.NewGuid();

        var first = await _service.ConsumeForAdaptAsync(userId, adaptRequestId, CancellationToken.None);
        var second = await _service.ConsumeForAdaptAsync(userId, adaptRequestId, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.BalanceAfter.Should().Be(second.BalanceAfter);
        (await _ledger.GetBalanceAsync(userId, CancellationToken.None)).Should().Be(2);
    }

    [Fact]
    public async Task RefundConsumptionAsync_restores_credit()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 3);
        var adaptRequestId = Guid.NewGuid();

        await _service.ConsumeForAdaptAsync(userId, adaptRequestId, CancellationToken.None);
        await _service.RefundConsumptionAsync(userId, adaptRequestId, CancellationToken.None);

        (await _ledger.GetBalanceAsync(userId, CancellationToken.None)).Should().Be(3);
    }

    [Fact]
    public async Task RefundConsumptionAsync_throws_when_no_prior_consume()
    {
        var userId = Guid.NewGuid();

        var act = () => _service.RefundConsumptionAsync(userId, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetBalanceAsync_returns_balance_and_recent_consumption()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 4);
        await _service.ConsumeForAdaptAsync(userId, Guid.NewGuid(), CancellationToken.None);
        await _service.ConsumeForAdaptAsync(userId, Guid.NewGuid(), CancellationToken.None);

        var view = await _service.GetBalanceAsync(userId, CancellationToken.None);

        view.Balance.Should().Be(2);
        view.RecentConsumption.Should().Be(2);
    }

    [Fact]
    public async Task GetHistoryAsync_paginates_with_limit()
    {
        var userId = Guid.NewGuid();
        _ledger.SeedBalance(userId, 30);
        for (var i = 0; i < 30; i++)
        {
            await _ledger.AccreditAsync(
                userId, CreditLedgerReason.Purchase, $"p-{i}", 1, balanceAfter: 0, null, CancellationToken.None);
        }

        var page = await _service.GetHistoryAsync(userId, 10, null, CancellationToken.None);

        page.Entries.Should().HaveCount(10);
        page.NextCursor.Should().NotBeNull();
    }
}
