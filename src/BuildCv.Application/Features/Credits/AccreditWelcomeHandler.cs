using BuildCv.Domain.Credits;

namespace BuildCv.Application.Features.Credits;

public sealed class AccreditWelcomeHandler(ICreditLedger ledger)
{
    public const int WelcomeCredits = 3;

    public async Task<CreditLedgerEntry> HandleAsync(AccreditWelcomeCommand command, CancellationToken ct)
    {
        var balance = await ledger.GetBalanceAsync(command.UserId, ct);
        var newBalance = balance + WelcomeCredits;

        return await ledger.AccreditAsync(
            userId: command.UserId,
            reason: CreditLedgerReason.Welcome,
            reference: $"welcome:{command.UserId}",
            delta: WelcomeCredits,
            balanceAfter: newBalance,
            metadata: "Welcome credits on first OAuth signup",
            ct: ct);
    }
}

public sealed record AccreditWelcomeCommand
{
    public Guid UserId { get; init; }
}
