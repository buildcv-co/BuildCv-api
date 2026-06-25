using BuildCv.Domain.Credits;

namespace BuildCv.Application.Features.Credits;

public interface ICreditLedger
{
    Task<CreditLedgerEntry> AccreditAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        int delta,
        int balanceAfter,
        string? metadata,
        CancellationToken ct);

    Task<CreditLedgerEntry?> FindByReferenceAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        CancellationToken ct);

    Task<int> GetBalanceAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<CreditLedgerEntry>> GetHistoryAsync(
        Guid userId,
        int limit,
        CreditCursorPosition? before,
        CancellationToken ct);

    Task<int> CountConsumptionsSinceAsync(Guid userId, DateTime since, CancellationToken ct);
}
