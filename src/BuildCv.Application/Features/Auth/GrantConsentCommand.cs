namespace BuildCv.Application.Features.Auth;

public sealed record GrantConsentCommand(Guid UserId, string Purpose, int PolicyVersion);
