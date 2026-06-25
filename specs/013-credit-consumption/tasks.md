# Tasks: 013-credit-consumption

## Status

[Tasks] — ✅ SHIPPED (PR1 + PR2 + PR3 merged on `main`)
## Review workload forecast

- **Total estimated diff**: ~800 lines (3 PRs)
- **400-line budget risk**: HIGH for single PR, LOW for chained PRs
- **Chained PRs recommended**: Yes
- **Strategy**: 3 PRs matching the 012-wompi pattern that landed in 3h21m
- **Each PR keeps build + test green** (gate per PR)

## PR boundaries (locked)

| PR | Scope | Estimated diff | Files (new) | Files (modified) | Test additions |
|----|-------|----------------|-------------|------------------|----------------|
| **PR1** | Domain + Application | ~250 lines | `CreditLedgerEntry.cs`, `CreditLedgerReason.cs`, `ICreditLedger.cs`, `ICreditConsumptionService.cs`, `ICreditsFeatureFlag.cs`, 7 handlers | `User.cs` (add CreditBalance) | +35 unit (App) |
| **PR2** | Infrastructure + DB | ~300 lines | `EfCreditLedger.cs`, `EfCreditConsumptionService.cs`, `CreditLedgerEntryConfiguration.cs`, `CreditsFeatureFlag.cs`, `CreditsOptions.cs`, `20260624_AddCreditLedger.cs` migration | `BuildCvDbContext.cs`, `UserConfiguration.cs`, `DependencyInjection.cs`, `appsettings.json` | +20 integration |
| **PR3** | API + Web | ~250 lines | `CreditEndpoints.cs`, `RequireCreditsFilter.cs`, `EndpointConventionBuilderExtensions.cs`, `balance/route.ts`, `history/route.ts`, `credit-badge.tsx`, `low-credit-banner.tsx`, `credits.ts` (lib) | `HandleWebhookHandler.cs`, `AdaptEndpoints.cs`, `Program.cs` (DI), `wompi-widget.tsx` (call fetchBalance on APPROVED) | +10 e2e API + +25 e2e Web |

## PR1: Domain + Application (~250 lines)

### T1.1 — Domain: CreditLedgerEntry + CreditLedgerReason
- **Files**: `BuildCv-api/src/BuildCv.Domain/Credits/CreditLedgerEntry.cs`, `BuildCv-api/src/BuildCv.Domain/Credits/CreditLedgerReason.cs`
- **Tests** (TDD, 5+):
  - `CreditLedgerEntry_RequiresNonZeroDelta`: throws if Delta == 0
  - `CreditLedgerEntry_RequiresNonNegativeBalanceAfter`: throws if BalanceAfter < 0
  - `CreditLedgerEntry_RequiresNonEmptyReference`: throws if Reference null/empty
  - `CreditLedgerEntry_DefaultsCreatedAtToUtcNow`
  - `CreditLedgerReason_HasAllFiveValues`: Welcome=1, Purchase=2, Consumption=3, Refund=4, ManualAdjustment=5

### T1.2 — Domain: User.CreditBalance
- **Files**: `BuildCv-api/src/BuildCv.Domain/Auth/User.cs` (add `int CreditBalance { get; init; } = 0;`)
- **Tests** (TDD, 3+):
  - `User_CreditBalance_DefaultsToZero`
  - `User_CreditBalance_CanBeSetInWith`: `user with { CreditBalance = 5 }`
  - `User_CreditBalance_Immutability`: cannot mutate after construction

### T1.3 — Application: ICreditLedger port
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/Credits/ICreditLedger.cs`
- **Tests** (TDD, 5+):
  - `ICreditLedger_AccreditAsync_RequiresNonZeroDelta`
  - `ICreditLedger_AccreditAsync_RequiresNonEmptyReference`
  - `ICreditLedger_AccreditAsync_RequiresPositiveDeltaForGrants`
  - `ICreditLedger_AccreditAsync_RequiresNegativeDeltaForConsumption`
  - `ICreditLedger_FindByReferenceAsync_ReturnsNullIfNotFound`

### T1.4 — Application: ICreditConsumptionService port
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/Credits/ICreditConsumptionService.cs`
- **Tests** (TDD, 3+):
  - `CreditConsumeResult_RequiresExplicitSuccessOrFailure`
  - `CreditBalanceView_RequiresNonNegativeBalance`
  - `CreditHistoryPage_RequiresValidCursor`

### T1.5 — Application: ICreditsFeatureFlag port
- **Files**: `BuildCv-api/src/BuildCv.Application/Common/ICreditsFeatureFlag.cs`
- **Tests** (TDD, 2+):
  - `ICreditsFeatureFlag_Contract` (mock test)

