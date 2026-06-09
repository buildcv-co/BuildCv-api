using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Infrastructure.Auth;

public sealed class CompositeOAuthAdapter(
    GoogleOAuthAdapter googleAdapter,
    LinkedInOAuthAdapter linkedinAdapter) : IAuthenticationService
{
    public Task<Result<OAuthUserInfo>> ExchangeCodeAsync(
        string provider, string code, string redirectUri, CancellationToken ct = default)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => googleAdapter.ExchangeCodeAsync(provider, code, redirectUri, ct),
            "linkedin" => linkedinAdapter.ExchangeCodeAsync(provider, code, redirectUri, ct),
            _ => Task.FromResult(Result.Failure<OAuthUserInfo>(
                new Error("AUTH/OAUTH_FAILED", $"Unsupported OAuth provider: {provider}"))),
        };
    }
}
