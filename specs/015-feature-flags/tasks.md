# Tasks: 015-feature-flags

## Status

[Tasks] — Ready to apply (3 chained PRs)

## Review workload forecast

- **Total estimated diff**: ~600 lines (3 PRs)
- **400-line budget risk**: MEDIUM (PR2 close to threshold at ~250 lines)
- **Chained PRs recommended**: Yes (matches 013-credit-consumption pattern)
- **Strategy**: 3 PRs — PR1 Domain + Application, PR2 Infrastructure + DB, PR3 API + adapters
- **Each PR keeps build + test green** (gate per PR)
- **Backward compat**: 011/012/013 test suites must pass unchanged after PR3

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

## PR boundaries (locked)

| PR | Scope | Estimated diff | Files (new) | Files (modified) | Test additions |
|----|-------|----------------|-------------|------------------|----------------|
| **PR1** | Domain + Application | ~200 lines | `FeatureFlag.cs`, `FeatureFlagAuditLog.cs`, `FeatureFlagNotFoundException.cs`, `IFeatureFlag.cs`, `IFeatureFlagStore.cs`, `IFeatureFlagAdminService.cs`, `FeatureFlagsOptions.cs`, `GetFeatureFlagHandler.cs`, `ListFeatureFlagsHandler.cs`, `UpdateFeatureFlagHandler.cs`, `GetFeatureFlagAuditLogHandler.cs` | (none) | +20 unit tests (7 domain + 13 application) |
| **PR2** | Infrastructure + DB | ~250 lines | `EfFeatureFlagStore.cs`, `InMemoryFeatureFlagStore.cs`, `CachingFeatureFlagDecorator.cs`, `FeatureFlagAdminService.cs`, `FeatureFlagMigrationService.cs`, `FeatureFlagConfiguration.cs`, `FeatureFlagAuditLogConfiguration.cs`, `20260625_AddFeatureFlags.cs`, `FeatureFlagInvoiceAdapter.cs`, `FeatureFlagPaymentAdapter.cs`, `FeatureFlagCreditsAdapter.cs` | `BuildCvDbContext.cs`, `DependencyInjection.cs`, `appsettings.json` | +15 integration tests |
| **PR3** | API | ~150 lines | `FeatureFlagAdminEndpoints.cs`, `AuthPolicies.cs`, `RateLimitPolicies.cs` | `Program.cs` (AddAuthPolicies + AddRateLimitPolicies + MapFeatureFlagAdminEndpoints), `InvoiceEndpoints.cs` DI wire (011), `PaymentEndpoints.cs` DI wire (012) | +10 e2e tests |

## PR1: Domain + Application (~200 lines, +20 unit tests)

### T1.1 — Domain: `FeatureFlag` entity
- **Files**: `BuildCv-api/src/BuildCv.Domain/FeatureFlags/FeatureFlag.cs`
- **Tests** (TDD, 4):
  - `FeatureFlag_Create_RequiresName` — empty/whitespace → `ArgumentException`
  - `FeatureFlag_Create_NameExceedsHundredChars_Throws` — name > 100 → `ArgumentException`
  - `FeatureFlag_Create_DefaultsCurrentValueToDefaultValue` — `CurrentValue == DefaultValue` on construction
  - `FeatureFlag_Create_SetsUpdatedAtToUtcNow` — `UpdatedAt` ≈ `DateTime.UtcNow` within seconds

### T1.2 — Domain: `FeatureFlagAuditLog` entity
- **Files**: `BuildCv-api/src/BuildCv.Domain/FeatureFlags/FeatureFlagAuditLog.cs`
- **Tests** (TDD, 4):
  - `FeatureFlagAuditLog_Create_RequiresFlagName` — empty/whitespace → `ArgumentException`
  - `FeatureFlagAuditLog_Create_RequiresChangedBy` — `Guid.Empty` → `ArgumentException`
  - `FeatureFlagAuditLog_Create_ReasonExceeds500Chars_Throws`
  - `FeatureFlagAuditLog_Create_DefaultsChangedAtToUtcNow`

