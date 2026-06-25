using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Credits;

public sealed class EfCreditLedger(BuildCvDbContext db, ILogger<EfCreditLedger> logger) : ICreditLedger
{
    private const int MaxRetries = 3;

    public async Task<CreditLedgerEntry> AccreditAsync(
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

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Reference must be a non-empty idempotency key.", nameof(reference));
        }

        if (balanceAfter < 0)
        {
            throw new InvalidOperationException(
                $"BalanceAfter must be non-negative (got {balanceAfter} for user {userId}).");
        }

        var existing = await FindByReferenceAsync(userId, reason, reference, ct);
        if (existing is not null)
        {
            return existing;
        }

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new InvalidOperationException($"User {userId} not found.");

            var currentBalance = user.CreditBalance;
            if (currentBalance + delta < 0)
            {
                throw new InvalidOperationException(
                    $"Credit operation would make balance negative (current={currentBalance}, delta={delta}).");
            }

            var actualBalanceAfter = currentBalance + delta;
            if (actualBalanceAfter != balanceAfter)
            {
                throw new InvalidOperationException(
                    $"Stale balance: caller reported {balanceAfter} but actual is {actualBalanceAfter} for user {userId}.");
            }

            var entry = CreditLedgerEntry.Create(
                userId: userId,
                reason: reason,
                reference: reference,
                delta: delta,
                balanceAfter: actualBalanceAfter,
                metadata: metadata);

            db.CreditLedgerEntries.Add(entry);
            db.Entry(user).CurrentValues["CreditBalance"] = actualBalanceAfter;

            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return entry;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                logger.LogWarning(
                    "Concurrency conflict on attempt {Attempt} for user {UserId} reference {Reference} — retrying",
                    attempt, userId, reference);
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (attempt < MaxRetries && IsUniqueViolation(ex))
            {
                logger.LogWarning(
                    "Unique violation on attempt {Attempt} for user {UserId} reference {Reference} — retrying",
                    attempt, userId, reference);
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        var afterRetry = await FindByReferenceAsync(userId, reason, reference, ct)
            ?? throw new InvalidOperationException(
                $"AccreditAsync failed after {MaxRetries} attempts for user {userId} reference {reference}.");
        return afterRetry;
    }

    public async Task<CreditLedgerEntry?> FindByReferenceAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        CancellationToken ct)
    {
        return await db.CreditLedgerEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Reason == reason && e.Reference == reference, ct);
    }

    public async Task<int> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.CreditBalance ?? 0;
    }

    public async Task<IReadOnlyList<CreditLedgerEntry>> GetHistoryAsync(
        Guid userId,
        int limit,
        CreditCursorPosition? before,
        CancellationToken ct)
    {
        var query = db.CreditLedgerEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        if (before is not null)
        {
            query = query.Where(e =>
                e.CreatedAt < before.Value.CreatedAt
                || (e.CreatedAt == before.Value.CreatedAt && e.Id.CompareTo(before.Value.Id) < 0));
        }

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<int> CountConsumptionsSinceAsync(Guid userId, DateTime since, CancellationToken ct)
    {
        return db.CreditLedgerEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId
                && e.Reason == CreditLedgerReason.Consumption
                && e.CreatedAt >= since, ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
}
