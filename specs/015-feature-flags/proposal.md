# Proposal: 015-feature-flags — Centralized feature flag service

## Status

[Proposal] — Pending spec (no `spec.md` / `design.md` / `tasks.md` exist yet).

## Context

**The problem.** Three shipped features (011-factus, 012-wompi, 013-credit-consumption) each implemented their own feature flag pattern independently. This is duplication of effort, three different shapes for the same concept, and a wall in front of the next 4-5 flag use cases already on the roadmap (per-user flags, time-based rollout, A/B testing, emergency kill-switches, compliance toggles).

**Why now.** v1 monetization (013-credit-consumption) is shipped and the v0/v1 boundary is documented (014-constitution-v1.2.0). The product has now accumulated 3 independent flag implementations, and a 4th-5th flag without centralization would create 5+ inconsistent shapes. The Clean Architecture ports pattern is mature (`IAiClient`, `ICvParser`, `IPdfGenerator`, `IPaymentProvider`, `IInvoiceProvider`, `ICreditLedger`), so the natural shape is `IFeatureFlag` as a fifth cross-cutting port in `BuildCv.Application/Common/`.

### Existing flag patterns (documented)

| Feature | Flag key | Pattern | Implementation |
|---------|----------|---------|----------------|
| 011-factus | `Factus:Enabled` | appsettings.json boolean + nullable provider | `IInvoiceProvider?` resolved via null-conditional; if null, system runs in local mode (Draft invoice) |
| 012-wompi | `Wompi:Enabled` | appsettings.json boolean + active vs disabled adapter | DI registers `IPaymentProvider` = `WompiAdapter` (enabled) OR `DisabledPaymentProvider` (disabled, returns 404) |
| 013-credit-consumption | `Credits:Enabled` | appsettings.json boolean + dedicated interface | `ICreditsFeatureFlag.IsEnabled` (`IOptions<CreditsOptions>` binder, synchronous boolean property) |

All three patterns share these properties:

