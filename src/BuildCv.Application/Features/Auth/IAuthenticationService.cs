using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed record OAuthUserInfo(string Provider, string ProviderId, string Email, string Name);

public interface IAuthenticationService
{
    Task<Result<OAuthUserInfo>> ExchangeCodeAsync(string provider, string code, string redirectUri, CancellationToken ct = default);
}
