# Tasks: 018-cv-iteration-loop

## Status

[Tasks] — Ready to apply (3 chained PRs)

## Review workload forecast

- **Total estimated diff**: ~770 lines (3 PRs) — matches design forecast
- **400-line budget risk**: MEDIUM (PR2 at ~300 lines, PR1 at ~250, PR3 at ~200)
- **Chained PRs recommended**: Yes
- **Strategy**: 3 PRs matching 016-subscription-recurring / 013-credit-consumption pattern
- **Each PR keeps build + test green** (gate per PR)
- **Chain strategy**: stacked-to-main (PR N merges to main before PR N+1 starts)

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

## PR boundaries (locked)

| PR | Scope | Estimated diff | Files (new) | Files (modified) | Test additions |
|----|-------|----------------|-------------|------------------|----------------|
| **PR1** | Domain + Application | ~250 lines | `IterationRequest.cs` (+`RequestStatus` enum), `IterationStep.cs`, `IterationResult.cs` (+ `IterationResultEntity`), `ProbabilityWarning.cs`, `IIterationService.cs`, `IIterationStore.cs`, `IterateAdaptationHandler.cs`, `GetIterationResultHandler.cs`, `IterationService.cs`, `ICreditConsumptionService.cs` (additive) | `DependencyInjection.cs` (handler singletons + InMemoryIterationStore fallback) | +18 unit tests (8 Domain + 10 Application) |
| **PR2** | Infrastructure + DB | ~300 lines | `IterationRequestConfiguration.cs`, `IterationResultConfiguration.cs`, `EfIterationStore.cs`, `InMemoryIterationStore.cs`, `IterationCleanupWorker.cs`, `20260625HHMMSS_AddIterationResults.cs` migration (+ `.Designer.cs`), `EfCreditConsumptionService.ConsumeForIterationAsync` (extension on existing file) | `BuildCvDbContext.cs` (add DbSet + ApplyConfigurationsFromAssembly), `DependencyInjection.cs` (EF store + hosted worker) | +15 integration tests |
| **PR3** | API + Web | ~200 lines | `IterationEndpoints.cs`, `IterationContracts.cs`, `IterationRateLimiting` policy, BFF `app/api/adapt/iterate/route.ts` + `[requestId]/route.ts`, 4 components (`iteration-control-panel`, `iteration-result-card`, `iteration-step-list`, `probability-warning`), `app/analizar/iterate/page.tsx`, `messages/{es,en}.json` (i18n copy), `docs/integrations/cv-generator.md` | `BuildCv.Api/Security/RateLimiting.cs` (add `IteratePolicy`), `BuildCv.Api/Program.cs` (add `MapIterationEndpoints()`), `BuildCv-web/lib/copy/es.ts` (alt copy source) | +10 e2e tests (5 API integration + 5 Playwright) |

> **Note on `IterationResultEntity`**: lives in `BuildCv.Domain/Iterations/` per design (internal record, EF projection). Constitution Art. VI preserved: no EF attributes on domain types; the entity class has plain `init` properties that EF maps via `IterationResultConfiguration`.

> **Note on `ICreditConsumptionService` extension**: additive only. Existing `ConsumeForAdaptAsync` is untouched. PR1 adds the new method to the interface; PR2 adds the EF implementation in `EfCreditConsumptionService.cs`.

## PR1: Domain + Application (~250 lines, +18 unit tests)

### T1.1 — Domain entities (TDD)
- **Files**:
  - `BuildCv-api/src/BuildCv.Domain/Iterations/IterationRequest.cs` (record + `RequestStatus` enum + factory `Create()`)
  - `BuildCv-api/src/BuildCv.Domain/Iterations/IterationStep.cs` (record)
  - `BuildCv-api/src/BuildCv.Domain/Iterations/IterationResult.cs` (record + `FromRunningRequest` factory)
  - `BuildCv-api/src/BuildCv.Domain/Iterations/ProbabilityWarning.cs` (record + `From(bestScore, threshold)` factory)
  - `BuildCv-api/src/BuildCv.Domain/Iterations/IterationResultEntity.cs` (internal record, EF projection)
