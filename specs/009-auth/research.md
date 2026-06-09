# Design: 009-auth — Authentication & Habeas Data Compliance

## Technical Approach

Add OAuth 2.0 authentication (Google + LinkedIn) and Habeas Data compliance (consent, ARCO rights) to BuildCv-api following Clean Architecture. New ports in Application, adapters in Infrastructure, Minimal API endpoints in Api. In-memory stores for v0.5; database persistence deferred to 010-persistence.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|-------------|-----------|
| Token format | JWT (access 15min + refresh 7d) | Opaque sessions, cookies | Stateless, scales horizontally, matches Minimal APIs pattern (Art. VI) |
| OAuth flow | Authorization Code + PKCE | Implicit, Client Credentials | RFC 6749 standard; PKCE protects public clients (Art. V) |
| Consent storage (v0.5) | In-memory `ConcurrentDictionary` | SQLite, Redis | No DB in v0/v0.5 (Art. III/VII); in-memory satisfies functional requirements |
| User data storage (v0.5) | In-memory `ConcurrentDictionary` | EF Core, Redis | Same as consent — DB deferred to 010-persistence |
| Privacy policy | Embedded JSON resource | Database, static file | Versioned, immutable, deployable with code |

## Data Flow

```
OAuth Login Flow:
  Browser ──POST /auth/{provider}──→ Api.Endpoints ──→ IAuthService
                                                        │
                                       ┌────────────────┤
                                       ▼                ▼
                              GoogleOAuthAdapter   LinkedInOAuthAdapter
                                       │                │
                                       ▼                ▼
                              Provider token exchange + userinfo
                                       │
                                       ▼
                              UpsertUser → IUserDataService
                                       │
                                       ▼
                              JwtTokenAdapter → Issue access + refresh tokens
                                       │
                                       ▼
                              Response { accessToken, refreshToken }

Protected Endpoint Flow:
  Request ──Authorization: Bearer JWT──→ JwtBearerMiddleware ──→ Validate signature
                                                         │
                                                         ▼
                                                 Set HttpContext.User
                                                         │
                                                         ▼
                                                 Endpoint handler

Consent Flow:
  POST /user/consent ──→ IConsentService ──→ ConsentStore (in-memory v0.5)
                            │                         │
                            ▼                         ▼
                      Check policy version     Write audit log
                            │
                            ▼
                      Result Success / Failure

ARCO Flow (GET /user/data):
  Request ──→ IUserDataService ──→ ConsentStore (verify active consent)
                                        │
                                        ▼
                                  UserDataStore.GetByUserId()
                                        │
                                        ▼
                                  Audit log write → Return data
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/BuildCv.Domain/Auth/User.cs` | Create | User entity (Id, Provider, ProviderId, Email, Name, CreatedAt, LastLoginAt) |
| `src/BuildCv.Domain/Auth/ConsentRecord.cs` | Create | Consent entity (Id, UserId, PolicyVersion, ConsentDate, RevokedAt, Purpose) |
| `src/BuildCv.Domain/Auth/DataTreatmentLog.cs` | Create | Audit entity (Id, UserId, DataType, Action, Timestamp, Reason) |
| `src/BuildCv.Application/Features/Auth/IAuthenticationService.cs` | Create | Port: OAuth exchange, token generation |
| `src/BuildCv.Application/Features/Auth/IConsentService.cs` | Create | Port: consent grant/revoke/check |
| `src/BuildCv.Application/Features/Auth/IUserDataService.cs` | Create | Port: user CRUD for ARCO rights |
| `src/BuildCv.Application/Features/Auth/GoogleOAuthCallbackCommand.cs` | Create | Command + handler for Google callback |
| `src/BuildCv.Application/Features/Auth/LinkedInOAuthCallbackCommand.cs` | Create | Command + handler for LinkedIn callback |
| `src/BuildCv.Application/Features/Auth/RefreshTokenCommand.cs` | Create | Command + handler for token refresh |
| `src/BuildCv.Application/Features/Auth/LogoutCommand.cs` | Create | Command + handler for logout |
| `src/BuildCv.Application/Features/Consent/GrantConsentCommand.cs` | Create | Command + handler |
| `src/BuildCv.Application/Features/Consent/RevokeConsentCommand.cs` | Create | Command + handler |
| `src/BuildCv.Application/Features/Consent/GetUserDataQuery.cs` | Create | Query + handler (ARCO: Access) |
| `src/BuildCv.Application/Features/Consent/RectifyUserDataCommand.cs` | Create | Command + handler (ARCO: Rectification) |
| `src/BuildCv.Application/Features/Consent/DeleteUserDataCommand.cs` | Create | Command + handler (ARCO: Cancellation) |
| `src/BuildCv.Application/Features/Consent/PrivacyPolicyQuery.cs` | Create | Query + handler for policy endpoint |
| `src/BuildCv.Infrastructure/Auth/GoogleOAuthAdapter.cs` | Create | Google OAuth 2.0 implementation |
| `src/BuildCv.Infrastructure/Auth/LinkedInOAuthAdapter.cs` | Create | LinkedIn OAuth 2.0 implementation |
| `src/BuildCv.Infrastructure/Auth/JwtTokenAdapter.cs` | Create | JWT generation + validation |
| `src/BuildCv.Infrastructure/Auth/InMemoryConsentStore.cs` | Create | In-memory consent store (v0.5) |
| `src/BuildCv.Infrastructure/Auth/InMemoryUserDataStore.cs` | Create | In-memory user data store (v0.5) |
| `src/BuildCv.Infrastructure/Auth/InMemoryRefreshTokenStore.cs` | Create | In-memory refresh token store (v0.5) |
| `src/BuildCv.Api/Endpoints/AuthEndpoints.cs` | Create | POST /auth/google, /auth/linkedin, GET /auth/me, POST /auth/logout |
| `src/BuildCv.Api/Endpoints/UserDataEndpoints.cs` | Create | ARCO: GET/PUT/DELETE /user/data, POST /user/consent, /user/consent/revoke |
| `src/BuildCv.Api/Endpoints/PrivacyEndpoints.cs` | Create | GET /privacy-policy |
| `src/BuildCv.Api/Contracts/AuthContracts.cs` | Create | OAuthCallbackRequest, TokenResponse, UserProfileResponse |
| `src/BuildCv.Api/Contracts/UserDataContracts.cs` | Create | UserDataResponse, RectifyRequest, ConsentRequest |
| `src/BuildCv.Api/Program.cs` | Modify | Add JWT Bearer + OAuth middleware, map new endpoints |
| `src/BuildCv.Api/Security/RateLimiting.cs` | Modify | Add `auth` (30/min) and `consent` (10/min) policies |
| `src/BuildCv.Application/DependencyInjection.cs` | Modify | Register new handlers + ports |
| `src/BuildCv.Infrastructure/DependencyInjection.cs` | Modify | Register new adapters |
| `src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj` | Modify | Add `Microsoft.AspNetCore.Authentication.JwtBearer` package |
| `tests/BuildCv.Application.Tests/Features/Auth/` | Create | Consent handler tests, ARCO validation tests |
| `tests/BuildCv.Infrastructure.Tests/Auth/` | Create | JWT adapter tests, OAuth adapter unit tests |
| `tests/BuildCv.Api.IntegrationTests/AuthEndpointTests.cs` | Create | Auth flow integration tests |

