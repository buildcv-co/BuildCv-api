namespace BuildCv.Application.Features.Auth;

public sealed record HasActiveConsentQuery(Guid UserId, string Purpose);