### T1.6 — Application: 7 handlers (TDD per handler, 5+ each, 35 total)

#### T1.6.1 — AccreditPurchaseHandler
- Reference: `payment:{paymentId}`
- Reason: `Purchase`
- Idempotency: replay returns existing
- Delta: positive (matches payment.Credits)
- Updates `users.credit_balance`

#### T1.6.2 — AccreditWelcomeHandler
- Reference: `welcome:{userId}`
- Reason: `Welcome`
- Delta: +3
- Idempotency: replay returns existing (signup can be called multiple times)

#### T1.6.3 — ConsumeForAdaptHandler
- Reference: `adapt:{adaptRequestId}`
- Reason: `Consumption`
- Delta: -1
- Returns `CreditConsumeResult { Success=false, ErrorCode="CREDIT/INSUFFICIENT" }` if balance < 1
- Idempotency: replay returns existing (same adaptRequestId)

#### T1.6.4 — RefundConsumptionHandler
- Reference: `adapt:{adaptRequestId}:refund`
- Reason: `Refund`
- Delta: +1
- Idempotency: replay returns existing
- Throws if no prior Consumption entry exists

#### T1.6.6 — GetCreditBalanceHandler
- Returns `CreditBalanceView { Balance, RecentConsumption }`
- `RecentConsumption`: count of `Consumption` entries in last 7 days

#### T1.6.7 — GetCreditHistoryHandler
- Pagination: `limit` (1-200, default 50), `cursor` (opaque)
- Order: `CreatedAt DESC`
- Cursor encoding: base64(`{createdAt.Ticks}:{id}`)

#### T1.6.8 — GrantManualCreditHandler
- Reference: `admin:{adminId}:{ticks}`
- Reason: `ManualAdjustment`
- Delta: arbitrary (positive or negative)
- Requires admin role (enforced in API layer)

### PR1 acceptance
- [ ] Domain compiles with 0 warnings (`dotnet format --verify-no-changes`)
- [ ] All 7 handlers pass 5+ unit tests each
- [ ] `dotnet test` is green
- [ ] Domain has 0 package references (existing constraint)
- [ ] Commit message: `feat(013-credit-consumption): PR1 — domain + application (credit ledger) [TICKET-XXX]`
- [ ] **Work-unit commits**: 1 commit per handler group (not 1 per file)
  - `feat(013): domain — CreditLedgerEntry + CreditLedgerReason + User.CreditBalance`
  - `feat(013): application — ICreditLedger + ICreditConsumptionService + ICreditsFeatureFlag`
  - `feat(013): application — AccreditPurchase + AccreditWelcome handlers`
  - `feat(013): application — ConsumeForAdapt + RefundConsumption handlers`
  - `feat(013): application — GetBalance + GetHistory + GrantManualCredit handlers`
  - `test(013): domain + application unit tests (35+)`
- [ ] PR merges to `main` (no feature branch)

## PR2: Infrastructure + DB (~300 lines)

### T2.1 — EF: CreditLedgerEntryConfiguration
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/CreditLedgerEntryConfiguration.cs`
- **Tests** (TDD, 5+):
  - `CreditLedgerEntryConfiguration_MapsToTable_CreditLedgerEntries`
  - `CreditLedgerEntryConfiguration_HasUniqueIndex_OnUserReasonReference`
  - `CreditLedgerEntryConfiguration_HasIndex_OnUserCreatedAt`
  - `CreditLedgerEntryConfiguration_HasCheckConstraint_DeltaNonZero`
  - `CreditLedgerEntryConfiguration_HasCheckConstraint_BalanceNonNeg`
  - `CreditLedgerEntryConfiguration_CascadeDeletesFromUser`

### T2.2 — EF: UserConfiguration (modify)
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/UserConfiguration.cs`
- **Tests** (TDD, 3+):
  - `UserConfiguration_MapsCreditBalance_ToColumn`
  - `UserConfiguration_HasCheckConstraint_CreditBalanceNonNeg`
  - `UserConfiguration_DefaultsCreditBalance_ToZero`

### T2.3 — EF: BuildCvDbContext (modify)
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs`
- **Tests** (TDD, 2+):
  - `BuildCvDbContext_HasDbSet_CreditLedgerEntries`
  - `BuildCvDbContext_AppliesCreditLedgerEntryConfiguration`

### T2.4 — Migration: AddCreditLedger
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260624_AddCreditLedger.cs`
- **Tests** (TDD, 3+):
  - `Migration_Applies_Successfully`: up + down
  - `Migration_CreatesTable_CreditLedgerEntries_WithConstraints`
  - `Migration_AddsColumn_CreditBalance_ToUsers`

