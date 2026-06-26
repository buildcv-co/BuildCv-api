# Proposal: 014-constitution-v1.2.0

## Status
[Proposal] — Pending spec

## Type
**Enmienda MENOR** (semver 1.1.0 → 1.2.0)

## Trigger
This amendment is triggered by:
1. **013.2-web-jwt-cookie SHIPPED + ARCHIVED** (commit `09baba5` + tag `013.2-web-jwt-cookie-v1.0`)
   - Added `next-auth@^4.24.7` dependency to BuildCv-web
   - Owner RATIFIED Art. VI amendment verbally, but constitution file still v1.1.0
2. **Pre-existing WARNINGs from 009-auth** (since 2026):
   - Art. III: `IUserDataStore` persistence (not explicit in v1.1.0)
   - Art. VII: v0/v1 auth boundary (implicit, not explicit)

## Goal
Ratify the `next-auth` dependency (Art. VI amendment) and explicitly document the v0/v1 boundary for Art. III (persistence) and Art. VII (auth). No new articles, no breaking changes.

## Changes summary

### Art. III — Privacidad primero (modify)
- Add paragraph: "v0 persists NOTHING. v1 (introduced in 009-auth) MAY persist user identity (email, name, OAuth provider ID) under Habeas Data controls. CV and job content are NEVER persisted server-side, regardless of version."

### Art. VI — Clean Architecture (add exception)
- Add exception clause: "Approved external dependencies for v1: `next-auth@^4.x` for session management in BuildCv-web (ratified 2026-06-25 by owner, see `013.2-web-jwt-cookie-design.md`). This is the only approved web-side auth library."

### Art. VII — v0 lanzable sin fricción (modify)
- Add paragraph: "v0 endpoints are anonymous (no auth required). v1 endpoints (009-auth+) require auth via `RequireAuthorization()` middleware. The boundary is per-endpoint, configurable via the route definition. Both rate-limit policies apply based on the endpoint's role."

### Art. IX — Habeas Data (cross-reference, no change)
- Add note: "User data persistence in v1 is governed by 009-auth (`IUserDataStore`), 013-credit-consumption (`CreditLedgerEntry` cascade), and the ARCO anonymization pattern."

### Header — Version bump
- Change "Versión 1.1.0" to "Versión 1.2.0"
- Change date to 2026-06-25
- Update §Gobernanza to reference this amendment

## Impact declared

### Code impact: ZERO
- No code changes
- No new dependencies (next-auth already ratified and shipped)
- No API changes
- No migration needed

### Documentation impact: MINIMAL
- 1 file modified: `BuildCv-api/.specify/memory/constitution.md`
- ~30 lines added, ~10 lines modified
- 0 lines deleted

### Test impact: NONE
- No new tests needed (governance change)
- Existing 1454/1454 tests still pass (verified at sdd-verify)

### Risk impact: REDUCED
- Removes 2 pre-existing WARNINGs (Art. III + Art. VII)
- Formalizes next-auth ratification (closes Art. VI amendment)

## Affected features

| Feature | Status | Affected |
|---------|--------|----------|
| 009-auth | ✅ SHIPPED | No code change; Art. III + Art. VII clarification |
| 011-factus | ✅ SHIPPED | No change |
| 012-wompi | ✅ SHIPPED | No change |
| 013-credit-consumption | ✅ ARCHIVED | No change |
| 013.2-web-jwt-cookie | ✅ ARCHIVED | Art. VI next-auth ratification |
| 013.3-refund-midstream-test | ✅ SHIPPED | No change |

## Compliance check

### Ratified by §Gobernanza
- ✅ Owner approved Art. VI amendment for next-auth (2026-06-25)
- ✅ Impact declared (code: 0, docs: minimal, tests: 0, risk: reduced)
- ✅ Version bump justified (MINOR: clarifies ratified amendment + addresses WARNINGs)

### Process compliance
- ✅ Proposal written (this document)
- ⏳ Spec pending (changelog of changes)
- ⏳ Design pending (actual diff of constitution.md)
- ⏳ Tasks pending (single commit: edit constitution.md)
- ⏳ Apply pending
- ⏳ Verify pending
- ⏳ Archive pending

## Non-goals

- No new articles (Art. X, etc.)
- No breaking changes to existing articles
- No new governance rules
- No changes to the §Gobernanza process itself

## Open questions

None — all decisions are documented in:
- `BuildCv-api/specs/013-credit-consumption-followups/013.2-web-jwt-cookie-design.md` (Art. VI Amendment)
- `BuildCv-api/specs/013-credit-consumption-followups/013.2-web-jwt-cookie-verify-report.md` (WARNINGs)

## Next

`sdd-spec` → write `spec.md` with the exact changelog (what changes in each article)
