using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public interface IUserDataStore
{
    void Upsert(User user);

    void Delete(Guid userId);

    Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default);

    Task<Result<User>> GetByProviderAsync(string provider, string providerId, CancellationToken ct = default);

    Task UpsertAsync(User user, CancellationToken ct = default);

    Task DeleteAsync(Guid userId, CancellationToken ct = default);

    Task AddTreatmentLogAsync(DataTreatmentLog log, CancellationToken ct = default);

    Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default);
}