## Interfaces / Contracts

### Domain Entities

```csharp
// src/BuildCv.Domain/Auth/User.cs
namespace BuildCv.Domain.Auth;

public sealed class User
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime LastLoginAt { get; init; }
}

// src/BuildCv.Domain/Auth/ConsentRecord.cs
public sealed class ConsentRecord
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int PolicyVersion { get; init; }
    public DateTime ConsentDate { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string Purpose { get; init; } = "";
    public bool IsValid => RevokedAt is null;
}

// src/BuildCv.Domain/Auth/DataTreatmentLog.cs
public sealed class DataTreatmentLog
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string DataType { get; init; } = "";
    public string Action { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public string Reason { get; init; } = "";
}
```

### Application Ports

```csharp
// IAuthenticationService — OAuth token exchange + user lookup
public interface IAuthenticationService
{
    Task<Result<OAuthUserInfo>> ExchangeCodeAsync(string provider, string code, string redirectUri, CancellationToken ct = default);
}

public sealed record OAuthUserInfo(string Provider, string ProviderId, string Email, string Name);

// IConsentService — consent lifecycle
public interface IConsentService
{
    Task<Result<ConsentRecord>> GrantAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<Result> RevokeAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<bool> HasActiveConsentAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetConsentHistoryAsync(Guid userId, CancellationToken ct = default);
}

// IUserDataService — user CRUD for ARCO
public interface IUserDataService
{
    Task<Result<User>> GetOrCreateAsync(string provider, string providerId, string email, string name, CancellationToken ct = default);
    Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<User>> UpdateAsync(Guid userId, string? email, string? name, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default);
}

// IRefreshTokenStore — refresh token lifecycle
public interface IRefreshTokenStore
{
    Task<string> CreateAsync(Guid userId, CancellationToken ct = default);
    Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}
```

