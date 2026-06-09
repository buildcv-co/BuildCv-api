namespace BuildCv.Application.Features.Auth;

public sealed record GoogleOAuthCallbackCommand(string Code, string RedirectUri);