### T1.3 — Domain: `FeatureFlagNotFoundException`
- **Files**: `BuildCv-api/src/BuildCv.Domain/FeatureFlags/FeatureFlagNotFoundException.cs`
- **Tests** (TDD, 1):
  - `FeatureFlagNotFoundException_IncludesFlagName` — `ex.FlagName == "wompi-enabled"` and message contains name

### T1.4 — Application: `IFeatureFlag` + `IFeatureFlagStore` + `IFeatureFlagAdminService` ports
- **Files**:
  - `BuildCv-api/src/BuildCv.Application/Common/IFeatureFlag.cs`
  - `BuildCv-api/src/BuildCv.Application/Common/IFeatureFlagStore.cs`
  - `BuildCv-api/src/BuildCv.Application/Common/IFeatureFlagAdminService.cs`
  - `BuildCv-api/src/BuildCv.Application/Common/FeatureFlagsOptions.cs`
- **Tests** (TDD, 4 — interface contract via NSubstitute / fake):
  - `IFeatureFlag_IsEnabledAsync_HonorsCancellation`
  - `IFeatureFlagStore_UpsertAsync_HonorsCancellation`
  - `IFeatureFlagStore_AppendAuditLogAsync_HonorsCancellation`
  - `FeatureFlagsOptions_DefaultsCacheTtlToSixtySeconds`

### T1.5 — Application: `GetFeatureFlagHandler` (TDD)
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/FeatureFlags/GetFeatureFlagHandler.cs`
- **Tests** (3):
  - `GetFeatureFlagHandler_ReturnsFlag_WhenExists`
  - `GetFeatureFlagHandler_ReturnsNull_WhenNotFound`
  - `GetFeatureFlagHandler_PropagatesCancellation`

### T1.6 — Application: `ListFeatureFlagsHandler` (TDD)
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/FeatureFlags/ListFeatureFlagsHandler.cs`
- **Tests** (2):
  - `ListFeatureFlagsHandler_ReturnsAllFlagsFromStore`
  - `ListFeatureFlagsHandler_ReturnsEmptyList_WhenStoreIsEmpty`

### T1.7 — Application: `UpdateFeatureFlagHandler` (TDD)
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/FeatureFlags/UpdateFeatureFlagHandler.cs`
- **Tests** (4):
  - `UpdateFeatureFlagHandler_CallsAdminService_ThenInvalidatesCache`
  - `UpdateFeatureFlagHandler_LogsOldAndNewValue`
  - `UpdateFeatureFlagHandler_PropagatesFeatureFlagNotFoundException`
  - `UpdateFeatureFlagHandler_PropagatesDbUpdateConcurrencyException`

### T1.8 — Application: `GetFeatureFlagAuditLogHandler` (TDD)
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/FeatureFlags/GetFeatureFlagAuditLogHandler.cs`
- **Tests** (4):
  - `GetFeatureFlagAuditLogHandler_ReturnsEntries_ForFlag`
  - `GetFeatureFlagAuditLogHandler_PassesLimitAndCursor_ToStore`
  - `GetFeatureFlagAuditLogHandler_DefaultsLimitToFifty_WhenNull`
  - `GetFeatureFlagAuditLogHandler_PropagatesCancellation`

### PR1 acceptance
- [ ] All 20+ unit tests pass (target: 650 → 670 = 630 + 20 + 20 from PR3)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] `dotnet build -c Release` 0 warnings
- [ ] `dotnet list src/BuildCv.Domain package references` returns 0 (Domain purity invariant, Art. VI)
- [ ] `dotnet test` green — existing 630 still pass
- [ ] **Work-unit commits**:
  - `test(015): tests rojos de dominio (FeatureFlag, FeatureFlagAuditLog, Exception)`
  - `feat(015): dominio — FeatureFlag + FeatureFlagAuditLog + Exception`
  - `feat(015): application — IFeatureFlag + IFeatureFlagStore + IFeatureFlagAdminService + FeatureFlagsOptions`
  - `test(015): tests rojos de handlers (Get, List, Update, AuditLog)`
  - `feat(015): application — 4 handlers`
  - `chore(015): refactor + verificación constitution-check`
