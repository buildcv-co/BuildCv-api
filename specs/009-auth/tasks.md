# Tasks: 009-auth — Authentication & Habeas Data Compliance

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1400–1800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain entities + Application ports + in-memory stores + JWT adapter | PR 1 | Base: feature/009-auth. Core types everything depends on. ~400 lines |
| 2 | OAuth adapters + consent/ARCO handlers + auth endpoints + middleware | PR 2 | Base: PR 1 branch. Main logic + API wiring. ~600 lines |
| 3 | Integration tests + rate limiting + cleanup + DI wiring finalization | PR 3 | Base: PR 2 branch. Verification + polish. ~400 lines |

---

## Phase 1: Domain Layer — Entities & Shared Types

- [x] 1.1 Create `src/BuildCv.Domain/Auth/User.cs` — User entity (Id, Provider, ProviderId, Email, Name, CreatedAt, LastLoginAt)
- [x] 1.2 Create `src/BuildCv.Domain/Auth/ConsentRecord.cs` — Consent entity (Id, UserId, PolicyVersion, ConsentDate, RevokedAt, Purpose, IsValid computed)
- [x] 1.3 Create `src/BuildCv.Domain/Auth/DataTreatmentLog.cs` — Audit entity (Id, UserId, DataType, Action, Timestamp, Reason)
- [x] 1.4 **Test**: Write `tests/BuildCv.Domain.Tests/Auth/UserTests.cs` — Verify User init properties, ConsentRecord.IsValid behavior (TDD RED→GREEN)

## Phase 2: Application Layer — Ports & Commands

- [x] 2.1 Create `src/BuildCv.Application/Features/Auth/IAuthenticationService.cs` — Port: ExchangeCodeAsync(provider, code, redirectUri) → Result\<OAuthUserInfo\>
- [x] 2.2 Create `src/BuildCv.Application/Features/Auth/IRefreshTokenStore.cs` — Port: CreateAsync, ValidateAsync, RevokeAsync
- [x] 2.3 Create `src/BuildCv.Application/Features/Auth/IConsentService.cs` — Port: GrantAsync, RevokeAsync, HasActiveConsentAsync, GetConsentHistoryAsync
- [x] 2.4 Create `src/BuildCv.Application/Features/Auth/IUserDataService.cs` — Port: GetOrCreateAsync, GetByIdAsync, UpdateAsync, DeleteAsync, GetTreatmentLogsAsync
- [x] 2.5 Create `src/BuildCv.Application/Features/Auth/GoogleOAuthCallbackCommand.cs` + Handler — Command + handler for Google OAuth callback
- [x] 2.6 Create `src/BuildCv.Application/Features/Auth/LinkedInOAuthCallbackCommand.cs` + Handler — Command + handler for LinkedIn OAuth callback
- [x] 2.7 Create `src/BuildCv.Application/Features/Auth/RefreshTokenCommand.cs` + Handler — Token refresh with rotation
- [x] 2.8 Create `src/BuildCv.Application/Features/Auth/LogoutCommand.cs` + Handler — Revoke refresh token
- [x] 2.9 Create `src/BuildCv.Application/Features/Auth/GrantConsentCommand.cs` + Handler — Grant consent with policy version check
- [x] 2.10 Create `src/BuildCv.Application/Features/Auth/RevokeConsentCommand.cs` + Handler — Revoke active consent
- [x] 2.11 Create `src/BuildCv.Application/Features/Auth/GetUserDataQuery.cs` + Handler — ARCO: Access (return all user data)
- [x] 2.12 Create `src/BuildCv.Application/Features/Auth/RectifyUserDataCommand.cs` + Handler — ARCO: Rectification
- [x] 2.13 Create `src/BuildCv.Application/Features/Auth/DeleteUserDataCommand.cs` + Handler — ARCO: Cancellation (delete all + revoke consent)
- [x] 2.14 Create `src/BuildCv.Application/Features/Consent/PrivacyPolicyQuery.cs` + Handler — Return current/specific policy version
- [x] 2.15 **Test**: Write `tests/BuildCv.Application.Tests/Features/Auth/ConsentHandlerTests.cs` — Grant requires no existing active consent, revoke stops operations, stale policy triggers re-consent (TDD)
- [x] 2.16 **Test**: Write `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs` — Access returns all data, rectify updates fields + logs, cancel deletes + revokes (TDD)
- [x] 2.17 **Test**: Write `tests/BuildCv.Application.Tests/Features/Auth/OAuthCallbackHandlerTests.cs` — Callback creates/updates user, issues tokens, rotates refresh (TDD)
- [x] 2.18 Modify `src/BuildCv.Application/DependencyInjection.cs` — Register new handlers, validators, ports