- **Tests** (8+, TDD):
  - `IterationRequest_Create_ValidArgs_ReturnsRunning_Status`
  - `IterationRequest_Create_IterationCount0_Throws`
  - `IterationRequest_Create_IterationCount21_Throws`
  - `IterationRequest_Create_ThresholdMinus1_Throws`
  - `IterationRequest_Create_Threshold101_Throws`
  - `IterationRequest_Create_EmptyCv_Throws`
  - `ProbabilityWarning_From_BelowThreshold_PopulatesThreeGenericActions`
  - `IterationResult_FromRunningRequest_DefaultsAllStepsEmptyAndCreditsConsumedZero`
- **Domain purity check**: `dotnet list src/BuildCv.Domain package references` → must be 0 (existing constraint, Art. VI).

### T1.2 — Application ports (TDD, interfaces only)
- **Files**:
  - `BuildCv-api/src/BuildCv.Application/Features/Iterations/IIterationService.cs` (`RunAsync` + `GetAsync`)
  - `BuildCv-api/src/BuildCv.Application/Features/Iterations/IIterationStore.cs` (`GetByRequestIdAsync` + `SaveAsync` + `UpdateRequestStatusAsync` + `DeleteExpiredAsync`)
- **Tests** (2+, contract smoke):
  - `IIterationService_Contract_Smoke`
  - `IIterationStore_Contract_Smoke`

### T1.3 — Handlers (TDD, 2 handlers + 1 service)
- **Files**:
  - `BuildCv-api/src/BuildCv.Application/Features/Iterations/IterateAdaptationHandler.cs` (loop: 30s per-iter + 5min total, best-selection rule, idempotency hit short-circuit, credit-debit-before-loop, `OperationCanceledException` per-iter catch, `InsufficientCreditsException`)
  - `BuildCv-api/src/BuildCv.Application/Features/Iterations/GetIterationResultHandler.cs` (thin pass-through to `IIterationStore`)
  - `BuildCv-api/src/BuildCv.Application/Features/Iterations/IterationService.cs` (composes both, implements `IIterationService`)
  - `BuildCv-api/src/BuildCv.Application/Features/Credits/ICreditConsumptionService.cs` (additive: `ConsumeForIterationAsync(userId, iterationRequestId, creditCount, ct)`)
- **Tests** (10+, TDD; use `FakeAdaptHandler` + `FakeScoreHandler` + `FakeCrossEntityValidator` + `InMemoryIterationStore` + `InMemoryCreditConsumptionService`):
  - `IterateAdaptationHandler_RunsN_Iterations_CallsAdaptAndScore`
  - `IterateAdaptationHandler_SelectsBest_StepWithHighestScore`
  - `IterateAdaptationHandler_SkipsSteps_FailingArtI_ExcludedFromSelection`
  - `IterateAdaptationHandler_AllStepsCritical_ReturnsStatusFailed_BestStepNull`
  - `IterateAdaptationHandler_BelowThreshold_GeneratesProbabilityWarning`
  - `IterateAdaptationHandler_AtOrAboveThreshold_NoWarning`
  - `IterateAdaptationHandler_DebitsN_CreditsAtomically_BeforeLoop`
  - `IterateAdaptationHandler_InsufficientCredits_ThrowsInsufficientCreditsException`
  - `IterateAdaptationHandler_PerIterationTimeout_RecordsFailedStep`
  - `IterateAdaptationHandler_TotalTimeout_ReturnsTimedOut_WithPartialTrue`
  - `IterateAdaptationHandler_ArtIViolationsCount_IsExposed_InResult`
  - `GetIterationResultHandler_ReturnsCached_WhenExists`
  - `GetIterationResultHandler_ReturnsNull_WhenNotFound`