- [ ] PR merges to `main` (no feature branch)

## PR2: Infrastructure + DB (~250 lines, +15 integration tests)

### T2.1 — EF Core: `FeatureFlagConfiguration` + `FeatureFlagAuditLogConfiguration` + DbContext
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Configurations/FeatureFlagAuditLogConfiguration.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs` (MODIFY — add `DbSet<FeatureFlag>`, `DbSet<FeatureFlagAuditLog>`, apply configurations)
- **Tests** (TDD, 5 — uses Testcontainers PostgreSQL or in-memory):
  - `FeatureFlagConfiguration_MapsToTable_feature_flags`
  - `FeatureFlagConfiguration_HasPrimaryKey_Name`
  - `FeatureFlagConfiguration_HasRowVersion_xmin`
  - `FeatureFlagAuditLogConfiguration_MapsToTable_feature_flag_audit_log`
  - `FeatureFlagAuditLogConfiguration_HasIndex_FlagName_ChangedAt`

### T2.2 — Migration: `20260625_AddFeatureFlags`
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625_AddFeatureFlags.cs`
- **Tests** (3):
  - `Migration_Up_CreatesTable_feature_flags_WithConstraints`
  - `Migration_Up_CreatesTable_feature_flag_audit_log_WithIndex`
  - `Migration_Down_DropsBothTables`

### T2.3 — Infrastructure: `EfFeatureFlagStore` + `InMemoryFeatureFlagStore`
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/EfFeatureFlagStore.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/InMemoryFeatureFlagStore.cs` (NEW — test-only double)
- **Tests** (TDD, 8 — 4 EF integration + 4 InMemory unit):
  - `EfFeatureFlagStore_GetAsync_ReturnsFlag_WhenExists`
  - `EfFeatureFlagStore_GetAsync_ReturnsNull_WhenNotFound`
  - `EfFeatureFlagStore_UpsertAsync_PersistsToDb_OnFirstInsert`
  - `EfFeatureFlagStore_UpsertAsync_UpdatesCurrentValue_OnExisting`
  - `EfFeatureFlagStore_AppendAuditLogAsync_WritesEntry`
  - `EfFeatureFlagStore_GetAuditLogAsync_PaginatesCorrectly_WithCursor`
  - `EfFeatureFlagStore_GetAuditLogAsync_ClampsLimitTo200`
  - `InMemoryFeatureFlagStore_MirrorsEfBehavior_ForTests`

### T2.4 — Infrastructure: `CachingFeatureFlagDecorator` + `FeatureFlagAdminService`
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/CachingFeatureFlagDecorator.cs` (NEW — `IMemoryCache` 60s TTL, `Invalidate(name)`)
  - `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/FeatureFlagAdminService.cs` (NEW — transaction-wrapped update + audit log)
- **Tests** (5):
  - `CachingFeatureFlagDecorator_IsEnabledAsync_CachesResult_ForTtl`
  - `CachingFeatureFlagDecorator_Invalidate_RemovesCacheEntry`
  - `CachingFeatureFlagDecorator_TtlExpires_RefreshesFromDb`
  - `CachingFeatureFlagDecorator_FallsBackToAppsettings_WhenDbReturnsNull`
  - `FeatureFlagAdminService_UpdateAsync_WritesAuditLog_InSameTransaction`

### T2.5 — Infrastructure: 3 backward-compat adapters
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Invoicing/FeatureFlagInvoiceAdapter.cs` (NEW — wraps `IInvoiceProvider`, checks `"factus-enabled"`)
  - `BuildCv-api/src/BuildCv.Infrastructure/Payments/FeatureFlagPaymentAdapter.cs` (NEW — wraps `IPaymentProvider`, checks `"wompi-enabled"`)
  - `BuildCv-api/src/BuildCv.Infrastructure/Credits/FeatureFlagCreditsAdapter.cs` (NEW — implements `ICreditsFeatureFlag`, delegates to `"credits-enabled"`)
- **Tests** (3):
  - `FeatureFlagInvoiceAdapter_UsesLocalProvider_WhenFlagDisabled`
  - `FeatureFlagPaymentAdapter_ReturnsDisabled_WhenFlagDisabled`
  - `FeatureFlagCreditsAdapter_DelegatesToFeatureFlag`

### T2.6 — Infrastructure: `FeatureFlagMigrationService` (IHostedService)
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/FeatureFlagMigrationService.cs` (NEW)
- **Tests** (3):
  - `FeatureFlagMigrationService_SeedsThreeRows_FromAppsettingsDefaults`
  - `FeatureFlagMigrationService_IsIdempotent_OnRerun`
  - `FeatureFlagMigrationService_LogsButDoesNotThrow_OnFailure`

