using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public interface IRefreshTokenStore
{
    Task<string> CreateAsync(Guid userId, CancellationToken ct = default);
    Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
