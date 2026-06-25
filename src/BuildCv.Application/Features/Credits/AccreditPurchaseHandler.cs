using BuildCv.Domain.Credits;

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
}

public sealed record AccreditPurchaseCommand
{
    public Guid UserId { get; init; }
    public Guid PaymentId { get; init; }
    public int Credits { get; init; }
    public string? Metadata { get; init; }
}