### T2.7 — DI Registration + Configuration
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (MODIFY — register `IFeatureFlag` → `CachingFeatureFlagDecorator`, `IFeatureFlagStore` → `EfFeatureFlagStore`, `IFeatureFlagAdminService` → `FeatureFlagAdminService`, `IHostedService` → `FeatureFlagMigrationService`, `ICreditsFeatureFlag` → `FeatureFlagCreditsAdapter`)
  - `BuildCv-api/src/BuildCv.Api/appsettings.json` (MODIFY — add `FeatureFlags` section with `CacheTtlSeconds: 60` and `Defaults: { factus-enabled, wompi-enabled, credits-enabled }`)
- **Tests** (2):
  - `DependencyInjection_RegistersIFeatureFlag_AsScoped`
  - `DependencyInjection_RegistersFeatureFlagMigration_AsHostedService`

### PR2 acceptance
- [ ] All 15+ integration tests pass
- [ ] EF migration applies cleanly (`dotnet ef database update` succeeds in dev)
- [ ] 011/012/013 test suites still pass unchanged (adapter pattern preserves contracts)
- [ ] DI registered, app starts
- [ ] `dotnet test` green
- [ ] `dotnet format --verify-no-changes` clean
- [ ] **Work-unit commits**:
  - `test(015): tests rojos EF configuration + DbContext`
  - `feat(015): infrastructure — EF configuration + DbContext`
  - `feat(015): infrastructure — migration AddFeatureFlags (20260625)`
  - `test(015): tests rojos EfFeatureFlagStore + InMemoryFeatureFlagStore`
  - `feat(015): infrastructure — EfFeatureFlagStore + InMemoryFeatureFlagStore`
  - `test(015): tests rojos CachingFeatureFlagDecorator + AdminService`
  - `feat(015): infrastructure — CachingFeatureFlagDecorator + FeatureFlagAdminService`
  - `feat(015): infrastructure — 3 backward-compat adapters`
  - `feat(015): infrastructure — FeatureFlagMigrationService + DI registration + appsettings`
  - `test(015): integration tests (15+)`
- [ ] PR merges to `main`

## PR3: API (~150 lines, +10 e2e tests)

### T3.1 — API: `AuthPolicies` + `RateLimitPolicies`
- **Files**:
  - `BuildCv-api/src/BuildCv.Api/Auth/AuthPolicies.cs` (NEW or MODIFY — add `Admin` policy requiring `RequireRole("admin")`)
  - `BuildCv-api/src/BuildCv.Api/RateLimiting/RateLimitPolicies.cs` (NEW or MODIFY — add `Admin` fixed-window 30/min/IP)
- **Tests** (TDD, 3):
  - `AuthPolicies_Admin_RequiresAuthenticatedUserAndRole`
  - `RateLimitPolicies_Admin_PermitsThirtyRequestsPerMinute`
  - `RateLimitPolicies_Admin_ReturnsRetryAfterHeader_On429`

### T3.2 — API: `FeatureFlagAdminEndpoints` + DTOs
- **Files**: `BuildCv-api/src/BuildCv.Api/Endpoints/FeatureFlagAdminEndpoints.cs` (NEW — GET list, GET single, PUT update, GET audit-log)
- **Tests** (TDD, 7):
  - `FeatureFlagAdminEndpoints_List_Returns200_WithValidAdminAuth`
  - `FeatureFlagAdminEndpoints_Get_Returns200_WhenFlagExists`
  - `FeatureFlagAdminEndpoints_Get_Returns404_WhenFlagMissing`
  - `FeatureFlagAdminEndpoints_Put_Returns200_OnSuccess`
  - `FeatureFlagAdminEndpoints_Put_Returns404_ForUnknownFlag`
  - `FeatureFlagAdminEndpoints_Put_Returns409_OnConcurrentUpdate`
  - `FeatureFlagAdminEndpoints_AuditLog_Returns200_Paginated`

