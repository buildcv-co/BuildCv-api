namespace BuildCv.Application.Features.Auth;

public sealed record LinkedInOAuthCallbackCommand(string Code, string RedirectUri);
