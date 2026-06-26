# Spec: 015-feature-flags

## Status

[Spec] — Pending design

## Overview

Introduce a unified `IFeatureFlag` service in `BuildCv.Application/Common/` that centralizes feature flag management. Migrate the 3 existing flags (011-factus, 012-wompi, 013-credit-consumption) to the new service with backward compatibility — appsettings defaults continue to work, a `feature_flags` table adds runtime overrides, an append-only `feature_flag_audit_log` captures every change, and a minimal admin API (`GET` + `PUT`) lets operators toggle flags without redeploy. The shape replaces 3 bespoke flag patterns with a single port (Art. VI) and unblocks the next 5 flag use cases (per-user, time-based rollout, A/B testing, kill-switches, compliance toggles). Frontend is untouched (v1.5+).

## Domain model

### FeatureFlag (new) — `BuildCv.Domain/FeatureFlags/FeatureFlag.cs`

| Field | Type | Constraint |
|---|---|---|
| `Name` | `string` | PK, kebab-case, e.g. `factus-enabled` |
| `DefaultValue` | `bool` | from `appsettings.json` (source of truth at startup) |
| `CurrentValue` | `bool` | from DB override; falls back to `DefaultValue` when row absent |
| `Description` | `string?` | operator-facing free text |
| `UpdatedAt` | `DateTime` (UTC) | `timestamptz`, `DEFAULT now()` |
| `UpdatedBy` | `Guid?` | user id of last admin flip (nullable: never flipped = `null`) |
| `Xmin` | `uint` | EF shadow property → Postgres system column (optimistic concurrency) |

### FeatureFlagAuditLog (new, append-only) — `BuildCv.Domain/FeatureFlags/FeatureFlagAuditLog.cs`

| Field | Type | Constraint |
|---|---|---|
| `Id` | `Guid` (UUIDv7) | PK |
| `FlagName` | `string` | FK → `feature_flags.name`, indexed |
| `OldValue` | `bool?` | `null` for initial seed/creation |
| `NewValue` | `bool` | required |
| `ChangedBy` | `Guid` | user id of admin, NEVER email/name (Art. III) |
| `ChangedAt` | `DateTime` (UTC) | `timestamptz`, `DEFAULT now()` |
| `Reason` | `string?` (≤ 200) | optional operator note |

Append-only — no `UPDATE`, no `DELETE` on this table.

### Constraints / Indexes
- `UNIQUE(Name)` on `feature_flags` (PK enforces)
- `INDEX(FlagName, ChangedAt DESC)` on `feature_flag_audit_log` — admin list-by-flag, newest first
- `CHECK (CurrentValue IS NOT NULL)` — flag MUST always resolve to a boolean

## Requirements

### R1: IFeatureFlag port + handler

The system MUST expose `IFeatureFlag.IsEnabledAsync(string name, CancellationToken ct)` returning `Task<bool>`. Resolution precedence is `feature_flags.current_value` (DB) → `appsettings.json` (default) → throw `FeatureFlagNotFoundException`. Each successful call MUST be cached in `IMemoryCache` for `FeatureFlags:CacheTtlSeconds` (default `60`). (Art. VI)

#### Scenario: Flag exists in DB → returns DB value
- GIVEN `feature_flags` row with `name = "wompi-enabled"` and `current_value = true`
- WHEN `IFeatureFlag.IsEnabledAsync("wompi-enabled")` is called
- THEN the response is `true`
- AND the result is cached for `CacheTtlSeconds` (default 60s)

#### Scenario: Flag absent in DB → returns appsettings default
- GIVEN no `feature_flags` row for `factus-enabled`
- AND `appsettings.json` has `FeatureFlags:Defaults:factus-enabled = true`
- WHEN `IFeatureFlag.IsEnabledAsync("factus-enabled")` is called
- THEN the response is `true`
- AND no `feature_flag_audit_log` entry is written (read-only fallback)

#### Scenario: Unknown flag with no default → throws
- GIVEN no `feature_flags` row for `reports-v2-enabled`
- AND no `appsettings.json` entry under `FeatureFlags:Defaults:reports-v2-enabled`
- WHEN `IFeatureFlag.IsEnabledAsync("reports-v2-enabled")` is called
- THEN the call throws `FeatureFlagNotFoundException`
- AND the exception surfaces to the caller (no silent fallback to `false`)

### R2: feature_flags table + EF migration