### T1.4 — DI Registration (handlers + InMemory fallback)
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (MODIFY)
- In `AddInfrastructure()`:
  - Register `IIterationStore` → `InMemoryIterationStore` (placeholder; PR2 swaps to `EfIterationStore` via conditional)
  - Register `IterateAdaptationHandler`, `GetIterationResultHandler`, `IIterationService` as singletons
  - Add DI helper to resolve all singletons via factory (matches 016 pattern)

### PR1 acceptance
- [ ] All 18+ tests pass (758/758 = 740 + 18 — base from post-016 backend = 740, conservative)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] `dotnet build -c Release` 0 warnings (warnings-as-errors)
- [ ] Domain has 0 package references (existing constraint)
- [ ] All Domain types are pure records (no IO, no EF attributes, no LLM refs)
- [ ] Work-unit commits:
  - `feat(018): domain — IterationRequest + IterationStep + IterationResult + ProbabilityWarning + IterationResultEntity`
  - `feat(018): application — IIterationService + IIterationStore`
  - `feat(018): application — IterateAdaptationHandler + GetIterationResultHandler + IterationService + ICreditConsumptionService extension`
  - `feat(018): infrastructure — DI registration of ports and handlers (InMemory fallback)`
  - `test(018): domain + application unit tests (18)`
- [ ] PR merges to `main`

## PR2: Infrastructure + DB (~300 lines, +15 integration tests)

### T2.1 — EF Core configuration + DbContext
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/IterationRequestConfiguration.cs` (NEW — maps `IterationRequest` to `iteration_requests` table; not in `Configurations/` subdirectory per design namespace)
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/IterationResultConfiguration.cs` (NEW — maps `IterationResultEntity` to `iteration_results` table; `best_step` + `all_steps` + `probability_warning` as `jsonb`)
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs` (MODIFY — add `DbSet<IterationResultEntity> IterationResults` + ensure `ApplyConfigurationsFromAssembly` picks up the new configurations)
- **Tests** (3+):
  - `IterationRequestConfiguration_MapsToTable_iteration_requests_WithColumnsAndIndexes`
  - `IterationResultConfiguration_StoresAllStepsAsJsonb`
  - `BuildCvDbContext_HasDbSet_IterationResults`

### T2.2 — Migration
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625HHMMSS_AddIterationResults.cs` (NEW, hand-written per design)
  - Creates `iteration_requests` (uuid PK, FK `users` ON DELETE CASCADE, CHECK constraints on `iteration_count` + `probability_threshold`, 2 indexes `(user_id, created_at DESC)` + `(status, created_at)`)
  - Creates `iteration_results` (uuid PK, FK `iteration_requests` ON DELETE CASCADE, `best_step` jsonb NULL, `all_steps` jsonb NOT NULL, `probability_warning` jsonb NULL, `expires_at` timestamptz, index `ix_iteration_results_expires_at`)
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625HHMMSS_AddIterationResults.Designer.cs` (auto-generated by `dotnet ef migrations add`)
- **Tests** (3+):
  - `Migration_AddIterationResults_AppliesCleanly`
  - `Migration_AddIterationResults_CreatesTable_iteration_requests_WithCheckConstraints`
  - `Migration_AddIterationResults_CreatesTable_iteration_results_WithJsonbColumns`

### T2.3 — EF adapter + JSON (de)serialization
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/Iterations/EfIterationStore.cs` (NEW)
  - `GetByRequestIdAsync` — AsNoTracking, deserialize `best_step` + `all_steps` + `probability_warning` from jsonb
  - `SaveAsync` — computes `ExpiresAt = UtcNow + 24h`, upserts by PK (`request_id`)
  - `UpdateRequestStatusAsync` — partial update of `status` column
  - `DeleteExpiredAsync` — `ExecuteDeleteAsync` for `expires_at < olderThan`
