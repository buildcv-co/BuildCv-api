using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Adapt;

public sealed class RefundMidStreamTests
{
    [Fact]
    public async Task MidStreamFailure_DoesNotIssueRefund()
    {
        var ledger = new InMemoryCreditLedger();
        var service = new InMemoryCreditConsumptionService(ledger);
        var userId = Guid.NewGuid();
        await ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "test:setup", 1, 1, null, CancellationToken.None);

        var simulator = new AdaptSimulator(service);
        await simulator.SimulateMidStreamFailureAsync(userId, CancellationToken.None);

        var balance = await ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(0);

        var history = await ledger.GetHistoryAsync(userId, 10, null, CancellationToken.None);
        history.Should().NotContain(e => e.Reason == CreditLedgerReason.Refund);
        history.Should().ContainSingle(e => e.Reason == CreditLedgerReason.Consumption);
    }

    [Fact]
    public async Task PreStreamFailure_IssuesRefund()
    {
        var ledger = new InMemoryCreditLedger();
        var service = new InMemoryCreditConsumptionService(ledger);
        var userId = Guid.NewGuid();
        await ledger.AccreditAsync(
            userId, CreditLedgerReason.Welcome, "test:setup", 1, 1, null, CancellationToken.None);

        var simulator = new AdaptSimulator(service);
        await simulator.SimulatePreStreamFailureAsync(userId, CancellationToken.None);

        var balance = await ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(1);

        var history = await ledger.GetHistoryAsync(userId, 10, null, CancellationToken.None);
        history.Should().ContainSingle(e => e.Reason == CreditLedgerReason.Refund);
        history.Should().ContainSingle(e => e.Reason == CreditLedgerReason.Consumption);
    }
}

internal sealed class AdaptSimulator(ICreditConsumptionService consumption)
{
    public async Task SimulateMidStreamFailureAsync(Guid userId, CancellationToken ct)
    {
        var adaptRequestId = Guid.NewGuid();
        await consumption.ConsumeForAdaptAsync(userId, adaptRequestId, ct);
        EmitFirstToken();
    }

    public async Task SimulatePreStreamFailureAsync(Guid userId, CancellationToken ct)
    {
        var adaptRequestId = Guid.NewGuid();
        await consumption.ConsumeForAdaptAsync(userId, adaptRequestId, ct);
        await consumption.RefundConsumptionAsync(userId, adaptRequestId, ct);
    }

    private static void EmitFirstToken() { }
}
