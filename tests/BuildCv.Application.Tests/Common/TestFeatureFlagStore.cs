using System.Collections.Concurrent;
using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Tests.Common;

internal sealed class TestFeatureFlagStore : IFeatureFlagStore
{
    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new();
    private readonly ConcurrentBag<FeatureFlagAuditLog> _auditLog = [];

    public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
    {
        _flags.TryGetValue(name, out var flag);
        return Task.FromResult(flag);
    }

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<FeatureFlag> snapshot = _flags.Values
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(snapshot);
    }

    public Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        _flags[flag.Name] = flag;
        return Task.CompletedTask;
    }

    public Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default)
    {
        _auditLog.Add(log);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default)
    {
        var entries = _auditLog
            .Where(l => l.FlagName == flagName)
            .OrderByDescending(l => l.ChangedAt)
            .ThenByDescending(l => l.Id)
            .AsEnumerable();

        if (!string.IsNullOrEmpty(cursor))
        {
            var decoded = CursorCodec.Decode(cursor);
            entries = entries.Where(l =>
                l.ChangedAt.Ticks < decoded.Ticks
                || (l.ChangedAt.Ticks == decoded.Ticks && l.Id.CompareTo(decoded.Id) < 0));
        }

        IReadOnlyList<FeatureFlagAuditLog> page = entries
            .Take(limit)
            .ToList();
        return Task.FromResult(page);
    }

    public IReadOnlyCollection<FeatureFlagAuditLog> AllAuditEntries => _auditLog.ToArray();
}

internal static class CursorCodec
{
    public static string Encode(DateTime changedAt, Guid id)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{changedAt.Ticks}:{id}"));

    public static (long Ticks, Guid Id) Decode(string cursor)
    {
        var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var parts = raw.Split(':', 2);
        return (long.Parse(parts[0]), Guid.Parse(parts[1]));
    }
}