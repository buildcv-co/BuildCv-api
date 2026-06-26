namespace BuildCv.Application.Features.Auth;

public sealed record WebSignupCommand(string Provider, string ProviderAccountId, string Email, string Name);
