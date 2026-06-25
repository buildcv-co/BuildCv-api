using System.Collections.Concurrent;
using System.Text;
using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class InMemoryFeatureFlagStore : IFeatureFlagStore
{
    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new();
    private readonly ConcurrentBag<FeatureFlagAuditLog> _auditLog = [];

    public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _flags.TryGetValue(name, out var flag);
        return Task.FromResult(flag);
    }

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<FeatureFlag> snapshot = _flags.Values
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(snapshot);
    }

    public Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _flags[flag.Name] = flag;
        return Task.CompletedTask;
    }

    public Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _auditLog.Add(log);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IEnumerable<FeatureFlagAuditLog> entries = _auditLog
            .Where(l => l.FlagName == flagName)
            .OrderByDescending(l => l.ChangedAt)
            .ThenByDescending(l => l.Id);

        if (TryDecodeCursor(cursor, out var cursorAt, out var cursorId))
        {
            entries = entries.Where(l =>
                l.ChangedAt.Ticks < cursorAt.Ticks
                || (l.ChangedAt.Ticks == cursorAt.Ticks && l.Id.CompareTo(cursorId) < 0));
        }

        IReadOnlyList<FeatureFlagAuditLog> page = entries
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
        return Task.FromResult(page);
    }

    private static bool TryDecodeCursor(string? cursor, out DateTime at, out Guid id)
    {
        at = default;
        id = default;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split(':', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            at = new DateTime(long.Parse(parts[0]), DateTimeKind.Utc);
            id = Guid.Parse(parts[1]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