## Phase 3: Infrastructure Layer — Adapters & Stores

- [x] 3.1 Create `src/BuildCv.Infrastructure/Auth/GoogleOAuthAdapter.cs` — Google OAuth 2.0 code exchange + userinfo via HttpClient
- [x] 3.2 Create `src/BuildCv.Infrastructure/Auth/LinkedInOAuthAdapter.cs` — LinkedIn OAuth 2.0 code exchange + userinfo via HttpClient
- [x] 3.3 Create `src/BuildCv.Infrastructure/Auth/JwtTokenAdapter.cs` — JWT generation (access 15min + refresh 7d) using System.IdentityModel.Tokens.Jwt
- [x] 3.4 Create `src/BuildCv.Infrastructure/Auth/InMemoryRefreshTokenStore.cs` — ConcurrentDictionary-based refresh token store with expiration
- [x] 3.5 Create `src/BuildCv.Application/Features/Auth/InMemoryConsentStore.cs` — ConcurrentDictionary-based consent store (append-only audit trail)
- [x] 3.6 Create `src/BuildCv.Application/Features/Auth/InMemoryUserDataStore.cs` — ConcurrentDictionary-based user data store + audit log
- [x] 3.7 **Test**: Write `tests/BuildCv.Infrastructure.Tests/Auth/JwtTokenAdapterTests.cs` — Generate valid JWT, validate signature, check expiry claims (TDD)
- [x] 3.8 **Test**: Write `tests/BuildCv.Infrastructure.Tests/Auth/InMemoryStoresTests.cs` — CRUD for all 3 stores, expiration, revocation, append-only audit (TDD)
- [x] 3.9 Modify `src/BuildCv.Infrastructure/DependencyInjection.cs` — Register OAuth adapters, JWT adapter, in-memory stores
- [x] 3.10 Modify `src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj` — Add `Microsoft.AspNetCore.Authentication.JwtBearer` package

## Phase 4: Api Layer — Endpoints, Contracts & Middleware

- [x] 4.1 Create `src/BuildCv.Api/Contracts/AuthContracts.cs` — OAuthCallbackRequest, TokenResponse, UserProfileResponse records
- [x] 4.2 Create `src/BuildCv.Api/Contracts/UserDataContracts.cs` — UserDataResponse, RectifyRequest, ConsentRequest records
- [x] 4.3 Create `src/BuildCv.Api/Endpoints/AuthEndpoints.cs` — POST /auth/google, /auth/linkedin, GET /auth/me, POST /auth/logout (rate-limited)
- [x] 4.4 Create `src/BuildCv.Api/Endpoints/UserDataEndpoints.cs` — GET/PUT/DELETE /user/data, POST /user/consent, /user/consent/revoke (rate-limited)
- [x] 4.5 Create `src/BuildCv.Api/Endpoints/PrivacyEndpoints.cs` — GET /privacy-policy (public, no auth required)
- [x] 4.6 Modify `src/BuildCv.Api/Security/RateLimiting.cs` — Add `auth` (30/min) and `consent` (10/min) policies
- [x] 4.7 Modify `src/BuildCv.Api/Program.cs` — Add JWT Bearer auth middleware, OAuth configuration, map new endpoints
- [x] 4.8 **Test**: Write `tests/BuildCv.Api.IntegrationTests/AuthEndpointTests.cs` — Full OAuth callback → token → protected endpoint → refresh → logout flow (WebApplicationFactory, mock OAuth)
- [x] 4.9 **Test**: Write `tests/BuildCv.Api.IntegrationTests/ConsentEndpointTests.cs` — Consent grant → persist → revoke → persist blocked flow
- [x] 4.10 **Test**: Write `tests/BuildCv.Api.IntegrationTests/ArcoEndpointTests.cs` — Access → rectify → cancel → verify deletion lifecycle

## Phase 5: Verification & Cleanup

- [x] 5.1 Run `dotnet build BuildCv.slnx -c Release` — 0 warnings
- [x] 5.2 Run `dotnet test` — all green
- [x] 5.3 Run `dotnet format --verify-no-changes` — formatting clean
- [x] 5.4 Verify Domain purity: `dotnet list src/BuildCv.Domain package references` → 0 packages
- [x] 5.5 Verify no PII in structured logs (Art. III compliance check)