### T3.3 — API: `Program.cs` wiring
- **File**: `BuildCv-api/src/BuildCv.Api/Program.cs` (MODIFY)
  - Add `builder.Services.AddAuthPolicies()`
  - Add `builder.Services.AddRateLimitPolicies()`
  - Add `app.MapFeatureFlagAdminEndpoints()` after `app.Build()`

### T3.4 — API: Wire adapters in DI for 011/012
- **Files**:
  - `BuildCv-api/src/BuildCv.Api/Endpoints/InvoiceEndpoints.cs` (MODIFY — switch DI registration to `FeatureFlagInvoiceAdapter`)
  - `BuildCv-api/src/BuildCv.Api/Endpoints/PaymentEndpoints.cs` (MODIFY — switch DI registration to `FeatureFlagPaymentAdapter`)
- **Tests** (3):
  - `InvoiceEndpoints_RerunUnchanged_AfterAdapterWired` (re-run 011 test suite)
  - `PaymentEndpoints_RerunUnchanged_AfterAdapterWired` (re-run 012 test suite)
  - `CreditEndpoints_RerunUnchanged_AfterAdapterWired` (re-run 013 test suite)

### PR3 acceptance
- [ ] All 10+ e2e tests pass
- [ ] Admin endpoints require `admin` role + rate-limited at 30/min/IP
- [ ] 011/012/013 backward compat verified (existing test suites green — proves zero regression)
- [ ] 6 gates green: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`, `constitution-check.sh`, `preflight.sh`, `dotnet list src/BuildCv.Domain package references` returns 0
- [ ] **Work-unit commits**:
  - `test(015): tests rojos admin endpoints (GET list, GET single, PUT, GET audit-log, 401, 403, 404, 409, 429)`
  - `feat(015): api — AuthPolicies + RateLimitPolicies`
  - `feat(015): api — FeatureFlagAdminEndpoints + DTOs`
  - `feat(015): api — Program.cs wiring + adapter DI rewiring for 011/012`
  - `test(015): re-run 011/012/013 e2e suites para probar zero regression`
  - `chore(015): preflight verde + constitution-check + git tag 015-feature-flags-v1.0`
- [ ] PR merges to `main` + tag `015-feature-flags-v1.0`

## Test count forecast

| Phase | Before 015 | After 015 | Delta |
|-------|------------|-----------|-------|
| API unit (App) | 194 | 194 + 13 = 207 | +13 |
| API unit (Domain) | 122 | 122 + 9 = 131 | +9 |
| API integration (Infra) | 93 | 93 + 15 = 108 | +15 |
| API e2e | 83 | 83 + 10 = 93 | +10 |
| **API total** | **630** | **677** | **+47** |
| Web (no changes) | 745 | 745 | 0 |
| E2E Playwright (no changes) | 79 | 79 | 0 |
| **TOTAL** | **1454** | **1501** | **+47** |

> **Note**: Forecast includes 5 unit + 5 integration + 5 e2e buffer above spec's 20+15+10 minimum (45 total). Extra headroom for concurrency, cancellation, and edge cases.

## Dependency graph (per PR)

```
PR1 (Domain + Application)
  ├── T1.1-T1.3: Domain entities + exception (no deps)
  ├── T1.4: Application ports + options (depend on T1.1-T1.3)
  └── T1.5-T1.8: 4 handlers (depend on T1.4)
PR1 → PR2 (blocked until PR1 merges)

PR2 (Infrastructure + DB)
  ├── T2.1: EF config + DbContext (depends on PR1)
  ├── T2.2: Migration (depends on T2.1)
  ├── T2.3: Stores (EfFeatureFlagStore + InMemoryFeatureFlagStore) (depend on T2.1)
  ├── T2.4: Caching + Admin (depend on T2.3)
  ├── T2.5: 3 backward-compat adapters (depend on T2.4)
  ├── T2.6: Migration service (depends on T2.2)
  └── T2.7: DI registration + appsettings (depends on T2.4, T2.5, T2.6)
