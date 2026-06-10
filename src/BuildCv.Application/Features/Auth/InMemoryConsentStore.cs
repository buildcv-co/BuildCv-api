using System.Collections.Concurrent;
using BuildCv.Domain.Auth;

namespace BuildCv.Application.Features.Auth;

public sealed class InMemoryConsentStore : IConsentStore
{
    private readonly ConcurrentDictionary<(Guid UserId, string Purpose), ConsentRecord> _active = new();
    private readonly ConcurrentBag<ConsentRecord> _auditTrail = new();

    public void Add(ConsentRecord record)
    {
        _active[(record.UserId, record.Purpose)] = record;
        _auditTrail.Add(record);
    }

    public Task AddAsync(ConsentRecord record, CancellationToken ct = default)
    {
        Add(record);
        return Task.CompletedTask;
    }

    public void RevokeAll(Guid userId, DateTime revokedAt)
    {
        var keys = _active.Keys.Where(k => k.UserId == userId).ToList();
        foreach (var key in keys)
        {
            if (_active.TryRemove(key, out var record))
            {
                var revoked = record with { RevokedAt = revokedAt };
                _auditTrail.Add(revoked);
            }
        }
    }

    public Task RevokeAllAsync(Guid userId, DateTime revokedAt, CancellationToken ct = default)
    {
        RevokeAll(userId, revokedAt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConsentRecord>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var result = _auditTrail.Where(r => r.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<ConsentRecord>>(result);
    }

    public Task<ConsentRecord?> GetActiveAsync(Guid userId, string purpose, CancellationToken ct = default)
    {
        return _active.TryGetValue((userId, purpose), out var record) && record.IsValid
            ? Task.FromResult<ConsentRecord?>(record)
            : Task.FromResult<ConsentRecord?>(null);
    }

    public Task<ConsentRecord?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default)
    {
        return _active.TryGetValue((userId, purpose), out var record)
            ? Task.FromResult<ConsentRecord?>(record)
            : Task.FromResult<ConsentRecord?>(null);
    }
}