### API Contracts

```csharp
// OAuthCallbackRequest
public sealed record OAuthCallbackRequest(string Code, string? State = null);
// TokenResponse
public sealed record TokenResponse(string AccessToken, string RefreshToken, UserProfileResponse User);
// UserProfileResponse
public sealed record UserProfileResponse(Guid UserId, string Provider, string Email, string Name);
// ConsentRequest
public sealed record ConsentRequest(string Purpose);
// RectifyUserDataRequest
public sealed record RectifyUserDataRequest(string? Email, string? Name);
```

## Error Handling Strategy

| Error Code | HTTP Status | When |
|------------|-------------|------|
| `AUTH/OAUTH_FAILED` | 401 | OAuth code exchange fails |
| `AUTH/INVALID_TOKEN` | 401 | JWT validation fails |
| `AUTH/REFRESH_REVOKED` | 401 | Refresh token revoked or expired |
| `AUTH/STATE_MISMATCH` | 403 | OAuth state parameter CSRF mismatch |
| `CONSENT/REQUIRED` | 403 | No active consent for requested operation |
| `CONSENT/STALE_POLICY` | 403 | Consent exists but policy version outdated |
| `CONSENT/ALREADY_GRANTED` | 409 | User attempts to grant already-active consent |
| `ARCO/DATA_NOT_FOUND` | 404 | No user data found for Access request |
| `ARCO/CANCEL_NO_DATA` | 404 | No data to delete for Cancellation request |

All errors return RFC 9457 ProblemDetails. Log metadata only (traceId, provider, error code) — never PII (Art. III).

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| **Unit** | Consent grant/revoke logic, ARCO validation, JWT token generation, OAuth callback parsing | xUnit + FluentAssertions; mock IRefreshTokenStore/IUserDataService |
| **Unit** | In-memory stores (ConsentStore, UserDataStore, RefreshTokenStore) | xUnit; verify CRUD, expiration, revocation |
| **Integration** | Full OAuth callback → token issuance → protected endpoint → refresh → logout | WebApplicationFactory; mock OAuth providers |
| **Integration** | Consent flow: grant → persist attempt → revoke → persist blocked | WebApplicationFactory; verify HTTP status codes |
| **Integration** | ARCO flow: access → rectify → cancel → verify deletion | WebApplicationFactory; verify data lifecycle |

## Migration / Rollout

No database migration required — in-memory stores for v0.5. When 010-persistence lands, swap `InMemory*Store` for `EF Core` implementations behind the same ports. No feature flags needed — auth middleware is additive and unauthenticated endpoints remain accessible.

## Open Questions

- [ ] Google/LinkedIn OAuth client credentials: where to store (user-secrets local, env vars production)?
- [ ] Privacy policy content: JSON resource vs markdown? Needs product decision on format.
- [ ] Refresh token rotation: single-use (strict) vs sliding window (lenient)? Proposal says rotation; design implements single-use.