### T2.5 — Infrastructure: EfCreditLedger
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Credits/EfCreditLedger.cs`
- **Tests** (TDD, 8+):
  - `EfCreditLedger_AccreditAsync_CreatesEntry_AndUpdatesBalance`
  - `EfCreditLedger_AccreditAsync_IdempotentOnReplay`: same reference → existing entry
  - `EfCreditLedger_AccreditAsync_ThrowsOnNegativeDeltaForPurchase`
  - `EfCreditLedger_AccreditAsync_UpdatesBalance_InSameTransaction`
  - `EfCreditLedger_AccreditAsync_RetriesOnTransientFailure`: 3x retry
  - `EfCreditLedger_FindByReferenceAsync_ReturnsEntry_IfExists`
  - `EfCreditLedger_FindByReferenceAsync_ReturnsNull_IfNotFound`
  - `EfCreditLedger_AccreditAsync_ThrowsIfBalanceWouldGoNegative`

### T2.6 — Infrastructure: EfCreditConsumptionService
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Credits/EfCreditConsumptionService.cs`
- **Tests** (TDD, 8+):
  - `EfCreditConsumptionService_ConsumeForAdaptAsync_DeductsOneCredit_WhenBalancePositive`
  - `EfCreditConsumptionService_ConsumeForAdaptAsync_Fails_WhenBalanceZero`
  - `EfCreditConsumptionService_ConsumeForAdaptAsync_IsIdempotent`
  - `EfCreditConsumptionService_RefundConsumptionAsync_RestoresCredit`
  - `EfCreditConsumptionService_RefundConsumptionAsync_ThrowsIfNoPriorConsume`
  - `EfCreditConsumptionService_GetBalanceAsync_ReturnsBalanceAndRecentConsumption`
  - `EfCreditConsumptionService_GetHistoryAsync_PaginatesCorrectly`
  - `EfCreditConsumptionService_GetHistoryAsync_DecodesCursorCorrectly`

### T2.7 — Infrastructure: CreditsFeatureFlag + CreditsOptions
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/Credits/CreditsFeatureFlag.cs`, `BuildCv-api/src/BuildCv.Infrastructure/Credits/CreditsOptions.cs`
- **Tests** (TDD, 3+):
  - `CreditsFeatureFlag_IsEnabled_ReturnsTrue_WhenConfigIsTrue`
  - `CreditsFeatureFlag_IsEnabled_ReturnsFalse_WhenConfigIsFalse`
  - `CreditsFeatureFlag_IsEnabled_ReturnsFalse_WhenConfigMissing`

### T2.8 — DI Registration
- **Files**: `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (modify)
- **Tests** (TDD, 2+):
  - `DependencyInjection_RegistersICreditLedger_AsScoped`
  - `DependencyInjection_RegistersICreditConsumptionService_AsScoped`

### T2.9 — Configuration
- **Files**: `BuildCv-api/src/BuildCv.Api/appsettings.json` (add `Credits.Enabled: true` for dev)
- **Files**: `BuildCv-api/src/BuildCv.Api/appsettings.Production.json` (add `Credits.Enabled: false` for safe rollout)

### T2.10 — Integration tests
- **Files**: `BuildCv-api/tests/BuildCv.Infrastructure.Tests/Credits/` (new)
- **Tests** (15+):
  - EF migration applies cleanly
  - `EfCreditLedger` writes to DB correctly
  - Unique violation caught → returns existing (idempotency)
  - `xmin` concurrency conflict → retry 3x
  - Cascade delete: user → ledger gone, payments kept
  - CHECK constraint violations caught
  - End-to-end: signup → welcome → consume → balance=2
  - End-to-end: signup → consume → refund → balance=3
  - Concurrent consumes with balance=1 → exactly one succeeds
  - Webhook APPROVED → ledger + balance updated
  - Webhook APPROVED replayed → idempotent (single entry)
  - Webhook APPROVED with `Credits:Enabled=false` → no ledger entry
  - Welcome grant on signup → balance=3
  - Welcome grant replayed → idempotent
  - ARCO delete → user anonymized, ledger cascade, payments kept

### PR2 acceptance
- [ ] EF migration applies (`dotnet ef database update` in dev)
- [ ] All integration tests pass
- [ ] DI registered, app starts
- [ ] `dotnet test` green
- [ ] `dotnet format --verify-no-changes` green
- [ ] **Work-unit commits**:
  - `feat(013): infrastructure — CreditLedgerEntryConfiguration + UserConfiguration + DbContext`
  - `feat(013): infrastructure — migration AddCreditLedger (20260624)`
  - `feat(013): infrastructure — EfCreditLedger`
  - `feat(013): infrastructure — EfCreditConsumptionService`
  - `feat(013): infrastructure — CreditsFeatureFlag + DI registration`
  - `test(013): integration tests (20+)`
