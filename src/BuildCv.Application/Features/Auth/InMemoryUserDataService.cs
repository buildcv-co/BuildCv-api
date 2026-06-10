using System.Collections.Concurrent;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class InMemoryUserDataService : IUserDataService
{
    private readonly IUserDataStore _store;
    private readonly ConcurrentDictionary<(string Provider, string ProviderId), Guid> _providerKeyMap = new();

    public InMemoryUserDataService(IUserDataStore store)
    {
        _store = store;
    }

    public async Task<Result<User>> GetOrCreateAsync(string provider, string providerId, string email, string name, CancellationToken ct = default)
    {
        var key = (provider, providerId);
        if (_providerKeyMap.TryGetValue(key, out var existingId))
        {
            var existingResult = await _store.GetByIdAsync(existingId, ct);
            if (existingResult.IsSuccess)
            {
                var existing = existingResult.Value;
                var updated = existing with
                {
                    Email = email,
                    Name = name,
                    LastLoginAt = DateTime.UtcNow,
                };
                _store.Upsert(updated);
                return Result.Success(updated);
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ProviderId = providerId,
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
        };
        _providerKeyMap[key] = user.Id;
        _store.Upsert(user);
        return Result.Success(user);
    }

    public Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => _store.GetByIdAsync(userId, ct);

    public async Task<Result<User>> UpdateAsync(Guid userId, string? email, string? name, CancellationToken ct = default)
    {
        var userResult = await _store.GetByIdAsync(userId, ct);
        if (userResult.IsFailure)
        {
            return userResult;
        }

        var user = userResult.Value;
        var updated = user with
        {
            Email = email ?? user.Email,
            Name = name ?? user.Name,
            LastLoginAt = DateTime.UtcNow,
        };
        _store.Upsert(updated);
        return Result.Success(updated);
    }

    public Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        _store.Delete(userId);
        return Task.FromResult(Result.Success());
    }

    public Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default)
        => _store.GetTreatmentLogsAsync(userId, ct);
}