The system MUST provide an EF migration `20260625_AddFeatureFlags` creating two tables (`feature_flags`, `feature_flag_audit_log`) with PKs, indexes, and check constraints as defined in **Domain model**. The migration MUST be backward-compatible: existing 011/012/013 appsettings keys continue to function as defaults when no DB row exists. (Art. VI, Art. IX)

#### Scenario: Migration applies cleanly on empty DB
- GIVEN an empty PostgreSQL schema
- WHEN `dotnet ef database update` runs migration `20260625_AddFeatureFlags`
- THEN both tables exist with correct columns, PKs, indexes, and CHECK constraints
- AND the migration is idempotent on rerun (no errors if already applied)

#### Scenario: Existing appsettings still works pre-seed
- GIVEN `feature_flags` table is empty (migration ran, but seed hasn't)
- AND `appsettings.json` has `FeatureFlags:Defaults:wompi-enabled = true`
- WHEN `IFeatureFlag.IsEnabledAsync("wompi-enabled")` is called
- THEN the response is `true` (appsettings fallback)
- AND no row is auto-created in `feature_flags` (seed is a separate `IHostedService` step)

#### Scenario: DB row overrides appsettings
- GIVEN `feature_flags` row `wompi-enabled = false`
- AND `appsettings.json` has `FeatureFlags:Defaults:wompi-enabled = true`
- WHEN `IFeatureFlag.IsEnabledAsync("wompi-enabled")` is called
- THEN the response is `false` (DB wins)

### R3: Caching decorator

The system MUST wrap `EfFeatureFlagStore` with `CachingFeatureFlagDecorator` using `IMemoryCache` (already in `Microsoft.Extensions.Caching.Memory`, no new NuGet). TTL MUST be configurable via `FeatureFlags:CacheTtlSeconds` (default `60`). Admin updates MUST call `Invalidate(name)` synchronously after the DB write commits so the local instance sees the change immediately (other instances bounded by TTL). (Art. VI)

#### Scenario: Second call within TTL returns from cache
- GIVEN `IFeatureFlag.IsEnabledAsync("wompi-enabled")` returns `true` on the first call
- WHEN the same call is made again within 60s
- THEN the second call returns `true` without a DB roundtrip
- AND the cache key is `feature-flag:{name}`

#### Scenario: Admin update invalidates cache for that flag
- GIVEN `IFeatureFlag.IsEnabledAsync("wompi-enabled")` returns `true` and is cached
- WHEN an admin calls `PUT /api/v1/admin/feature-flags/wompi-enabled` with `value = false`
- THEN the cache entry for `wompi-enabled` is removed synchronously after DB commit
- AND the next `IsEnabledAsync("wompi-enabled")` call returns `false` immediately (no waiting for TTL)

#### Scenario: Cache TTL is configurable
- GIVEN `FeatureFlags:CacheTtlSeconds = 30` in `appsettings.json`
- WHEN `IFeatureFlag.IsEnabledAsync("credits-enabled")` is called twice
- THEN the second call after > 30s re-queries the DB
- AND the cache expires exactly at 30s

### R4: Admin API

The system MUST expose a minimal admin API under `/api/v1/admin/feature-flags` requiring the `admin` role claim (re-use 009-auth JWT + new `"admin"` policy) and rate-limited by the `"admin"` policy (30/min by IP). (Art. VI, Art. VII, Art. IX)

#### Scenario: Admin updates a flag
- GIVEN an authenticated user with `role = admin` and a valid JWT
- WHEN `PUT /api/v1/admin/feature-flags/wompi-enabled` is called with body `{ "value": false, "reason": "Disabling for production incident P1-273" }`
- THEN the response is HTTP 200 with `{ name, defaultValue, currentValue, updatedAt, updatedBy }`
- AND `feature_flags.current_value` is updated to `false`
- AND `feature_flag_audit_log` gains one row: `{ flagName: "wompi-enabled", oldValue: true, newValue: false, changedBy: <userId>, reason: "Disabling for production incident P1-273" }`
- AND the cache entry for `wompi-enabled` is invalidated

#### Scenario: Non-admin user gets 403
- GIVEN an authenticated user with `role = user` (no `admin` claim)
- WHEN `PUT /api/v1/admin/feature-flags/wompi-enabled` is called
- THEN the response is HTTP 403 with `{ error: "AUTH/FORBIDDEN" }`
- AND no DB write occurs and no audit log entry is created

#### Scenario: Unauthenticated user gets 401
- GIVEN no JWT in the request
- WHEN `PUT /api/v1/admin/feature-flags/wompi-enabled` is called
- THEN the response is HTTP 401 with `{ error: "AUTH/UNAUTHENTICATED" }`

#### Scenario: Unknown flag returns 404
- GIVEN a request for a flag name not in `feature_flags` and not in appsettings
- WHEN `PUT /api/v1/admin/feature-flags/reports-v2-enabled` is called by an admin
- THEN the response is HTTP 404 with `{ error: "FEATURE_FLAG/NOT_FOUND" }`
- AND no audit log entry is created (nothing changed)

#### Scenario: Rate limit applied
- GIVEN the same admin IP making > 30 PUT requests in 60 seconds
- WHEN the 31st request arrives
- THEN the response is HTTP 429 with `{ error: "RATE_LIMIT/EXCEEDED" }`
- AND the response includes a `Retry-After` header (seconds until window resets)

### R5: Audit log query

The system MUST expose `GET /api/v1/admin/feature-flags/{name}/audit-log?limit=50&cursor={cursor}` (admin role, same `"admin"` rate limit) returning the last entries for that flag, newest first, with keyset pagination (cursor = base64(`{changedAt.Ticks}:{id}`)). Default `limit = 50`, max `limit = 200`. The table is append-only — no admin endpoint may `UPDATE` or `DELETE` rows. (Art. IX)

#### Scenario: Newest-first listing with default limit
- GIVEN 30 audit-log entries for `wompi-enabled`
- WHEN `GET /api/v1/admin/feature-flags/wompi-enabled/audit-log` is called
- THEN the response is HTTP 200 with `{ entries: [...20 newest...], nextCursor: "..." }` (limit defaults to 50, so all 30 returned if 30 ≤ 50)

#### Scenario: Pagination via cursor
- GIVEN 75 audit-log entries for `wompi-enabled`
- WHEN `GET .../audit-log?limit=20` is called, then `?limit=20&cursor={returned cursor}`
- THEN the second response contains entries 21–40 (no overlap with first page)
- AND `nextCursor` is `null` when no more entries

#### Scenario: Flag with no history returns empty list
- GIVEN no audit-log entries for `credits-enabled`
- WHEN `GET .../credits-enabled/audit-log` is called
- THEN the response is HTTP 200 with `{ entries: [], nextCursor: null }`

### R6: Migration of existing flags (backward compatibility)

The system MUST seed 3 rows into `feature_flags` from `appsettings.json` defaults via `FeatureFlagMigrationService` (`IHostedService`, runs once on startup, idempotent via `upsert` on `Name`). The 3 existing flag patterns (011-factus `IInvoiceProvider?`, 012-wompi `IPaymentProvider` active vs `DisabledPaymentProvider`, 013-credit-consumption `ICreditsFeatureFlag`) MUST keep their public contracts through thin adapter classes that delegate to `IFeatureFlag`. (Art. VI — zero breaking change to 011/012/013)

#### Scenario: Migration seeds 3 rows from appsettings
- GIVEN `appsettings.json` has `FeatureFlags:Defaults:factus-enabled = true`, `wompi-enabled = true`, `credits-enabled = false`
- WHEN `FeatureFlagMigrationService` runs on startup
- THEN 3 rows are upserted into `feature_flags` with matching `default_value` and `current_value`
- AND rerunning the service is idempotent (no duplicate rows)

#### Scenario: 011-factus adapter still works
- GIVEN an authenticated request to `POST /api/v1/invoices` (factus endpoint)
- AND `FeatureFlags:Defaults:factus-enabled = true`
- WHEN the request is processed
- THEN the existing 011 endpoint returns the same response as before PR1 of 015
- AND `IFeatureFlag.IsEnabledAsync("factus-enabled")` is invoked inside the adapter (verified via integration test)

#### Scenario: 012-wompi active vs disabled adapter still works
- GIVEN an authenticated request to `POST /api/v1/payments/checkout`
- AND `FeatureFlags:Defaults:wompi-enabled = false`
- WHEN the request is processed
- THEN `IPaymentProvider` resolves to `DisabledPaymentProvider` (returns 404)
- AND this behavior is identical to pre-015 (proven by rerunning 012 test suite unchanged)

#### Scenario: 013-credit-consumption adapter still works
- GIVEN `ICreditsFeatureFlag.IsEnabled` is read by `HandleWebhookHandler`
- WHEN a webhook arrives
- THEN the adapter delegates to `IFeatureFlag.IsEnabledAsync("credits-enabled")`
- AND the 013 test suite passes unchanged

### R7: List all flags

The system MUST expose `GET /api/v1/admin/feature-flags` (admin role, `"admin"` rate limit) returning every registered flag with name + default + current values, sorted by name. (Art. IX — visibility for compliance)

#### Scenario: Admin lists all registered flags
- GIVEN 3 flags in `feature_flags` (`factus-enabled`, `wompi-enabled`, `credits-enabled`)
- WHEN `GET /api/v1/admin/feature-flags` is called by an admin
- THEN the response is HTTP 200 with `{ flags: [{ name, defaultValue, currentValue, updatedAt, updatedBy }, ...] }`
- AND entries are sorted alphabetically by `name` (deterministic order)

#### Scenario: Flags absent from DB do not appear
- GIVEN only `wompi-enabled` is in `feature_flags` (others fall back to appsettings)
- WHEN `GET /api/v1/admin/feature-flags` is called
- THEN the response lists only `wompi-enabled`
- AND appsettings-only flags are not enumerated (no auto-seed on read)

## API contracts

| Method | Path | Auth | Rate limit | Returns |
|---|---|---|---|---|
| `GET` | `/api/v1/admin/feature-flags` | admin | `admin` 30/min/IP | `{ flags: [{ name, defaultValue, currentValue, updatedAt, updatedBy }] }` |
| `GET` | `/api/v1/admin/feature-flags/{name}` | admin | `admin` 30/min/IP | `{ name, defaultValue, currentValue, updatedAt, updatedBy }` or 404 |
| `PUT` | `/api/v1/admin/feature-flags/{name}` | admin | `admin` 30/min/IP | `{ name, defaultValue, currentValue, updatedAt, updatedBy }` |
| `GET` | `/api/v1/admin/feature-flags/{name}/audit-log?limit&cursor` | admin | `admin` 30/min/IP | `{ entries: [...], nextCursor: string? }` |

### Error envelopes (RFC 9457 ProblemDetails for 4xx)

| HTTP | Code | Trigger |
|---|---|---|
| 401 | `AUTH/UNAUTHENTICATED` | No JWT |
| 403 | `AUTH/FORBIDDEN` | JWT without `admin` role |
| 404 | `FEATURE_FLAG/NOT_FOUND` | Flag name not in DB and not in appsettings |
| 409 | `FEATURE_FLAG/CONFLICT` | Optimistic concurrency failure (`xmin` mismatch — retry once) |
| 429 | `RATE_LIMIT/EXCEEDED` | > 30 PUT/GET in 60s from same IP |

### `PUT /api/v1/admin/feature-flags/{name}` — request/response

```json
// Request
{ "value": false, "reason": "Disabling for production incident P1-273" }

// Response 200
{ "name": "wompi-enabled", "defaultValue": true, "currentValue": false,
  "updatedAt": "2026-06-25T14:30:00Z", "updatedBy": "<admin-user-guid>" }
```

## Application ports

### `IFeatureFlag` (new) — `BuildCv.Application/Common/IFeatureFlag.cs`
```csharp
public interface IFeatureFlag
{
    Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
}
```

### `IFeatureFlagStore` (new) — `BuildCv.Application/Common/IFeatureFlagStore.cs`
```csharp
public interface IFeatureFlagStore
{
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
    Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default);
    Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default);
}
```

## Frontend integration

**None** — this is a backend-only feature. Operators consume the admin API via curl / scripts until a v1.5 web admin dashboard ships. The 013 `CreditBadge` and `LowCreditBanner` UI components are untouched.

## Strategy

3 chained PRs (matching 013-credit-consumption pattern, each keeps `dotnet build` + `dotnet test` + `dotnet format` green):

- **PR1** (~200 lines, +20 unit tests): Domain + Application — `FeatureFlag` + `FeatureFlagAuditLog` entities, `FeatureFlagNotFoundException`, `IFeatureFlag` + `IFeatureFlagStore` ports, 3 handlers (`GetFeatureFlag`, `ListFeatureFlags`, `UpdateFeatureFlag`).
- **PR2** (~250 lines, +15 integration tests): Infrastructure + DB — `EfFeatureFlagStore` + `InMemoryFeatureFlagStore` (test-only), `CachingFeatureFlagDecorator` (`IMemoryCache` 60s TTL + `Invalidate(name)`), `FeatureFlagMigrationService` (`IHostedService`, idempotent seed), `FeatureFlagOptions` (`IConfiguration` binder for `FeatureFlags:Defaults:{name}`), EF migration `20260625_AddFeatureFlags`.
- **PR3** (~150 lines, +10 e2e tests): API — `FeatureFlagAdminEndpoints` (GET list, GET single, PUT update, GET audit-log), `Admin` policy + `admin` rate-limit policy, 3 adapter classes (`FeatureFlagInvoiceAdapter`, `FeatureFlagPaymentAdapter`, `FeatureFlagCreditsAdapter`), integration tests rerun 011/012/013 suites unchanged to prove no regression.

## Compliance

| Article | How 015 complies |
|---|---|
| **I (Cero invención)** | N/A — flag infrastructure, no CV/job content. |
| **II (Determinismo)** | N/A — score engine untouched. `IFeatureFlag.IsEnabledAsync` is process-stable within the cache TTL (single boolean). |
| **III (Privacidad primero)** | `FeatureFlagAuditLog.ChangedBy` is a `Guid` user id — never email, name, IP, or CV/job content. Logs use `flagName, oldValue, newValue, changedBy, traceId` — same pattern as 012-wompi. |
| **IV (Encuadre honesto)** | Admin API returns raw boolean + description. No "advanced AI" or marketing copy. |
| **V (Entrada como dato)** | N/A — flag names are config-time constants, not user input. |
| **VI (Clean Architecture)** | `IFeatureFlag`, `IFeatureFlagStore` ports in `BuildCv.Application/Common/`. `EfFeatureFlagStore` + `CachingFeatureFlagDecorator` + `FeatureFlagMigrationService` in `BuildCv.Infrastructure`. Domain stays pure (verified by `dotnet list src/BuildCv.Domain package references` returning 0). Backward-compat adapters keep 011/012/013 contracts unchanged. |
| **VII (Rate limits)** | New `"admin"` policy: `30/min/IP` for `/api/v1/admin/feature-flags/*`. Lower than `score` (60/min) and `ai` (5/h) intentionally — admin endpoints are sensitive. `score`/`ai`/`export`/`import` policies unchanged. |
| **VIII (TDD)** | Red-green-refactor on every handler, decorator, adapter. Adapter tests rerun 011/012/013 suites unchanged to prove no regression. |
| **IX (Habeas Data)** | **Access:** R7 lists all flags. **Rectification:** R4 updates flag values + writes audit row. **Cancellation:** N/A (operational config). **Consent:** N/A. **Audit:** R5 reads audit log; every change is recorded with `changed_by`, `old_value`, `new_value`, `changed_at`, `reason` — compliance evidence for kill-switches. |

## Acceptance criteria

- [ ] All 7 R's pass with green tests
- [ ] All 6 gates pass: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`, `constitution-check.sh`, `preflight.sh`, no new warnings
- [ ] Test counts: +45 (20 unit + 15 integration + 10 e2e)
- [ ] 3 existing flags (011/012/013) migrated without regression — their original test suites pass unchanged
- [ ] Backward compatibility verified: existing appsettings keys (`Factus:Enabled`, `Wompi:Enabled`, `Credits:Enabled`) continue to work pre- and post-migration
- [ ] Audit log captures every admin update (verified by integration test: PUT → row inserted in same transaction)
- [ ] Admin endpoints require `admin` role + rate-limited at 30/min/IP
- [ ] No new NuGet dependencies (only `Microsoft.Extensions.Caching.Memory` already in API project)
- [ ] Zero suppressions, zero mocks falsos (real `InMemoryFeatureFlagStore` + Testcontainers PostgreSQL)

## Out of scope

- Admin dashboard UI (deferred to v1.5)
- Per-user flags / targeting (deferred to v1.5)
- A/B testing framework (deferred to v1.5)
- Time-based rollout (`enable_at` / `disable_at`) (deferred to v1.5)
- Multi-tenant flags (single-tenant)
- Flag analytics / telemetry (out of scope)
- Migration of 012's `Wompi:Environment` (sandbox vs production) — stays as `IOptions<WompiOptions>` (3-state config, not boolean)
- Audit log retention policy (indefinite for v1; cron deferred to v1.5)

## Next

`sdd-design` → ports (`IFeatureFlag`, `IFeatureFlagStore`, `IFeatureFlagAdminService`), EF migration SQL (`feature_flags` + `feature_flag_audit_log`), `CachingFeatureFlagDecorator` TTL config + invalidation semantics, `RequireAdmin` endpoint filter + `admin` rate-limit policy registration, 3 adapter classes (`FeatureFlagInvoiceAdapter`, `FeatureFlagPaymentAdapter`, `FeatureFlagCreditsAdapter`).