using BuildCv.Domain.Credits;
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Features.Credits;

public sealed class AccreditPurchaseHandler(ICreditLedger ledger)
{
    public async Task<CreditLedgerEntry> HandleAsync(AccreditPurchaseCommand command, CancellationToken ct)
    {
        var balance = await ledger.GetBalanceAsync(command.UserId, ct);
        var newBalance = balance + command.Credits;

        return await ledger.AccreditAsync(
            userId: command.UserId,
            reason: CreditLedgerReason.Purchase,
            reference: $"payment:{command.PaymentId}",
            delta: command.Credits,
            balanceAfter: newBalance,
            metadata: command.Metadata,
            ct: ct);
    }

    public async Task<CreditLedgerEntry> HandleAsync(
        Guid userId,
        SubscriptionPlan plan,
        string reference,
        int credits,
        string? metadata,
        CancellationToken ct)
    {
        _ = plan;
        var balance = await ledger.GetBalanceAsync(userId, ct);
        var newBalance = balance + credits;

        return await ledger.AccreditAsync(
            userId: userId,
            reason: CreditLedgerReason.Purchase,
            reference: reference,
            delta: credits,
            balanceAfter: newBalance,
            metadata: metadata,
            ct: ct);
    }
}

public sealed record AccreditPurchaseCommand
{
    public Guid UserId { get; init; }
    public Guid PaymentId { get; init; }
    public int Credits { get; init; }
    public string? Metadata { get; init; }
}
