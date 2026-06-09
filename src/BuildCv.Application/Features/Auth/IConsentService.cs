using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public interface IConsentService
{
    Task<Result<ConsentRecord>> GrantAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<Result> RevokeAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<bool> HasActiveConsentAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetConsentHistoryAsync(Guid userId, CancellationToken ct = default);
}
