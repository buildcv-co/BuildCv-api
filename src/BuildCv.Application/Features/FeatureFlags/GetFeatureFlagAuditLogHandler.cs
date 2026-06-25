using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Features.FeatureFlags;

public sealed class GetFeatureFlagAuditLogHandler(IFeatureFlagStore store)
{
    public const int MaxLimit = 200;
    public const int DefaultLimit = 50;

    public async Task<(IReadOnlyList<FeatureFlagAuditLog> Entries, string? NextCursor)> HandleAsync(
        string flagName, int? limit, string? cursor, CancellationToken ct = default)
    {
        var clampedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var entries = await store.GetAuditLogAsync(flagName, clampedLimit, cursor, ct);
        string? nextCursor = entries.Count == clampedLimit
            ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{entries[^1].ChangedAt.Ticks}:{entries[^1].Id}"))
            : null;
        return (entries, nextCursor);
    }
}