- [ ] PR merges to `main`

## PR3: API + Web (~250 lines)

### T3.1 — API: CreditEndpoints
- **Files**: `BuildCv-api/src/BuildCv.Api/Endpoints/CreditEndpoints.cs`
- **Tests** (TDD, 4+):
  - `CreditEndpoints_GetBalance_Returns200_WithValidJWT`
  - `CreditEndpoints_GetBalance_Returns401_WithoutJWT`
  - `CreditEndpoints_GetHistory_Returns200_Paginated`
  - `CreditEndpoints_Gift_Returns200_ForAdmin`
  - `CreditEndpoints_Gift_Returns403_ForNonAdmin`

### T3.2 — API: RequireCreditsFilter
- **Files**: `BuildCv-api/src/BuildCv.Api/Filters/RequireCreditsFilter.cs`, `BuildCv-api/src/BuildCv.Api/Filters/EndpointConventionBuilderExtensions.cs`
- **Tests** (TDD, 5+):
  - `RequireCreditsFilter_AllowsRequest_WhenBalanceSufficient`
  - `RequireCreditsFilter_Returns402_WhenBalanceInsufficient`
  - `RequireCreditsFilter_SetsXCreditBalanceHeader`
  - `RequireCreditsFilter_SetsRetryAfterHeader`
  - `RequireCreditsFilter_Returns401_WhenNoJWT`

### T3.3 — API: AdaptEndpoints (modify)
- **Files**: `BuildCv-api/src/BuildCv.Api/Endpoints/AdaptEndpoints.cs`
- **Tests** (TDD, 4+):
  - `AdaptEndpoints_Returns401_WithoutJWT`
  - `AdaptEndpoints_Returns402_With0Credits`
  - `AdaptEndpoints_Returns200_With1Credit`
  - `AdaptEndpoints_DeductsOneCredit_OnSuccess`

### T3.4 — API: HandleWebhookHandler (modify)
- **Files**: `BuildCv-api/src/BuildCv.Application/Features/Payments/HandleWebhookHandler.cs`
- **Tests** (TDD, 5+):
  - `HandleWebhookHandler_CreditsUser_OnApproved_WhenFlagEnabled`
  - `HandleWebhookHandler_DoesNotCreditUser_OnApproved_WhenFlagDisabled`
  - `HandleWebhookHandler_DoesNotFailWebhook_OnCreditGrantFailure`
  - `HandleWebhookHandler_IsIdempotent_OnReplayedApprovedWebhook`
  - `HandleWebhookHandler_StillCreatesInvoice_OnApproved`

### T3.5 — Web: BFF routes
- **Files**: `BuildCv-web/app/api/credits/balance/route.ts`, `BuildCv-web/app/api/credits/history/route.ts`
- **Tests** (TDD, 3+):
  - `BFF_Balance_Route_Proxies401_WhenNoSession`
  - `BFF_Balance_Route_Proxies200_WhenSessionValid`
  - `BFF_History_Route_ForwardsQueryParams`

### T3.6 — Web: API client
- **Files**: `BuildCv-web/lib/api/credits.ts`
- **Tests** (TDD, 2+):
  - `FetchBalance_ReturnsParsedObject`
  - `FetchHistory_EncodesQueryParamsCorrectly`

### T3.7 — Web: CreditBadge component
- **Files**: `BuildCv-web/components/credits/credit-badge.tsx`
- **Tests** (TDD, 3+):
  - `CreditBadge_DisplaysBalance`
  - `CreditBadge_RefreshesEvery30Seconds`
  - `CreditBadge_ShowsRed_WhenBalanceZero`

### T3.8 — Web: LowCreditBanner component
- **Files**: `BuildCv-web/components/credits/low-credit-banner.tsx`
- **Tests** (TDD, 2+):
  - `LowCreditBanner_Shows_WhenBalanceAtOrBelowThreshold`
  - `LowCreditBanner_Hides_WhenBalanceAboveThreshold`

### T3.9 — Web: 402 modal in adapt page
- **Files**: `BuildCv-web/app/(app)/adapt/page.tsx` (modify)
- **Tests** (TDD, 2+):
  - `AdaptPage_Shows402Modal_OnInsufficientCredits`
  - `AdaptPage_ShowsRefundToast_On502`