- **Tests** (5+):
  - `EfIterationStore_SaveAsync_PersistsRow_WithExpiresAtSetToNowPlus24h`
  - `EfIterationStore_GetByRequestIdAsync_RoundTripsAllSteps`
  - `EfIterationStore_GetByRequestIdAsync_ReturnsNull_WhenMissing`
  - `EfIterationStore_JsonbSerialization_BestStep_NullWhenAbsent`
  - `EfIterationStore_DeleteExpiredAsync_RemovesOldRows_KeepsFreshRows`
  - `EfIterationStore_UpdateRequestStatusAsync_Persists_NewStatus`

### T2.4 — InMemory adapter + Cleanup worker
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Iterations/InMemoryIterationStore.cs` (NEW — `ConcurrentDictionary<Guid, IterationResult>`, thread-safe reads/writes, TTL semantics for tests)
  - `BuildCv-api/src/BuildCv.Infrastructure/Iterations/IterationCleanupWorker.cs` (NEW — `BackgroundService`, `PeriodicTimer` 1h interval, calls `IIterationStore.DeleteExpiredAsync(UtcNow)` on each tick; logs `(deleted={N})` on success; logs error and continues on failure per Art. VI resilience)
- **Tests** (3+):
  - `InMemoryIterationStore_GetByRequestIdAsync_ReturnsLatest_LastWriteWins`
  - `InMemoryIterationStore_DeleteExpiredAsync_RemovesRowsOlderThan24h`
  - `IterationCleanupWorker_Tick_DeletesExpiredRows`

### T2.5 — EfCreditConsumptionService extension
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/Credits/EfCreditConsumptionService.cs` (MODIFY — add `ConsumeForIterationAsync`)
  - Validates `creditCount > 0`
  - Opens EF transaction
  - Locks user row, checks `CreditBalance >= creditCount` (return `CreditConsumeResult.Insufficient(balance)` if not)
  - Decrements `CreditBalance`, adds `CreditLedgerEntry { Reason = Consumption, Reference = "iterate:{iterationRequestId}", Delta = -creditCount, BalanceAfter = newBalance, Metadata = { iterationRequestId, creditCount } }`
  - Commits transaction (art. VI: atomic, no partial debits)
- **Tests** (3+):
  - `ConsumeForIterationAsync_DebitsN_Atomically_InOneTransaction`
  - `ConsumeForIterationAsync_InsufficientBalance_NoDebit_ReturnsInsufficient`
  - `ConsumeForIterationAsync_CreditLedgerEntry_HasReferenceIterate_Prefix`

### T2.6 — DI swap to EF adapter + hosted service registration
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (MODIFY)
  - In Postgres branch: swap `IIterationStore` from `InMemoryIterationStore` to `EfIterationStore` (scoped)
  - Register `IterationCleanupWorker` via `AddHostedService<>()`
- **Tests** (1+):
  - `DI_Registers_EfIterationStore_AsScoped_InPostgresBranch`
  - `DI_Registers_IterationCleanupWorker_AsHostedService`

### PR2 acceptance
- [ ] All 15+ integration tests pass
- [ ] EF migration applies cleanly (`dotnet ef database update`)
- [ ] 011/012/013/014/015/016/017 test suites still pass (no regressions)
- [ ] DI registered, app starts
- [ ] `dotnet test` green
- [ ] `dotnet format --verify-no-changes` clean
- [ ] Work-unit commits:
  - `feat(018): infrastructure — IterationRequestConfiguration + IterationResultConfiguration + DbContext`
  - `feat(018): infrastructure — migration AddIterationResults (20260625HHMMSS)`
  - `feat(018): infrastructure — EfIterationStore (jsonb round-trip)`
  - `feat(018): infrastructure — InMemoryIterationStore + IterationCleanupWorker`
  - `feat(018): infrastructure — EfCreditConsumptionService.ConsumeForIterationAsync`
  - `feat(018): infrastructure — DI swap to EfIterationStore + register cleanup worker`
  - `test(018): integration tests (15)`
