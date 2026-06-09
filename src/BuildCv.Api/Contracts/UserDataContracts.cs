namespace BuildCv.Api.Contracts;

public sealed record UserDataResponse(
    Guid UserId,
    string Provider,
    string Email,
    string Name,
    DateTime CreatedAt,
    DateTime LastLoginAt);
