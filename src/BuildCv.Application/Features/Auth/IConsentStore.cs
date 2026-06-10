using BuildCv.Domain.Auth;

namespace BuildCv.Application.Features.Auth;

public interface IConsentStore
{
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);
    Task<ConsentRecord?> GetActiveAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<ConsentRecord?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
    Task RevokeAllAsync(Guid userId, DateTime revokedAt, CancellationToken ct = default);
}
