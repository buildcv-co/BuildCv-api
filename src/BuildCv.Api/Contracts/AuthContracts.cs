namespace BuildCv.Api.Contracts;

public sealed record OAuthCallbackRequest(string Code, string? State = null);

public sealed record TokenResponse(string AccessToken, string RefreshToken, BuildCv.Application.Features.Auth.UserProfileResponse User);

public sealed record ConsentRequest(string Purpose);

public sealed record RectifyUserDataRequest(string? Email, string? Name);

public sealed record PrivacyPolicyResponse(
    int Version,
    string Content,
    DateTime EffectiveDate,
    IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> Purposes);
