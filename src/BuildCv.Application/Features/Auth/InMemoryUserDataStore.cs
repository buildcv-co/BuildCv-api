using System.Collections.Concurrent;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class InMemoryUserDataStore : IUserDataStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private readonly ConcurrentDictionary<Guid, bool> _hasPayments = new();
    private readonly ConcurrentBag<DataTreatmentLog> _logs = new();

    public InMemoryUserDataStore(User? initialUser = null)
    {
        if (initialUser is not null)
        {
            _users.TryAdd(initialUser.Id, initialUser);
        }
    }

    public void Upsert(User user) => _users[user.Id] = user;

    public Task UpsertAsync(User user, CancellationToken ct = default)
    {
        Upsert(user);
        return Task.CompletedTask;
    }

    public Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return _users.TryGetValue(userId, out var user)
            ? Task.FromResult(Result.Success(user))
            : Task.FromResult(Result.Failure<User>(new Error("ARCO/DATA_NOT_FOUND", "User not found")));
    }

    public Task<Result<User>> GetByProviderAsync(string provider, string providerId, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => u.Provider == provider && u.ProviderId == providerId);
        return user is not null
            ? Task.FromResult(Result.Success(user))
            : Task.FromResult(Result.Failure<User>(new Error("ARCO/DATA_NOT_FOUND", "User not found")));
    }

    public void Delete(Guid userId) => _users.TryRemove(userId, out _);

    public Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        Delete(userId);
        return Task.CompletedTask;
    }

    public Task<Result> AnonymizeAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_users.TryGetValue(userId, out var existing))
        {
            return Task.FromResult(Result.Failure(new Error("ARCO/DATA_NOT_FOUND", "User not found")));
        }

        _users[userId] = existing with
        {
            Provider = "redacted",
            ProviderId = "redacted",
            Email = "[deleted]@anonymized",
            Name = "[Deleted User]",
        };
        return Task.FromResult(Result.Success());
    }

    public Task<bool> HasPaymentsAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.FromResult(_hasPayments.TryGetValue(userId, out var has) && has);
    }

    public void SeedPayment(Guid userId) => _hasPayments[userId] = true;

    public void AddLog(DataTreatmentLog log) => _logs.Add(log);

    public Task AddTreatmentLogAsync(DataTreatmentLog log, CancellationToken ct = default)
    {
        AddLog(log);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default)
    {
        var result = _logs.Where(l => l.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<DataTreatmentLog>>(result);
    }
}
