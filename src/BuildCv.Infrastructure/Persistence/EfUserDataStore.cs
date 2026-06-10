using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Persistence;

public sealed class EfUserDataStore(BuildCvDbContext dbContext) : IUserDataStore
{
    public void Upsert(User user)
    {
        var existing = dbContext.Users.Find(user.Id);
        if (existing is not null)
        {
            dbContext.Entry(existing).CurrentValues.SetValues(user);
        }
        else
        {
            dbContext.Users.Add(user);
        }

        dbContext.SaveChanges();
    }

    public void Delete(Guid userId)
    {
        var user = dbContext.Users.Find(userId);
        if (user is not null)
        {
            dbContext.Users.Remove(user);
            dbContext.SaveChanges();
        }
    }

    public async Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FindAsync([userId], ct);
        return user is not null
            ? Result.Success(user)
            : Result.Failure<User>(new Error("ARCO/DATA_NOT_FOUND", "User not found"));
    }

    public async Task<Result<User>> GetByProviderAsync(string provider, string providerId, CancellationToken ct = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Provider == provider && u.ProviderId == providerId, ct);
        return user is not null
            ? Result.Success(user)
            : Result.Failure<User>(new Error("ARCO/DATA_NOT_FOUND", "User not found"));
    }

    public async Task UpsertAsync(User user, CancellationToken ct = default)
    {
        var existing = await dbContext.Users.FindAsync([user.Id], ct);
        if (existing is not null)
        {
            dbContext.Entry(existing).CurrentValues.SetValues(user);
        }
        else
        {
            dbContext.Users.Add(user);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is not null)
        {
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task AddTreatmentLogAsync(DataTreatmentLog log, CancellationToken ct = default)
    {
        dbContext.DataTreatmentLogs.Add(log);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.DataTreatmentLogs
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Timestamp)
            .ToListAsync(ct);
    }
}