### T3.10 — Web: wompi widget integration
- **Files**: `BuildCv-web/components/wompi/wompi-widget.tsx` (modify — on APPROVED, call fetchBalance)
- **Tests** (TDD, 1+):
  - `WompiWidget_CallsFetchBalance_OnApprovedEvent`

### T3.11 — E2E tests (Playwright)
- **Files**: `BuildCv-web/e2e/credits.spec.ts`
- **Tests** (10+):
  - Sign up → see "3 créditos" badge
  - Buy package → badge updates
  - Adapt 3x → balance=0
  - 4th adapt → 402 modal appears
  - Click "Comprar más" → redirect to Wompi
  - Complete purchase → badge updates
  - Low-credit banner appears at balance ≤ 2
  - Banner hidden at balance > 2
  - History page lists recent entries
  - Pagination works

### T3.12 — Copy (i18n)
- **Files**: `BuildCv-web/lib/copy/es.ts` (add credit strings)
- **Tests**: visual (snapshot)

### PR3 acceptance
- [ ] All e2e API tests pass
- [ ] All e2e Web tests pass
- [ ] All 6 gates green: lint, typecheck, test, e2e, build, constitution-check
- [ ] `dotnet format --verify-no-changes` green
- [ ] `pnpm lint` green
- [ ] `pnpm build` green
- [ ] `pnpm dev` + `dotnet run` integration works
- [ ] **Work-unit commits**:
  - `feat(013): api — CreditEndpoints + RequireCreditsFilter`
  - `feat(013): api — AdaptEndpoints auth + credit gate`
  - `feat(013): api — HandleWebhookHandler credits user on APPROVED`
  - `feat(013): web — BFF routes + API client`
  - `feat(013): web — CreditBadge + LowCreditBanner components`
  - `feat(013): web — 402 modal in adapt page + wompi integration`
  - `test(013): e2e API (10+) + e2e Web (10+ Playwright)`
  - `docs(013): copy es.ts + INDEX update`
- [ ] PR merges to `main`

## Test count forecast

| Phase | Before | After | Delta |
|-------|--------|-------|-------|
| API unit (App) | 128 | 163 | +35 |
| API integration (Infra) | 71 | 91 | +20 |
| API e2e | 252 | 262 | +10 |
| Web e2e | 718 | 743 | +25 |
| **TOTAL** | **1169** | **1259** | **+90** |

## Dependency graph (per PR)

```
PR1 (Domain + App)
  ├── T1.1, T1.2: Domain entities (no deps)
  ├── T1.3, T1.4, T1.5: Application ports (depend on T1.1, T1.2)
  └── T1.6.1-T1.6.8: 7 handlers (depend on T1.3, T1.4, T1.5)
PR1 → PR2 (blocked until PR1 merges)

PR2 (Infra + DB)
  ├── T2.1-T2.4: EF config + migration (depend on PR1)
  ├── T2.5-T2.8: Adapters (depend on T2.1-T2.4)
  └── T2.10: Integration tests (depend on T2.5-T2.8)
PR2 → PR3 (blocked until PR2 merges)

PR3 (API + Web)
  ├── T3.1-T3.4: API endpoints + filter (depend on PR2)
  ├── T3.5-T3.6: Web BFF + API client (depend on T3.1)
  ├── T3.7-T3.10: Web components + integration (depend on T3.5, T3.6)
  └── T3.11-T3.12: E2E + copy (depend on all above)
```

## Critical execution order

1. **PR1 first** (T1.1 → T1.2 → T1.3 → T1.4 → T1.5 → T1.6.x)
2. **PR2 second** (T2.1 → T2.2 → T2.3 → T2.4 → T2.5 → T2.6 → T2.7 → T2.8 → T2.10)
3. **PR3 last** (T3.1 → T3.2 → T3.3 → T3.4 → T3.5 → T3.6 → T3.7 → T3.8 → T3.9 → T3.10 → T3.11 → T3.12)

Each PR's `dotnet test` + `pnpm test` (if applicable) MUST be green before merge.

## Conventions per PR

- **Conventional commits**, Spanish messages, no AI attribution
- **Work-unit commits** (1 commit per logical group, not per file)
- **Branch**: only `main` (no feature branches)
- **Direct merge** to main
- **No force-push**, no interactive rebase
- **Pre-commit hook** runs `dotnet format --verify-no-changes` automatically

## Out of scope (not in any PR)
- Subscriptions / recurring billing
- User-requested refunds
- Multi-currency
- User-to-user gifting
- Credit expiration
- Migrating existing 012-wompi credits (none exist)

## Next
`sdd-apply` → implement the 3 PRs in order, each green, each mergeable on main.