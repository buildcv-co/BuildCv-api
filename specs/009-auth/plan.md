# Proposal: 009-auth — Authentication & Habeas Data Compliance

## Intent

BuildCv needs user accounts and Habeas Data compliance to unlock v1. Today the app is stateless (Art. III, v0). Adding auth enables: (a) personalized scoring history, (b) consent-gated data persistence (prerequisite for 010-persistence), and (c) legal compliance with Ley 1581 de 2012 before monetization. Without auth, we cannot identify users, collect consent, or honor ARCO rights — all hard requirements of Art. IX.

## Scope

### In Scope
- OAuth 2.0 authentication (Google + LinkedIn providers)
- JWT session management (access + refresh tokens)
- Consent flow: prior, informed, express consent before any data save
- ARCO rights API: Access, Rectification, Cancellation, Opposition endpoints
- Privacy policy endpoint (public, versioned)
- Data treatment registry (consent records with timestamps, scope, version)
- New rate limit policies: `auth` (login), `consent` (consent operations)
- New ports: `IAuthenticationService`, `IConsentService`, `IUserDataService`
- TDD: tests for consent logic, ARCO operations, OAuth token exchange

### Out of Scope
- Anthropic Enterprise ZDR (external gate, blocked)
- Wompi payment integration (external gate, blocked)
- Persistence layer (010-persistence — separate feature)
- UI/frontend auth flows (BuildCv-web responsibility)
- Email/password auth (OAuth only for v1)

### Deferred
- ZDR contract verification (Anthropic Enterprise signup)
- Wompi RUT registration + server-side payment confirmation
- Multi-factor authentication
- Role-based access control

## Capabilities

### New Capabilities
- `user-auth`: OAuth 2.0 login (Google, LinkedIn), JWT access/refresh tokens, session lifecycle, user profile storage
- `habeas-data-compliance`: Consent collection/revocation, ARCO rights API, privacy policy endpoint, data treatment registry

### Modified Capabilities
None — net-new feature, no existing spec changes.

## Approach

Follow Clean Architecture (Art. VI): ports in Application layer, adapters in Infrastructure. Use ASP.NET Core's built-in authentication middleware (JWT Bearer + OAuth 2.0). Consent and ARCO as separate Application features with their own handlers. Minimal API pattern consistent with existing endpoints (`/api/v1/auth/*`, `/api/v1/privacy/*`).

Key design decisions:
- **JWT over sessions**: stateless, scales horizontally, matches existing Minimal APIs pattern
- **Consent before persistence**: user must consent before 010-persistence can store anything (Art. IX FR-051)
- **Consent versioned**: each consent record includes policy version for audit trail
- **No PII in logs**: consent records log metadata only (userId, scope, timestamp) — never CV content (Art. III)

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/BuildCv.Application/Features/Auth/` | New | OAuth handler, JWT service, user profile port |
| `src/BuildCv.Application/Features/Consent/` | New | Consent/ARCO handlers, privacy policy port |
| `src/BuildCv.Infrastructure/Auth/` | New | Google/LinkedIn OAuth adapters, JWT issuer |
| `src/BuildCv.Api/Endpoints/AuthEndpoints.cs` | New | Login, callback, refresh, logout endpoints |
| `src/BuildCv.Api/Endpoints/PrivacyEndpoints.cs` | New | Consent, ARCO, privacy policy endpoints |
| `src/BuildCv.Api/Program.cs` | Modified | Add auth middleware + OAuth configuration |
| `src/BuildCv.Api/Security/RateLimiting.cs` | Modified | Add `auth` and `consent` rate limit policies |
| `tests/BuildCv.Application.Tests/` | New | Consent logic, ARCO validation tests |
| `tests/BuildCv.Api.IntegrationTests/` | New | Auth flow, consent flow integration tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| OAuth provider outage blocks login | Low | Graceful error + retry; no fallback provider yet (v1 scope) |
| Consent policy version drift | Medium | Single source of truth in `PrivacyPolicy` entity, version bumped atomically |
| JWT secret rotation breaks sessions | Low | Short-lived access tokens (15min) + refresh token rotation |
| Art. IX gates block full v1 | High | Auth infrastructure ships anyway; ZDR/Wompi gates are external and tracked separately |

## Rollback Plan

1. Remove auth middleware from `Program.cs` — endpoints return 401 but existing scoring/export/import remain unauthenticated
2. Remove OAuth configuration from `appsettings.json`
3. Revert new endpoint files and Application features
4. No database migration to rollback (v0 has no DB)
5. Rate limit policies for `auth`/`consent` become dead config — safe to leave

## Dependencies

- Google OAuth 2.0 client credentials (client ID + secret)
- LinkedIn OAuth 2.0 client credentials (client ID + secret)
- .NET 10 JWT Bearer middleware (built-in, no external package)
- Constitution Art. IX compliance audit before shipping

## Success Criteria

- [ ] User can authenticate via Google OAuth and receive JWT access + refresh tokens
- [ ] User can authenticate via LinkedIn OAuth and receive JWT access + refresh tokens
- [ ] Consent flow requires explicit opt-in before any data operation
- [ ] Consent revocation stops all data processing immediately
- [ ] ARCO endpoints return user data (Access), allow edits (Rectification), deletion (Cancellation)
- [ ] Privacy policy endpoint returns current version with full treatment details
- [ ] All auth/consent operations rate-limited per policy
- [ ] Zero PII in structured logs (Art. III)
- [ ] Tests pass for consent logic, ARCO operations, OAuth token lifecycle
- [ ] `dotnet build BuildCv.slnx -c Release` — 0 warnings
- [ ] `dotnet test` — all green
