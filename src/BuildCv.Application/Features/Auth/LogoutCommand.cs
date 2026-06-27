namespace BuildCv.Application.Features.Auth;

public sealed record LogoutCommand(string? RefreshToken, Guid? UserId);
