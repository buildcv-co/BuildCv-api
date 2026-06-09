namespace BuildCv.Application.Features.Auth;

public sealed record RectifyUserDataCommand(Guid UserId, string? Email, string? Name);
