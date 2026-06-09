using System.Net.Http.Json;
using System.Text.Json;
using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Infrastructure.Auth;

public sealed class GoogleOAuthAdapter(
    HttpClient httpClient,
    string clientId,
    string clientSecret) : IAuthenticationService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";

    public async Task<Result<OAuthUserInfo>> ExchangeCodeAsync(
        string provider, string code, string redirectUri, CancellationToken ct = default)
    {
        var tokenRequest = new FormUrlEncodedContent(
        [
            new("code", code),
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("redirect_uri", redirectUri),
            new("grant_type", "authorization_code"),
        ]);

        var tokenResponse = await httpClient.PostAsync(TokenEndpoint, tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return Result.Failure<OAuthUserInfo>(new Error("AUTH/OAUTH_FAILED", "Google token exchange failed"));
        }

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var userInfoResponse = await httpClient.SendAsync(userInfoRequest, ct);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            return Result.Failure<OAuthUserInfo>(new Error("AUTH/OAUTH_FAILED", "Google userinfo fetch failed"));
        }

        var userInfoJson = await userInfoResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var userInfo = new OAuthUserInfo(
            Provider: "google",
            ProviderId: userInfoJson.GetProperty("id").GetString()!,
            Email: userInfoJson.GetProperty("email").GetString()!,
            Name: userInfoJson.GetProperty("name").GetString()!);

        return Result.Success(userInfo);
    }
}
