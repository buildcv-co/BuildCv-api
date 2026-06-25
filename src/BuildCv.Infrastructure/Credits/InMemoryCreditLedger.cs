using System.Collections.Concurrent;
using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;

namespace BuildCv.Infrastructure.Credits;

public sealed class InMemoryCreditLedger : ICreditLedger
{
    private readonly ConcurrentDictionary<(Guid UserId, CreditLedgerReason Reason, string Reference), CreditLedgerEntry> _entries = new();
    private readonly ConcurrentDictionary<Guid, int> _balances = new();

    public IReadOnlyList<CreditLedgerEntry> AllEntries => _entries.Values.OrderBy(e => e.CreatedAt).ToList();

    public Task<CreditLedgerEntry> AccreditAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        int delta,
        int balanceAfter,
        string? metadata,
        CancellationToken ct)
    {
        if (delta == 0)
        {
            throw new ArgumentException("Delta must be non-zero.", nameof(delta));
        }

        if (balanceAfter < 0)
        {
            throw new InvalidOperationException(
                $"Accreditation would set balance to {balanceAfter} (negative).");
        }

        var key = (userId, reason, reference);
        if (_entries.TryGetValue(key, out var existing))
        {
            return Task.FromResult(existing);
        }

        var entry = CreditLedgerEntry.Create(
            userId: userId,
            reason: reason,
            reference: reference,
            delta: delta,
            balanceAfter: balanceAfter,
            metadata: metadata,
            createdAt: DateTime.UtcNow);

        if (!_entries.TryAdd(key, entry))
        {
            return Task.FromResult(_entries[key]);
        }

        _balances.AddOrUpdate(userId, delta, (_, current) => current + delta);
        return Task.FromResult(entry);
    }

    public Task<CreditLedgerEntry?> FindByReferenceAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        CancellationToken ct)
    {
        _entries.TryGetValue((userId, reason, reference), out var entry);
        return Task.FromResult(entry);
    }

    public Task<int> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        _balances.TryGetValue(userId, out var balance);
        return Task.FromResult(balance);
    }

    public Task<IReadOnlyList<CreditLedgerEntry>> GetHistoryAsync(
        Guid userId,
        int limit,
        CreditCursorPosition? before,
        CancellationToken ct)
    {
        IReadOnlyList<CreditLedgerEntry> page = _entries.Values
            .Where(e => e.UserId == userId)
            .Where(e => before is null
                || e.CreatedAt < before.Value.CreatedAt
                || (e.CreatedAt == before.Value.CreatedAt && e.Id.CompareTo(before.Value.Id) < 0))
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(limit)
            .ToList();

        return Task.FromResult(page);
    }

    public Task<int> CountConsumptionsSinceAsync(Guid userId, DateTime since, CancellationToken ct)
    {
        var count = _entries.Values.Count(e =>
            e.UserId == userId &&
            e.Reason == CreditLedgerReason.Consumption &&
            e.CreatedAt >= since);

        return Task.FromResult(count);
    }

    public void SeedBalance(Guid userId, int balance) => _balances[userId] = balance;

    public void RemoveAllForUser(Guid userId)
    {
        foreach (var key in _entries.Keys.Where(k => k.UserId == userId).ToList())
        {
            _entries.TryRemove(key, out _);
        }

        _balances.TryRemove(userId, out _);
    }
}
