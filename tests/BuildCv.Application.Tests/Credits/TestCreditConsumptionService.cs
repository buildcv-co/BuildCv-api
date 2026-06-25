using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;

namespace BuildCv.Application.Tests.Credits;

internal sealed class TestCreditConsumptionService : ICreditConsumptionService
{
    private readonly TestCreditLedger _ledger;

    public TestCreditConsumptionService(TestCreditLedger ledger)
    {
        _ledger = ledger;
    }

    public async Task<CreditConsumeResult> ConsumeForAdaptAsync(
        Guid userId,
        Guid adaptRequestId,
        CancellationToken ct)
    {
        var reference = $"adapt:{adaptRequestId}";
        var existing = await _ledger.FindByReferenceAsync(userId, CreditLedgerReason.Consumption, reference, ct);
        if (existing is not null)
        {
            return new CreditConsumeResult(true, existing.BalanceAfter, null);
        }

        var balance = await _ledger.GetBalanceAsync(userId, ct);
        if (balance < 1)
        {
            return CreditConsumeResult.Insufficient(balance);
        }

        var entry = await _ledger.AccreditAsync(
            userId: userId,
            reason: CreditLedgerReason.Consumption,
            reference: reference,
            delta: -1,
            balanceAfter: balance - 1,
            metadata: null,
            ct: ct);

        return new CreditConsumeResult(true, entry.BalanceAfter, null);
    }

    public async Task RefundConsumptionAsync(Guid userId, Guid adaptRequestId, CancellationToken ct)
    {
        var consumeReference = $"adapt:{adaptRequestId}";
        var originalConsume = await _ledger.FindByReferenceAsync(userId, CreditLedgerReason.Consumption, consumeReference, ct);
        if (originalConsume is null)
        {
            throw new InvalidOperationException(
                $"Cannot refund: no prior Consumption entry for adapt request {adaptRequestId}.");
        }

        var refundReference = $"adapt:{adaptRequestId}:refund";
        var existingRefund = await _ledger.FindByReferenceAsync(userId, CreditLedgerReason.Refund, refundReference, ct);
        if (existingRefund is not null)
        {
            return;
        }

        var balance = await _ledger.GetBalanceAsync(userId, ct);
        await _ledger.AccreditAsync(
            userId: userId,
            reason: CreditLedgerReason.Refund,
            reference: refundReference,
            delta: 1,
            balanceAfter: balance + 1,
            metadata: $"refund of {consumeReference}",
            ct: ct);
    }

    public async Task<CreditBalanceView> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        var balance = await _ledger.GetBalanceAsync(userId, ct);
        var since = DateTime.UtcNow.AddDays(-7);
        var recent = await _ledger.CountConsumptionsSinceAsync(userId, since, ct);
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

        var entries = await _ledger.GetHistoryAsync(userId, pageSize, before, ct);

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
