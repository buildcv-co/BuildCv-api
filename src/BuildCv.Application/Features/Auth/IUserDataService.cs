using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public interface IUserDataService
{
    Task<Result<User>> GetOrCreateAsync(string provider, string providerId, string email, string name, CancellationToken ct = default);
    Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<User>> UpdateAsync(Guid userId, string? email, string? name, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default);
}
