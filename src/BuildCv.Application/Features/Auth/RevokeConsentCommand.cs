namespace BuildCv.Application.Features.Auth;

public sealed record RevokeConsentCommand(Guid UserId, string Purpose);