PR2 → PR3 (blocked until PR2 merges)

PR3 (API)
  ├── T3.1: AuthPolicies + RateLimitPolicies (depend on PR2)
  ├── T3.2: FeatureFlagAdminEndpoints + DTOs (depend on PR2)
  ├── T3.3: Program.cs wiring (depends on T3.1, T3.2)
  └── T3.4: Wire adapters in DI for 011/012 (depends on T3.3)
```

## Critical execution order

1. **PR1 first** (T1.1 → T1.2 → T1.3 → T1.4 → T1.5 → T1.6 → T1.7 → T1.8)
2. **PR2 second** (T2.1 → T2.2 → T2.3 → T2.4 → T2.5 → T2.6 → T2.7)
3. **PR3 last** (T3.1 → T3.2 → T3.3 → T3.4)

Each PR's `dotnet test` MUST be green before merge. **Per PR gates (all must pass):**

1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors)
2. `dotnet format --verify-no-changes`
3. `dotnet test -c Release --no-build` — existing 630 pass + new tests pass + 011/012/013 suites rerun unchanged after PR3
4. `constitution-check.sh` — no Art. I-IX violations
5. `./scripts/preflight.sh` — full pipeline green
6. `dotnet list src/BuildCv.Domain package references` — 0 packages (Domain purity, Art. VI)

## Conventions per PR

- **Conventional commits** in Spanish, no AI attribution
- **Work-unit commits** (1 commit per logical group, not per file)
- **Branch**: only `main` (no feature branches)
- **Direct merge** to main
- **No force-push**, no interactive rebase
- **Pre-commit hook** runs `dotnet format --verify-no-changes` automatically
- **TDD**: tests red BEFORE implementation on every handler, decorator, and adapter (Art. VIII)
- **Zero suppressions** (Art. VIII / project rules)

## Out of scope (deferred to v1.5)

- Admin dashboard UI (consumed via curl/scripts in v1)
- Per-user flags / targeting
- A/B testing framework
- Time-based rollout (`enable_at` / `disable_at` columns)
- Multi-tenant flags (single-tenant)
- Flag analytics / telemetry
- Migration of 012's `Wompi:Environment` (3-state, stays as `IOptions<WompiOptions>`)
- Audit log retention policy (indefinite for v1; cron deferred to v1.5)

## Risks

1. **Cache staleness on multi-instance** — 60s TTL means admin changes take up to 60s to propagate across instances. Acceptable for v1; `CachingFeatureFlagDecorator.Invalidate(name)` is called synchronously after commit so the local instance sees the change in milliseconds. Mitigation documented in design §"Flow (admin update path)".
2. **Migration race on first deploy** — if `AddFeatureFlags` migration runs before the code that handles "DB-missing → appsettings fallback", 011/012/013 could 500. Mitigation: fallback path implemented in `CachingFeatureFlagDecorator` BEFORE migration is required (`EfFeatureFlagStore.GetAsync` returns `null` on missing table → decorator falls back to `FeatureFlagsOptions`). `FeatureFlagMigrationService` is `IHostedService` that runs after app starts; failure is logged, not fatal.
3. **Adapter drift in backward-compat** — adapters could misread flag state and silently break 011/012/013. Mitigation: 011/012/013 test suites re-run unchanged as regression gate in PR3 acceptance. Adapter-specific tests verify `IFeatureFlag.IsEnabledAsync("factus-enabled") → IInvoiceProvider?.Provider` mapping.

## Next

`sdd-apply` → implement the 3 PRs in order, each green, each mergeable on `main`. After PR3 merged + tag `015-feature-flags-v1.0`, run `sdd-verify` (re-run full test suite + constitution-check + 011/012/013 backward-compat proof) and `sdd-archive` (write verify-report.md + archive-report.md + update 000-INDEX.md to ✅ SHIPPED + ARCHIVED).