- [ ] PR merges to `main`

## PR3: API + Web (~200 lines, +10 e2e tests)

### T3.1 — Admin endpoints + DTOs
- **Files**:
  - `BuildCv-api/src/BuildCv.Api/Endpoints/IterationEndpoints.cs` (NEW)
    - `MapGroup("/api/v1/adapt/iterate").RequireAuthorization().WithTags("Iterations")`
    - `POST /` → `IterateHandler` (200 OK synchronous / 402 INSUFFICIENT_CREDITS / 422 INVALID_INPUT / 401 / 429)
    - `GET /{requestId:guid}` → `GetIterationHandler` (200 / 404 NOT_FOUND or EXPIRED / 401)
    - Per-request credit gate lives inside handler (because `iterationCount` is in body, not endpoint registration); `.RequireCredits(0)` placeholder documents intent
  - `BuildCv-api/src/BuildCv.Api/Contracts/IterationContracts.cs` (NEW — `IterateRequestDto` with `[MaxLength]` + `IterationResultDto` + `IterationStepDto` + `ProbabilityWarningDto` + `FromDomain` mappers)
- **Tests** (5+ e2e API):
  - `IterationEndpoints_Post_Returns200_WithValidAuth_5Iterations_DefaultThreshold`
  - `IterationEndpoints_Post_Returns402_WithInsufficientCredits`
  - `IterationEndpoints_Post_Returns422_WithInvalidInput_IterationCountOutOfRange`
  - `IterationEndpoints_Post_Returns422_WithInvalidInput_ThresholdOutOfRange`
  - `IterationEndpoints_Post_Returns401_Unauthenticated`
  - `IterationEndpoints_Get_Returns200_WhenCached`
  - `IterationEndpoints_Get_Returns404_WhenNotFound`
  - `IterationEndpoints_Get_Returns404_WhenExpired`
  - `IterationEndpoints_Post_AllIterationsCritical_ReturnsFailed_NoWarning`
  - `IterationEndpoints_Post_BelowThreshold_ReturnsProbabilityWarning`

### T3.2 — Rate-limit policy + Program.cs wiring
- **Files**:
  - `BuildCv-api/src/BuildCv.Api/Security/RateLimiting.cs` (MODIFY — add `public const string IteratePolicy = "iterate"`)
  - Add policy in `AddAppRateLimiting`: `IteratePolicy` = fixed-window 10/h per IP (partition key = `ClientKey(httpContext)`), `QueueLimit = 0`
  - `BuildCv-api/src/BuildCv.Api/Program.cs` (MODIFY — call `app.MapIterationEndpoints()` after `MapAdaptEndpoints()`)
- **Tests** (1+ e2e API):
  - `IterationEndpoints_Post_Returns429_After11thRequestIn1Hour`

