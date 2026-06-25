using BuildCv.Domain.Credits;

namespace BuildCv.Application.Features.Credits;

public sealed class GrantManualCreditHandler(ICreditLedger ledger)
{
    public async Task<CreditLedgerEntry> HandleAsync(GrantManualCreditCommand command, CancellationToken ct)
    {
        if (command.Delta == 0)
        {
            throw new ArgumentException("Delta must be non-zero.", nameof(command.Delta));
        }

        var balance = await ledger.GetBalanceAsync(command.UserId, ct);
        var newBalance = balance + command.Delta;
        if (newBalance < 0)
        {
            throw new InvalidOperationException(
                $"Manual adjustment would make balance negative ({balance} + {command.Delta} = {newBalance}).");
        }

        var reference = string.IsNullOrWhiteSpace(command.Reference)
            ? $"admin:{command.AdminId}:{DateTime.UtcNow.Ticks}"
            : command.Reference;

        return await ledger.AccreditAsync(
            userId: command.UserId,
            reason: CreditLedgerReason.ManualAdjustment,
            reference: reference,
            delta: command.Delta,
            balanceAfter: newBalance,
            metadata: command.Reason,
            ct: ct);
    }
}

public sealed record GrantManualCreditCommand
{
    public Guid UserId { get; init; }
    public Guid AdminId { get; init; }
    public int Delta { get; init; }
    public string? Reason { get; init; }
    public string? Reference { get; init; }
}