- **Source of truth:** `appsettings.json` boolean only — **no DB persistence**, no admin API to change at runtime.
- **Read at:** DI registration time (011/012) OR runtime per-call (013).
- **No audit trail:** flag state changes require a code deploy.
- **No admin UI / API:** operators cannot toggle flags without a redeploy.
- **No caching strategy:** 011/012 read once at startup; 013 reads on every call (no perf issue at v0.5 traffic, but the shape doesn't compose for v2 high-QPS checks).
- **No per-flag metadata:** no `description`, `owner`, `created_at`, `last_changed_by` — just a boolean.
- **No consistency on naming:** `Factus:Enabled`, `Wompi:Enabled`, `Credits:Enabled` are all PascalCase + `:Enabled`, but the next flag (`Reports:Enabled`? `NewScoreAlgorithm:Enabled`?) would need a fourth bespoke shape.

### Upstream blockers for the next 5 flag use cases

| Use case | Why current pattern fails |
|----------|--------------------------|
| **Per-user flags** (e.g., beta tester override) | All 3 patterns are process-global; no `userId` parameter |
| **Time-based rollout** (e.g., enable for 10% of traffic over 24h) | No concept of rollout window or cohort |
| **A/B testing** (e.g., 50/50 split on two scoring algorithms) | No variant selector, no telemetry hook |
| **Emergency kill-switch** (compliance / legal) | Requires redeploy; no audit trail of who flipped it |
| **Compliance toggles** (e.g., force-strict logging for an audit window) | No audit log = no Art. IX compliance evidence |

### Constitutional pressure

- **Art. VII** (v0 lanzable sin fricción): flags help ship v0.5 features without redeploying the world. Currently we redeploy the API to flip a flag.
- **Art. IX** (Habeas Data + DIAN): a kill-switch that flips in seconds (not minutes) is materially safer for a P1 incident. **No audit log today = no compliance evidence.**
- **Art. VI** (Clean Architecture): three bespoke flag shapes is not a port — it's three ad-hoc things pretending to be one. Centralizing into `IFeatureFlag` is the port the Constitution expects.

## Goal

After 015 ships, the system has:

1. A single `IFeatureFlag` service in `BuildCv.Application/Common/` with one method `IsEnabled(string name)` returning `Task<bool>`.
2. The 3 existing flags (Factus, Wompi, Credits) migrated to use the new service — **backward compatible**: existing appsettings keys still work as defaults.
3. A `feature_flags` table in Postgres for runtime overrides (DB value wins over appsettings).
4. A `feature_flag_audit_log` table (append-only) capturing every flag change with `changed_by`, `old_value`, `new_value`, `reason`, `changed_at`.
5. A minimal admin API: `GET /api/v1/admin/feature-flags` + `PUT /api/v1/admin/feature-flags/{name}` with admin-role auth + rate-limiting.
6. In-memory cache with TTL (60s, configurable) + explicit invalidation on admin update.
7. Migration tooling: a one-time `FeatureFlagMigrationService` that seeds the 3 existing flags from `appsettings.json` defaults on first deploy.

**Deferred to v1.5** (per Non-goals): per-user targeting, time-based rollout, A/B testing, admin dashboard UI, multi-tenant flags, flag analytics.

## Non-goals

- **Admin dashboard UI** — API only; web UI deferred to v1.5.
- **Per-user flags** — process-global only; targeting is v1.5.
- **A/B testing framework** — out of scope; current goal is binary on/off.
- **Time-based rollout / schedule** — no `enable_at` / `disable_at` columns yet.
- **Multi-tenant flags** — single-tenant (BuildCv owns the flag space).
- **Flag analytics / telemetry** — no metrics on flag evaluations in this PR; can be added as a decorator later.
- **Migration of 012's `Wompi:Environment`** (sandbox vs production) — that's a 3-state config, not a boolean flag. Stays as `IOptions<WompiOptions>`.

## Decisions (locked)

| # | Decision | Rationale | Constitution |
|---|----------|-----------|--------------|
| **1** | **Storage: hybrid (appsettings default + DB override)** | Backward compatible with 011/012/013 deploys. DB value takes precedence at read time. Env-based default survives DB outages. | Art. III (operational metadata, no PII in flag rows). |
| **2** | **API surface: single `IFeatureFlag.IsEnabled(string name)` method (async, returns bool)** | One method, one name lookup. Async prepares for future DB call without forcing all current callers to be synchronous. `FeatureFlagNotFoundException` for unknown names (configurable to return `false` instead). | Art. VI (port shape, single responsibility). |
| **3** | **Migration: 3 existing flags migrated, appsettings still works as default** | `Factus:Enabled` → `feature-flags.factus.enabled` (default = current appsettings value). `Wompi:Enabled` → `feature-flags.wompi.enabled`. `Credits:Enabled` → `feature-flags.credits.enabled`. Old keys preserved as defaults; `FeatureFlagMigrationService` runs once on first boot to seed the table. **Zero-downtime deploy**: if DB migration hasn't run, the app still works from appsettings. | Art. VI (backward compatibility is part of the port contract). |
| **4** | **Audit log: `feature_flag_audit_log` table, append-only** | Every admin change writes one row with `id`, `flag_name`, `old_value`, `new_value`, `changed_by` (user id, NOT email/name), `changed_at`, `reason` (optional string). No updates, no deletes. Compliance evidence for Art. IX. | Art. IX FR-046/048/049 (audit trail for compliance). |
| **5** | **Admin API: `PUT /api/v1/admin/feature-flags/{name}` + `GET /api/v1/admin/feature-flags`** | PUT requires admin role (re-use 009-auth JWT + role claim). Writes audit log in same transaction. Returns 200 with new value. **Separate rate limit policy** `"admin"` 30/min (Art. VII — admin endpoints are sensitive). | Art. VI (REST endpoint in `BuildCv.Api`), Art. VII (rate limit), Art. IX (audit on every change). |
| **6** | **Caching: in-memory `IMemoryCache` with 60s TTL** | Avoids DB roundtrip on every `IsEnabled` call. TTL is short enough that operators see changes within a minute. Explicit `Invalidate(name)` on admin update so admin changes propagate immediately (don't wait for TTL). | Art. VI (decorator pattern, no Domain changes), Art. VII (perf budget respected). |
| **7** | **No new NuGet dependencies** | `IMemoryCache` ships in `Microsoft.Extensions.Caching.Memory` (already in the API project). `Microsoft.Extensions.Options` already used by all 3 existing flags. EF Core already used by 010-persistence. Zero new transitive deps. | Art. VI (no over-engineering, no new package surface). |

## Architecture (locked)

### Domain (`BuildCv.Domain`) — 0 packages, pure C#

```csharp
// BuildCv.Domain/FeatureFlags/
public sealed record FeatureFlag
{
    public string Name { get; init; }            // PK, kebab-case e.g. "factus-enabled"
    public bool DefaultValue { get; init; }      // from appsettings.json
    public bool CurrentValue { get; init; }      // from DB, falls back to DefaultValue
    public string? Description { get; init; }    // optional, operator-facing
    public DateTime UpdatedAt { get; init; }
    public uint Xmin { get; init; }              // EF shadow concurrency token (Postgres)
}

public sealed record FeatureFlagAuditLog
{
    public Guid Id { get; init; }                // PK
    public string FlagName { get; init; }
    public bool OldValue { get; init; }
    public bool NewValue { get; init; }
    public Guid ChangedBy { get; init; }         // user id, NEVER email/name
    public DateTime ChangedAt { get; init; }
    public string? Reason { get; init; }         // optional free text
}

public sealed class FeatureFlagNotFoundException(string name)
    : Exception($"Feature flag '{name}' is not registered.");
```

### Application (`BuildCv.Application`) — 0 packages, ports only

```csharp
// BuildCv.Application/Common/IFeatureFlag.cs (single port, replaces 3 bespoke shapes)
public interface IFeatureFlag
{
    Task<bool> IsEnabled(string name, CancellationToken ct = default);
}

// BuildCv.Application/FeatureFlags/Ports/IFeatureFlagStore.cs
public interface IFeatureFlagStore
{
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct);
    Task UpsertDefaultAsync(string name, bool defaultValue, string? description, CancellationToken ct);
    Task UpdateCurrentAsync(string name, bool newValue, CancellationToken ct);  // throws DbUpdateConcurrencyException on xmin mismatch
}

// BuildCv.Application/FeatureFlags/Ports/IFeatureFlagAdminService.cs
public interface IFeatureFlagAdminService
{
    Task UpdateFlagAsync(string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct);
}
```

**3 handlers** in `BuildCv.Application/FeatureFlags/`:

1. `GetFeatureFlagHandler(string name)` → `FeatureFlag?` — used by GET endpoint + admin UI.
2. `ListFeatureFlagsHandler()` → `IReadOnlyList<FeatureFlag>` — used by GET endpoint.
3. `UpdateFeatureFlagHandler(string name, bool newValue, Guid changedBy, string? reason)` → uses `IFeatureFlagAdminService`, writes audit + invalidates cache in same transaction.

**Backwards-compat shim**: the 3 existing `IInvoiceProvider?` / `IPaymentProvider (active vs Disabled)` / `ICreditsFeatureFlag` consumers keep their existing interfaces. A thin **adapter** wraps each:

```csharp
// BuildCv.Application/Common/FeatureFlagAdapter.cs (one for each legacy shape)
public sealed class FeatureFlagInvoiceAdapter(IFeatureFlag flags) : IInvoiceProvider?
{
    // returns null if "factus-enabled" is false, otherwise returns the configured provider
}
```

**Why adapter not delete?** The 3 features shipped with their existing public contract. Removing `ICreditsFeatureFlag` would force 013 to change its public surface (breaking change for any external consumer). The adapter keeps 011/012/013 untouched at the consumer level.

### Infrastructure (`BuildCv.Infrastructure`)

| Component | Responsibility |
|-----------|----------------|
| `EfFeatureFlagStore` | EF adapter for `IFeatureFlagStore`. Uses `BuildCvDbContext` (already registered). Reads/writes `feature_flags` table. |
| `InMemoryFeatureFlagStore` | Test-only. Stores flags in a `ConcurrentDictionary<string, FeatureFlag>`. |
| `CachingFeatureFlagDecorator` | Wraps `EfFeatureFlagStore`. `IMemoryCache` with 60s TTL. `Invalidate(name)` public method called by admin update handler. |
| `FeatureFlagMigrationService` | `IHostedService` runs once on startup. Seeds `feature_flags` table from `appsettings.json` defaults: `Factus:Enabled`, `Wompi:Enabled`, `Credits:Enabled`. Idempotent (`upsert` on name). |
| `FeatureFlagOptions` (`IConfiguration` binder) | Maps `FeatureFlags:Defaults:{name}` section to dictionary for the migration seed. |

**EF migration**: `AddFeatureFlags` adds 2 tables (`feature_flags`, `feature_flag_audit_log`) with proper indexes + `xmin` concurrency on `feature_flags`. Migration runs at startup per existing 010-persistence convention.

### API (`BuildCv.Api`)

```csharp
// BuildCv.Api/Endpoints/FeatureFlagAdminEndpoints.cs
public static class FeatureFlagAdminEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/feature-flags")
            .RequireAuthorization("Admin")  // new policy from 009-auth
            .RequireRateLimiting("admin");   // 30/min (Art. VII)

        group.MapGet("/", ListFeatureFlagsHandler);       // GET /api/v1/admin/feature-flags
        group.MapGet("/{name}", GetFeatureFlagHandler);   // GET /api/v1/admin/feature-flags/{name}
        group.MapPut("/{name}", UpdateFeatureFlagHandler);// PUT /api/v1/admin/feature-flags/{name}
        return app;
    }
}
```

**`PUT /api/v1/admin/feature-flags/{name}` contract**:

```json
// Request
{ "newValue": true, "reason": "Enable Factus for production DIAN rollout" }

// Response 200
{ "name": "factus-enabled", "currentValue": true, "updatedAt": "2026-06-25T..." }

// Response 404 — flag not registered
{ "type": "https://...", "title": "Feature flag not found", "status": 404, "code": "FEATURE_FLAG_NOT_FOUND" }

// Response 409 — concurrent update (xmin mismatch)
{ "type": "https://...", "title": "Concurrent flag update", "status": 409, "code": "FEATURE_FLAG_CONFLICT" }
```

**DI registration** (`BuildCv.Api/Program.cs`):

```csharp
// Registers: IFeatureFlag → CachingFeatureFlagDecorator → EfFeatureFlagStore → BuildCvDbContext
builder.Services.AddFeatureFlags(builder.Configuration);
// Adds: IFeatureFlagStore (Ef + InMemory), IFeatureFlagAdminService,
//       IHostedService → FeatureFlagMigrationService,
//       3 handlers, endpoint mapping
```

**One-time migration note**: existing 011/012/013 call sites **do not change**. The adapters handle the translation. New code uses `IFeatureFlag` directly.

### Web (`BuildCv-web`) — **NO CHANGES**

Backend-only feature. The admin API is consumed by operators via curl / scripts until v1.5 web UI.

## Risks

| # | Risk | Likelihood | Mitigation |
|---|------|------------|------------|
| **1** | **Cache staleness on admin updates** — 60s TTL means admin flips could take up to 60s to propagate to all app instances. | Med | `CachingFeatureFlagDecorator.Invalidate(name)` is called synchronously inside the admin handler after the DB write commits. Admin changes propagate within milliseconds to the local instance. Multi-instance propagation is bounded by 60s TTL. Acceptable for v1 (operators see the change reflected in their session instantly; downstream instances catch up within a minute). |
| **2** | **Migration risk on first deploy** — if `AddFeatureFlags` EF migration runs BEFORE the code that handles "DB-missing → appsettings fallback", existing 011/012/013 could 500. | Med | The fallback path is implemented in `CachingFeatureFlagDecorator` BEFORE the migration is required: `EfFeatureFlagStore.GetAsync(name)` returns `null` on missing table, decorator falls back to `FeatureFlagOptions` (appsettings). Code is backwards-compatible regardless of migration order. `FeatureFlagMigrationService` is `IHostedService` that runs after app starts — failure is logged, not fatal. |
| **3** | **Concurrency on flag update (two admins simultaneously)** — last-writer-wins could overwrite an intentional flip. | Low | EF shadow `xmin` column on `feature_flags` (proven in `PaymentConfiguration.cs` from 012-wompi). `DbUpdateConcurrencyException` mapped to HTTP 409. Client retries with fresh read. |
| **4** | **Audit log unbounded growth** — append-only table grows forever. | Med | Document as deferred (retention policy in v1.5). For v1, log volume is low (operators flip 1-2 flags per week). Add `created_at` index now so a future retention cron is cheap. |
| **5** | **Breaking change risk for 011/012/013** — adapter pattern could mask issues if adapters misread flag state. | Med | Adapter behavior is **fully covered by integration tests** that re-run the 011-factus, 012-wompi, and 013-credit-consumption test suites unchanged (proves no regression). New adapter-specific tests verify the `IFeatureFlag.IsEnabled("factus-enabled") → IInvoiceProvider?.Provider` mapping. |

## Compliance

| Article | How 015 complies |
|---------|------------------|
| **I (Cero invención)** | N/A. 015 is system infrastructure. The score engine, parser, and adapt pipeline are untouched. |
| **II (Determinismo)** | N/A. The score engine is untouched. `IFeatureFlag.IsEnabled` is process-stable per `IsEnabled` call within the cache TTL (single boolean return). |
| **III (Privacidad primero)** | `FeatureFlagAuditLog.ChangedBy` is a `Guid` user id — never email, name, or IP. No CV/job content anywhere in this feature. Logs use the 011/012 pattern: `flagName`, `oldValue`, `newValue`, `changedBy`, `traceId`. |
| **IV (Encuadre honesto)** | Admin API returns raw boolean + description. No "advanced AI" copy. |
| **V (Entrada como dato)** | N/A. Flag names are config-time constants, not user input. |
| **VI (Clean Architecture)** | `IFeatureFlag`, `IFeatureFlagStore`, `IFeatureFlagAdminService` ports in `BuildCv.Application/Common/`. `EfFeatureFlagStore` + `CachingFeatureFlagDecorator` + `FeatureFlagMigrationService` in `BuildCv.Infrastructure`. Domain stays pure (verified by `dotnet list src/BuildCv.Domain package references` returning 0). |
| **VII (Rate limits)** | New policy `"admin"` 30/min/IP for `PUT /api/v1/admin/feature-flags/{name}` (mirrors Art. VII's differentiation by cost). Admin endpoints are sensitive; lower limit is intentional. |
| **VIII (TDD)** | Red-green-refactor on every handler, decorator, and adapter. Adapter tests re-run 011/012/013 suites to prove no regression. |
| **IX (Habeas Data)** | **Access:** admin API lists all flags. **Rectification:** flag values are mutable. **Cancellation:** N/A (no user data). **Consent:** N/A (operational config). **Audit:** every flag change is logged with `changed_by`, `old_value`, `new_value`, `changed_at`, `reason`. Compliance evidence for emergency kill-switches. |

## Delivery Strategy

**3 chained PRs (matching 013-credit-consumption pattern), each keeps build+test green, each under 400 lines diff.**

| PR | Scope | Approx lines | Commits |
|----|-------|--------------|---------|
| **PR1** | Domain (`FeatureFlag`, `FeatureFlagAuditLog`, `FeatureFlagNotFoundException`) + Application (`IFeatureFlag`, `IFeatureFlagStore`, `IFeatureFlagAdminService`, 3 handlers) + unit tests | ~200 | 3-4 commits (red→green→refactor per handler) |
| **PR2** | Infrastructure (`EfFeatureFlagStore`, `InMemoryFeatureFlagStore`, `CachingFeatureFlagDecorator`, `FeatureFlagMigrationService`, `FeatureFlagOptions`) + EF migration (`AddFeatureFlags`) + DI registration + tests | ~250 | 4-5 commits (migration + adapter + cache decorator + migration service) |
| **PR3** | API (`FeatureFlagAdminEndpoints` GET/PUT, admin auth policy, rate limit policy) + 3 adapter classes (`FeatureFlagInvoiceAdapter`, `FeatureFlagPaymentAdapter`, `FeatureFlagCreditsAdapter`) + integration tests (rerun 011/012/013 suites) | ~150 | 3-4 commits (endpoint + auth + adapters + full integration run) |

**Work only on `main`**, direct merge per project rules. Each PR's `main` is the previous PR's `main` (feature-branch-chain pattern, not stacked).

**Per PR gates (must all pass):**

1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors).
2. `dotnet format --verify-no-changes`.
3. `dotnet test -c Release --no-build` — 451+ existing pass, new tests pass, **011/012/013 test suites re-run unchanged**.
4. `constitution-check.sh` — no Art. I-IX violations.
5. `./scripts/preflight.sh` — full pipeline green.

## Open Questions (for proposal-review time)

These decisions are locked, but the spec/design phases will need implementation-level answers. Surfacing here so the user can correct framing before artifact-writing:

1. **Admin role definition** — does 015 introduce a new `"admin"` role claim in 009-auth JWTs, or does it re-use an existing role (e.g., operator / staff)? Default: introduce a new `"admin"` role claim with a single migration to existing tokens.
2. **Cache TTL exact value** — 60s is the proposal default. Could be 30s (more responsive, more DB load) or 300s (less load, slower propagation). Default: 60s.
3. **Audit log retention** — "indefinite for v1" is the proposal default. Operator could request 90-day retention with a cron. Default: indefinite, document deferred.
4. **Flag naming convention for new flags** — kebab-case (`factus-enabled`) or snake_case (`factus_enabled`)? Default: **kebab-case** (matches HTTP URL path convention).
5. **Web UI for admin** — confirm out-of-scope for 015 (deferred to v1.5)? Default: yes, API only.

## Next

`sdd-spec` → write `spec.md` with 6+ requirements (R1: `IFeatureFlag` port + handler, R2: `feature_flags` table + EF migration, R3: caching decorator, R4: admin API + role + rate limit, R5: audit log, R6: backward-compat migration of 011/012/013) + scenarios using `Given/When/Then`.

Then `sdd-design` → ports, EF migration SQL, endpoint filter implementation, adapter pattern per legacy flag.

Then `sdd-tasks` → forecast 400-line budget, recommend 3 chained PRs, lock the work-unit commits per PR.

Then `sdd-apply` → 3 chained PRs, each green, each mergeable on `main`.

Then `sdd-verify` → re-run 011/012/013 full test suites to prove no regression.

Then `sdd-archive` → tag `015-feature-flags-v1.0` after PR3 merged.

## References

- **Existing flag interfaces:** `BuildCv-api/src/BuildCv.Application/Common/ICreditsFeatureFlag.cs` + `BuildCv-api/src/BuildCv.Infrastructure/Credits/CreditsFeatureFlag.cs` (013 pattern).
- **011-factus pattern:** `BuildCv-api/specs/011-factus/spec.md` (Section NFR-001: `Factus:Enabled=false` runtime mode).
- **012-wompi pattern:** `BuildCv-api/specs/012-wompi/spec.md` (Section R6: `Wompi:Enabled` gating, `DisabledPaymentProvider`).
- **013-credit-consumption pattern:** `BuildCv-api/specs/013-credit-consumption/spec.md` + `proposal.md` (canonical template).
- **Persistence layer (for EF):** `BuildCv-api/specs/010-persistence/spec.md`.
- **Auth (for admin role):** `BuildCv-api/specs/009-auth/spec.md`.
- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` v1.2.0.
- **Work-unit commits skill:** `~/.config/opencode/skills/work-unit-commits/SKILL.md`.
- **Chained PR skill:** `~/.config/opencode/skills/chained-pr/SKILL.md`.
