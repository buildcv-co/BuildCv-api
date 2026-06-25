using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Credits;

public sealed class InMemoryCreditConsumptionService(
    ICreditLedger ledger,
    ILogger<InMemoryCreditConsumptionService>? logger = null) : ICreditConsumptionService
{
    public async Task<CreditConsumeResult> ConsumeForAdaptAsync(
        Guid userId,
        Guid adaptRequestId,
        CancellationToken ct)
    {
        var reference = $"adapt:{adaptRequestId}";

        var existing = await ledger.FindByReferenceAsync(userId, CreditLedgerReason.Consumption, reference, ct);
        if (existing is not null)
        {
            return new CreditConsumeResult(true, existing.BalanceAfter, null);
        }

        var balance = await ledger.GetBalanceAsync(userId, ct);
        if (balance < 1)
        {
            logger?.LogInformation("Credit consumption rejected for user {UserId}: balance=0 (adapt={AdaptRequestId})",
                userId, adaptRequestId);
            return CreditConsumeResult.Insufficient(balance);
        }

        try
        {
            var entry = await ledger.AccreditAsync(
                userId: userId,
                reason: CreditLedgerReason.Consumption,
                reference: reference,
                delta: -1,
                balanceAfter: balance - 1,
                metadata: null,
                ct: ct);

            return new CreditConsumeResult(true, entry.BalanceAfter, null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("negative"))
        {
            var currentBalance = await ledger.GetBalanceAsync(userId, ct);
            logger?.LogInformation("Credit consumption lost race for user {UserId}: balance={Balance} (adapt={AdaptRequestId})",
                userId, currentBalance, adaptRequestId);
            return CreditConsumeResult.Insufficient(currentBalance);
        }
    }

    public async Task RefundConsumptionAsync(
        Guid userId,
        Guid adaptRequestId,
        CancellationToken ct)
    {
        var consumeReference = $"adapt:{adaptRequestId}";
        var refundReference = $"adapt:{adaptRequestId}:refund";

        var originalConsume = await ledger.FindByReferenceAsync(userId, CreditLedgerReason.Consumption, consumeReference, ct)
            ?? throw new InvalidOperationException(
                $"Cannot refund: no prior Consumption entry for adapt request {adaptRequestId}.");

        var existingRefund = await ledger.FindByReferenceAsync(userId, CreditLedgerReason.Refund, refundReference, ct);
        if (existingRefund is not null)
        {
            return;
        }

        var balance = await ledger.GetBalanceAsync(userId, ct);
        var newBalance = balance + 1;

        await ledger.AccreditAsync(
            userId: userId,
            reason: CreditLedgerReason.Refund,
            reference: refundReference,
            delta: 1,
            balanceAfter: newBalance,
            metadata: System.Text.Json.JsonSerializer.Serialize(new { originalReference = consumeReference }),
            ct: ct);

        logger?.LogInformation("Credit consumption refunded for user {UserId} adapt={AdaptRequestId} (consumeDelta={Delta})",
            userId, adaptRequestId, originalConsume.Delta);
    }

    public async Task<CreditBalanceView> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        var balance = await ledger.GetBalanceAsync(userId, ct);
        var since = DateTime.UtcNow.AddDays(-7);
        var recent = await ledger.CountConsumptionsSinceAsync(userId, since, ct);
        return new CreditBalanceView(balance, recent);
    }

    public async Task<CreditHistoryPage> GetHistoryAsync(
        Guid userId,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        var actualLimit = Math.Clamp(limit, 1, 200);
        var before = CreditCursor.Decode(cursor);
        var pageSize = actualLimit + 1;

        var entries = await ledger.GetHistoryAsync(userId, pageSize, before, ct);

        var hasMore = entries.Count > actualLimit;
        var page = hasMore ? entries.Take(actualLimit).ToList() : entries.ToList();

        string? nextCursor = null;
        if (hasMore)
        {
            var last = page[^1];
            nextCursor = CreditCursor.Encode(last.CreatedAt, last.Id);
        }

        return new CreditHistoryPage(
            page.Select(CreditLedgerEntryDto.From).ToList(),
            nextCursor);
    }
}
