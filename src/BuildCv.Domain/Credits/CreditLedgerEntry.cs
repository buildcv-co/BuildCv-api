namespace BuildCv.Domain.Credits;

public sealed record CreditLedgerEntry
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public CreditLedgerReason Reason { get; init; }
    public string Reference { get; init; } = "";
    public int Delta { get; init; }
    public int BalanceAfter { get; init; }
    public string? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }

    public static CreditLedgerEntry Create(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        int delta,
        int balanceAfter,
        string? metadata = null,
        DateTime? createdAt = null)
    {
        if (delta == 0)
        {
            throw new ArgumentException("Delta must be non-zero (no zero-delta ledger entries).", nameof(delta));
        }

        if (balanceAfter < 0)
        {
            throw new ArgumentException("BalanceAfter must be non-negative.", nameof(balanceAfter));
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Reference must be a non-empty idempotency key.", nameof(reference));
        }

        return new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Reason = reason,
            Reference = reference,
            Delta = delta,
            BalanceAfter = balanceAfter,
            Metadata = metadata,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }
}
