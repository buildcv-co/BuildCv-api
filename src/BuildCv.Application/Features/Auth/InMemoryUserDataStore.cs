using System.Collections.Concurrent;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class InMemoryUserDataStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private readonly ConcurrentBag<DataTreatmentLog> _logs = new();

    public InMemoryUserDataStore(User? initialUser = null)
    {
        if (initialUser is not null)
        {
            _users.TryAdd(initialUser.Id, initialUser);
        }
    }

    public void Upsert(User user) => _users[user.Id] = user;

    public Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return _users.TryGetValue(userId, out var user)
            ? Task.FromResult(Result.Success(user))
            : Task.FromResult(Result.Failure<User>(new Error("ARCO/DATA_NOT_FOUND", "User not found")));
    }

    public void Delete(Guid userId) => _users.TryRemove(userId, out _);

    public void AddLog(DataTreatmentLog log) => _logs.Add(log);

    public Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default)
    {
        var result = _logs.Where(l => l.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<DataTreatmentLog>>(result);
    }
}