### T3.3 — Web: BFF routes + components + page + i18n
- **Files**:
  - `BuildCv-web/app/api/adapt/iterate/route.ts` (NEW — POST; proxies `BACKEND_URL/POST /api/v1/adapt/iterate`; forwards JWT cookie per 013.2 pattern; returns DTO)
  - `BuildCv-web/app/api/adapt/iterate/[requestId]/route.ts` (NEW — GET; same proxy pattern)
  - `BuildCv-web/components/iterations/iteration-control-panel.tsx` (NEW — sliders for count (1-20, default 5) + threshold (0-100, default 50), live "Créditos necesarios: N" cost indicator, confirmation modal before start)
  - `BuildCv-web/components/iterations/iteration-result-card.tsx` (NEW — best step card + score badge + "Exportar PDF" (calls 004) + "Ver otros intentos" collapsible)
  - `BuildCv-web/components/iterations/iteration-step-list.tsx` (NEW — table of all steps with iteration #, score, severity, passed-Art-I flag, timestamp)
  - `BuildCv-web/components/iterations/probability-warning.tsx` (NEW — amber/red banner, role="alert", ARIA live region, conditional color: amber 25-49% / red <25% / hidden ≥50%, "Ver sugerencias" expand for 3 actions, "Mejorar CV" → `/editor`)
  - `BuildCv-web/app/analizar/iterate/page.tsx` (NEW — page composing above; paste/upload CV + paste vacancy + start button + results panel)
  - `BuildCv-web/messages/es.json` (MODIFY — add i18n keys: `iteration.title`, `iteration.subtitle`, `iteration.cta.start`, `iteration.progress.iterationOf`, `iteration.warning.compatibility`, `iteration.warning.cta.improve`, `iteration.warning.cta.suggestions`, `iteration.allFailedBanner`, `iteration.exportPdf`)
  - `BuildCv-web/messages/en.json` (MODIFY — same keys in English)
  - `BuildCv-web/lib/copy/es.ts` (MODIFY — add iteration copy as alt source for non-i18n consumers)
- **Tests** (3+ web unit):
  - `IterationControlPanel_ShowsCreditsNeeded_UpdatesWithSlider`
  - `IterationResultCard_DisplaysBestStep_And_SeverityBadge`
  - `ProbabilityWarning_RendersAmber_WhenBestPctBetween25And49`
  - `ProbabilityWarning_RendersRed_WhenBestPctBelow25`
  - `ProbabilityWarning_Hidden_WhenBestPctAtOrAbove50`

### T3.4 — E2E tests (Playwright)
- **File**: `BuildCv-web/e2e/iterations.spec.ts` (NEW)
- **Tests** (5+ Playwright):
  - `IterationFlow_LoadsWithDefaults_Count5_Threshold50_Shows5CreditsNeeded`
  - `IterationFlow_SliderUpdatesCostEstimate_To10Credits`
  - `IterationFlow_HappyPath_Start_ReturnsResultCard_WithBestStep`
  - `IterationFlow_ProbabilityWarning_RendersWhenBestBelowThreshold_Amber`
  - `IterationFlow_AllFailedBanner_RendersWhenStatusFailed`
  - `IterationFlow_ExportPdfButton_CallsExportEndpoint_WithBestStepText`
  - `IterationFlow_InsufficientCredits_StartButton_OpensBuyMoreModal`

### T3.5 — CV_generator integration doc
- **File**: `BuildCv-web/docs/integrations/cv-generator.md` (NEW — documentation only, no code)
- Content: v1 workflow (Option A: upload PDF/DOCX via 005 → parsed text → submit; Option B: paste Markdown text directly). v2 roadmap: webhook from `CV_generator` → BuildCv to auto-start iteration.
- **Tests**: 0 (docs only).

### PR3 acceptance
- [ ] All 10 e2e tests pass (5 API + 5 Playwright; web unit tests bring component coverage ≥90%)
- [ ] All 6 gates pass: lint, typecheck, test, e2e, build, constitution-check
- [ ] E2E tests pass (90/90 = 85 + 5)
- [ ] Backward compat: 011-017 test suites still pass unchanged
- [ ] i18n keys present in both `es.json` and `en.json`
- [ ] CV_generator doc published at `BuildCv-web/docs/integrations/cv-generator.md`
- [ ] Work-unit commits:
  - `feat(018): api — IterationEndpoints + IterationContracts + DTOs + 10 e2e tests`
  - `feat(018): api — iterate rate-limit policy + Program.cs wiring`
  - `feat(018): web — BFF routes (POST + GET) + 4 components + settings panel`
  - `feat(018): web — /analizar/iterate page + i18n copy (es + en) + 5 Playwright e2e`
  - `docs(018): web — CV_generator integration documentation (v1 upload, v2 roadmap)`
- [ ] PR merges to `main`

## Test count forecast

| Phase | Before 018 | After 018 | Delta |
|-------|------------|-----------|-------|
| API unit (App) | 218 (post-016) | 218 + 10 = 228 | +10 |
| API unit (Domain) | 136 (post-016) | 136 + 8 = 144 | +8 |
| API integration | 119 (post-016) | 119 + 15 = 134 | +15 |
| API e2e (endpoint tests in API project) | 98 (post-016) | 98 + 5 = 103 | +5 |
| **API total** | **834** (post-016) | **872** | **+38** |
| Web (no major changes, unit + e2e only) | 760 (post-016) | 760 + 5 = 765 | +5 |
| E2E Playwright | 85 (post-016) | 85 + 5 = 90 | +5 |
| **TOTAL** | **1679** (post-016) | **1727** | **+48** |

(forecast +43 from design; conservative delta +48 as test catalog may grow slightly with edge-case coverage)

> **Baseline note**: 016-subscription-recurring shipped with 834 API tests / 760 web tests / 85 Playwright = 1679 total. The 018 forecast assumes this baseline.

## Dependency graph (per PR)

```
PR1 (Domain + Application)
  ├── T1.1: Domain entities (no deps)
  ├── T1.2: Application ports (depend on T1.1)
  ├── T1.3: Handlers + service + ICreditConsumptionService extension (depend on T1.1 + T1.2)
  └── T1.4: DI registration with InMemoryIterationStore fallback (depends on T1.2 + T1.3)
PR1 → PR2 (blocked until PR1 merges to main)

PR2 (Infrastructure + DB)
  ├── T2.1: EF config + DbContext (depends on PR1's IterationResultEntity)
  ├── T2.2: Migration (depends on T2.1)
  ├── T2.3: EfIterationStore (depends on T2.1 + T2.2)
  ├── T2.4: InMemoryIterationStore + Cleanup worker (depends on T2.3 for TTL semantics)
  ├── T2.5: EfCreditConsumptionService.ConsumeForIterationAsync (depends on PR1's interface extension)
  └── T2.6: DI swap to EfIterationStore + register hosted worker (depends on T2.3 + T2.4 + T2.5)
PR2 → PR3 (blocked until PR2 merges to main)

PR3 (API + Web)
  ├── T3.1: Admin endpoints + DTOs (depends on PR2's iteration store + handlers)
  ├── T3.2: Rate-limit policy + Program.cs wiring (depends on T3.1)
  ├── T3.3: Web BFF routes + components + page + i18n (depends on T3.2)
  ├── T3.4: E2E tests Playwright (depends on T3.3)
  └── T3.5: CV_generator docs (no deps; can run anytime)
```

## Critical execution order

1. **PR1 first** (T1.1 → T1.2 → T1.3 → T1.4)
2. **PR2 second** (T2.1 → T2.2 → T2.3 → T2.4 → T2.5 → T2.6)
3. **PR3 last** (T3.1 → T3.2 → T3.3 → T3.4; T3.5 parallel)

Each PR's `dotnet test` + `pnpm test` + `dotnet format --verify-no-changes` MUST be green before merge.

## Conventions per PR

- **Conventional commits**, Spanish messages, no AI attribution (`feat(018): …`, `test(018): …`, `docs(018): …`)
- **Work-unit commits** (1 commit per logical group, not per file)
- **Branch**: only `main` (no feature branches)
- **Direct merge** to main (PR-N+1 starts from main after PR-N merges)
- **Pre-commit hook** runs `dotnet format --verify-no-changes` automatically
- **No suppressions** (no `#pragma warning disable`, no `[Skip]`, no `[Fact(DisplayName="Skip…")]`) — fix errors instead

## Out of scope (deferred to v1.5 / v2)

- **LLM temperature sampling control** — uses existing default; not exposed to user (v1.5)
- **A/B testing of different prompts** — v1 uses same prompt per iteration (v1.5)
- **User feedback loop ("did this help?")** — requires persistence + accounts (v1.5)
- **Multi-vacancy ranking** — 018 = one CV ↔ one vacancy (v1.5)
- **Per-iteration streaming via SSE** — v1 returns full result after N iterations (v1.5)
- **Parallel iteration execution** — v1 strictly sequential for determinism + cost control (v1.5)
- **Custom `RecommendedActions` per request** — hardcoded 3 in code for Art. IV consistency (v1.5)
- **Direct `~/Documentos/CV_generator` API integration** — v1 = upload via 005 or paste text, v2 = webhook (out of scope per proposal)

## CV_generator integration note

The `~/Documentos/CV_generator:main` repo generates CVs in Markdown/PDF format.

- **v1 (this change)**: user uploads the generated CV via existing `POST /api/v1/import` (005-cv-pdf-docx-import) OR pastes the Markdown text directly into the iteration request body (`cvText` field). No code integration required from CV_generator. Documented in `BuildCv-web/docs/integrations/cv-generator.md`.
- **v2 (out of scope, deferred)**: direct API integration via webhook from `CV_generator` → `BuildCv` to start iteration automatically when a new CV is generated. Tracked in `specs/_archive/018-cv-iteration-loop/` once v1 ships.

The v1 workflow is intentionally friction-free: the user keeps their existing CV_generator workflow and adds one extra click in BuildCv (paste text or upload file → click "Iniciar iteración"). v2 will close the loop with zero-click automation.

## Risks

1. **LLM non-determinism across iterations.** Even with `seed`, Anthropic may produce similar but not identical text. Same `requestId` re-run may produce slightly different result.
   - **Mitigation**: cache key includes `requestId`, so the FIRST result is canonical for 24h. UI copy acknowledges: "Re-ejecutar puede producir texto ligeramente distinto" (Art. IV honest framing). Document in verify report.

2. **Credit cost surprise.** User starts 20 iterations without realizing that's 20 credits.
   - **Mitigation**: UI shows "Créditos necesarios: N" prominently; Start button requires confirmation modal ("Esto consumirá N créditos. ¿Continuar?"). Backend `ConsumeForIterationAsync` debits BEFORE loop starts (fail-fast 402 if insufficient, no partial debits).

3. **Art. I false positives.** If validator is too strict, all iterations produce `Severity.Critical` → no best-step candidate.
   - **Mitigation**: design returns `Status=Failed` + `BestStep=null` + `BestStep=null` warning in this edge case. Existing 003 `CrossEntityValidator` has acceptable false-positive rate in production. UI shows "Mejores resultados requieren mayor compatibilidad" when `Status=Failed`.

4. **Timeout handling.** Long iterations may exceed 30s per-iter timeout.
   - **Mitigation**: per-iteration 30s + total 5min hard cap. On total timeout, return `Status=TimedOut` + `Partial=true` + best-so-far step. UI shows "Resultado parcial: N de M iteraciones completadas antes del timeout".

5. **Cleanup worker must run hourly.** TTL 24h means worker tick rate is critical for Art. III (no long-term persistence).
   - **Mitigation**: `IHostedService` with `PeriodicTimer(TimeSpan.FromHours(1))`; logs `(deleted={N})` per tick; logs error and continues on failure per Art. VI resilience. Worker unit-tested with `InMemoryIterationStore`.

6. **Jsonb schema evolution.** If `IterationStep` or `ProbabilityWarning` schema changes, existing rows in `iteration_results` may not deserialize.
   - **Mitigation**: `JsonSerializerOptions` is sealed per engine version (column `engine_version = "018-iteration-loop-1.0.0"`); on engine bump, a migration script can wipe rows older than engine version OR add new columns. TTL 24h naturally bounds the window.

## Next

`sdd-apply` → implement the 3 PRs in order, each green, each mergeable on main, with work-unit commits per the conventions above. Each PR must keep all 6 gates green (lint, typecheck, test, e2e, build, constitution-check).
