using BuildCv.Domain.Credits;

namespace BuildCv.Application.Features.Credits;

public interface ICreditConsumptionService
{
    Task<CreditConsumeResult> ConsumeForAdaptAsync(
        Guid userId,
        Guid adaptRequestId,
        CancellationToken ct);

    Task RefundConsumptionAsync(
        Guid userId,
        Guid adaptRequestId,
        CancellationToken ct);

    Task<CreditBalanceView> GetBalanceAsync(Guid userId, CancellationToken ct);

    Task<CreditHistoryPage> GetHistoryAsync(
        Guid userId,
        int limit,
        string? cursor,
        CancellationToken ct);
}

public sealed record CreditConsumeResult(bool Success, int BalanceAfter, string? ErrorCode)
{
    public static CreditConsumeResult Insufficient(int balance) =>
        new(false, balance, "CREDIT/INSUFFICIENT");
}

public sealed record CreditBalanceView
{
    public int Balance { get; }
    public int RecentConsumption { get; }

    public CreditBalanceView(int balance, int recentConsumption)
    {
        if (balance < 0)
        {
            throw new ArgumentException("Balance must be non-negative.", nameof(balance));
        }

        if (recentConsumption < 0)
        {
            throw new ArgumentException("RecentConsumption must be non-negative.", nameof(recentConsumption));
        }

        Balance = balance;
        RecentConsumption = recentConsumption;
    }
}

public sealed record CreditHistoryPage(
    IReadOnlyList<CreditLedgerEntryDto> Entries,
    string? NextCursor);

public sealed record CreditLedgerEntryDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public CreditLedgerReason Reason { get; init; }
    public string Reference { get; init; } = "";
    public int Delta { get; init; }
    public int BalanceAfter { get; init; }
    public string? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }

    public static CreditLedgerEntryDto From(CreditLedgerEntry entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        Reason = entry.Reason,
        Reference = entry.Reference,
        Delta = entry.Delta,
        BalanceAfter = entry.BalanceAfter,
        Metadata = entry.Metadata,
        CreatedAt = entry.CreatedAt
    };
}
