# Tasks: 016-subscription-recurring

## Status

[Tasks] — Ready to apply (3 chained PRs)

## Review workload forecast

- **Total estimated diff**: ~750 lines (3 PRs)
- **400-line budget risk**: MEDIUM (PR2 close to threshold)
- **Chained PRs recommended**: Yes
- **Strategy**: 3 PRs matching 013-credit-consumption pattern
- **Each PR keeps build + test green** (gate per PR)

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

## PR boundaries (locked)

| PR | Scope | Estimated diff | Files (new) | Files (modified) | Test additions |
|----|-------|----------------|-------------|------------------|----------------|
| **PR1** | Domain + Application | ~250 lines | `Subscription.cs`, `SubscriptionStateMachine.cs`, `ISubscriptionService.cs`, `ISubscriptionStore.cs`, `ISubscriptionProvider.cs`, `ISubscriptionFeatureFlag.cs`, 5 handlers | (none in production code) | +20 unit tests |
| **PR2** | Infrastructure + DB | ~300 lines | `EfSubscriptionStore.cs`, `InMemorySubscriptionStore.cs`, `WompiRecurringAdapter.cs`, `DisabledSubscriptionProvider.cs`, `SubscriptionFeatureFlag.cs`, `SubscriptionReconciliationWorker.cs`, `SubscriptionConfiguration.cs`, `20260625_AddSubscriptions.cs` migration | `BuildCvDbContext.cs`, `DependencyInjection.cs`, `HandleWebhookHandler.cs`, `appsettings.json` | +15 integration tests |
| **PR3** | API + Web | ~200 lines | `SubscriptionEndpoints.cs`, BFF routes, components | `Program.cs` (rate limits + map endpoints), `lib/copy/es.ts` | +10 e2e tests |

## PR1: Domain + Application (~250 lines, +20 unit tests)

### T1.1 — Domain entities (TDD)
- **Files**:
  - `BuildCv-api/src/BuildCv.Domain/Subscriptions/Subscription.cs`
  - `BuildCv-api/src/BuildCv.Domain/Subscriptions/SubscriptionPlan.cs` (enum)
  - `BuildCv-api/src/BuildCv.Domain/Subscriptions/SubscriptionStatus.cs` (enum)
  - `BuildCv-api/src/BuildCv.Domain/Subscriptions/SubscriptionStateMachine.cs`
- **Tests** (10+, TDD):
  - `Subscription_Create_SetsAllFields`
  - `Subscription_Starter_Has30CreditsPerMonth`
  - `Subscription_Standard_Has100CreditsPerMonth`
  - `SubscriptionStateMachine_TransitionToActive_AdvancesPeriod`
  - `SubscriptionStateMachine_TransitionToPastDue_IncrementsRetryCount`
  - `SubscriptionStateMachine_TransitionToPastDue_AfterMaxRetries_GoesToCanceled`
  - `SubscriptionStateMachine_UserCancel_TransitionsToCanceled`

### T1.2 — Application ports (TDD)
- **Files**:
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionService.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionStore.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionProvider.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionFeatureFlag.cs`
- **Tests** (3+, contract tests):
  - `ISubscriptionService_Contract`
  - `ISubscriptionStore_Contract`
  - `ISubscriptionProvider_Contract`

### T1.3 — Handlers (TDD, 5 handlers, ~4 tests each)
- **Files**:
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/SubscribeHandler.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/CancelSubscriptionHandler.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/GetSubscriptionHandler.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/HandleRecurringChargeHandler.cs`
  - `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ProcessRetriesHandler.cs`
- **Tests** (10+, TDD):
  - `SubscribeHandler_CreatesWompiCharge_AndPersistsSubscription`
  - `SubscribeHandler_GrantsFirstMonthCredits_ViaAccreditPurchaseHandler`
  - `SubscribeHandler_FailsWhenUserHasActiveSubscription`
  - `CancelSubscriptionHandler_CallsWompiCancel_AndUpdatesStatus`
  - `HandleRecurringChargeSuccess_GrantsCredits_AndAdvancesPeriod`
  - `HandleRecurringChargeFailure_TransitionsToPastDue`

### PR1 acceptance
- [ ] All 20+ tests pass (752/752 = 732 + 20)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] `dotnet build -c Release` 0 warnings
- [ ] Domain has 0 package references (existing constraint)
- [ ] Work-unit commits:
  - `feat(016): domain — Subscription + 2 enums + SubscriptionStateMachine`
  - `feat(016): application — ISubscriptionService + ISubscriptionStore + ISubscriptionProvider + ISubscriptionFeatureFlag`
  - `feat(016): application — 5 handlers (Subscribe, Cancel, Get, HandleRecurringCharge, ProcessRetries)`
  - `test(016): domain + application unit tests (20+)`
- [ ] PR merges to `main`

## PR2: Infrastructure + DB (~300 lines, +15 integration tests)

### T2.1 — EF Core configuration
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Configurations/SubscriptionConfiguration.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs` (MODIFY — add DbSet)
- **Tests** (3+):
  - `SubscriptionConfiguration_MapsToTable_subscriptions`
  - `SubscriptionConfiguration_HasUniqueIndex_OnUserActive`
  - `SubscriptionConfiguration_HasIndex_OnStatusNextChargeAt`

### T2.2 — Migration
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625_AddSubscriptions.cs`
- **Tests** (3+):
  - `Migration_Applies_Successfully`
  - `Migration_CreatesTable_subscriptions`
  - `Migration_HasUniqueIndex_OnUserActive`

### T2.3 — Stores
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/EfSubscriptionStore.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/InMemorySubscriptionStore.cs` (NEW, for tests)
- **Tests** (5+):
  - `EfSubscriptionStore_GetByUserIdAsync_ReturnsActiveSubscription`
  - `EfSubscriptionStore_UpsertAsync_PersistsChanges`
  - `EfSubscriptionStore_GetDueForRetryAsync_FiltersByNextChargeAt`
  - `EfSubscriptionStore_HandlesConcurrentUpdate_WithRetry`

### T2.4 — Wompi adapter + Disabled provider + Feature flag
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/Payments/WompiRecurringAdapter.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/Payments/DisabledSubscriptionProvider.cs` (NEW)
  - `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/SubscriptionFeatureFlag.cs` (NEW)
- **Tests** (5+):
  - `WompiRecurringAdapter_CreateScheduledChargeAsync_CallsWompiApi`
  - `WompiRecurringAdapter_CancelScheduledChargeAsync_CallsWompiApi`
  - `WompiRecurringAdapter_VerifyWebhookSignature_HmacValid`
  - `DisabledSubscriptionProvider_AlwaysReturnsFalse`
  - `SubscriptionFeatureFlag_ReadsConfig`

### T2.5 — Reconciliation worker
- **File**: `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/SubscriptionReconciliationWorker.cs` (NEW, IHostedService)
- **Tests** (3+):
  - `SubscriptionReconciliationWorker_PollsEvery60Seconds`
  - `SubscriptionReconciliationWorker_InvokesRetryHandler_ForDueSubscriptions`
  - `SubscriptionReconciliationWorker_IsIdempotent`

### T2.6 — Extend HandleWebhookHandler (modify)
- **File**: `BuildCv-api/src/BuildCv.Application/Features/Payments/HandleWebhookHandler.cs`
- Add handling for `recurring_charge.successful` + `recurring_charge.failed` events
- Dispatch by event_type BEFORE existing one-time logic
- **Tests** (3+):
  - `HandleWebhook_RecurringChargeSuccessful_GrantsCredits_AndAdvancesPeriod`
  - `HandleWebhook_RecurringChargeFailed_TransitionsToPastDue`
  - `HandleWebhook_OneTimePayment_StillWorks_NoRegression`

### T2.7 — DI Registration + Configuration
- **Files**:
  - `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (MODIFY)
  - `BuildCv-api/src/BuildCv.Api/appsettings.json` (MODIFY — add SubscriptionRecurring section)
- **Tests** (2+):
  - `SubscriptionRecurring_RegistersAllPorts`

### PR2 acceptance
- [ ] All 15+ integration tests pass
- [ ] EF migration applies cleanly (`dotnet ef database update`)
- [ ] 012-wompi webhook handler extended (no regression on one-time path)
- [ ] 013-credit-consumption AccreditPurchaseHandler reused (no new ledger logic)
- [ ] Backward compat: 011/012/013/014/015 test suites still pass
- [ ] DI registered, app starts
- [ ] `dotnet test` green
- [ ] `dotnet format --verify-no-changes` clean
- [ ] Work-unit commits:
  - `feat(016): infrastructure — EF configuration + DbContext`
  - `feat(016): infrastructure — migration AddSubscriptions (20260625)`
  - `feat(016): infrastructure — EfSubscriptionStore + InMemorySubscriptionStore`
  - `feat(016): infrastructure — WompiRecurringAdapter + DisabledSubscriptionProvider + SubscriptionFeatureFlag`
  - `feat(016): infrastructure — SubscriptionReconciliationWorker`
  - `feat(016): infrastructure — extend HandleWebhookHandler for recurring events`
  - `feat(016): infrastructure — DI registration + configuration`
  - `test(016): integration tests (15)`
- [ ] PR merges to `main`

## PR3: API + Web (~200 lines, +10 e2e tests)

### T3.1 — Admin endpoints
- **File**: `BuildCv-api/src/BuildCv.Api/Endpoints/SubscriptionEndpoints.cs`
- **Tests** (5+):
  - `SubscriptionEndpoints_Post_Returns201_WithValidAuth`
  - `SubscriptionEndpoints_Post_Returns409_WhenUserHasActiveSubscription`
  - `SubscriptionEndpoints_Post_Returns503_WhenFeatureFlagDisabled`
  - `SubscriptionEndpoints_GetMe_Returns200`
  - `SubscriptionEndpoints_DeleteMe_Returns200`

### T3.2 — Rate limit policies + Program.cs
- **File**: `BuildCv-api/src/BuildCv.Api/Program.cs` (MODIFY)
- Add 3 new rate limit policies: subscription 10/min, subscription-cancel 5/h, subscription-webhook 60/min
- Register `app.MapSubscriptionEndpoints()`

### T3.3 — Web: BFF routes + components
- **Files**:
  - `BuildCv-web/app/api/subscriptions/route.ts` (NEW — POST + GET)
  - `BuildCv-web/app/api/subscriptions/cancel/route.ts` (NEW — DELETE)
  - `BuildCv-web/components/subscriptions/subscription-card.tsx` (NEW)
  - `BuildCv-web/components/subscriptions/subscribe-modal.tsx` (NEW)
  - `BuildCv-web/components/subscriptions/cancel-modal.tsx` (NEW)
  - `BuildCv-web/lib/copy/es.ts` (MODIFY — add subscription copy)
- **Tests** (3+):
  - `SubscriptionCard_DisplaysPlanAndStatus`
  - `SubscribeModal_OpensPlanPicker`
  - `CancelModal_ShowsConfirmation`

### T3.4 — E2E tests (Playwright)
- **Files**:
  - `BuildCv-web/e2e/subscriptions.spec.ts` (NEW)
- **Tests** (5+):
  - `SubscriptionFlow_Subscribe_NewUser_CreditsGranted`
  - `SubscriptionFlow_Cancel_AccessUntilNextPeriod`
  - `SubscriptionFlow_GetStatus_ReturnsCurrentPlan`
  - `SubscriptionFlow_AlreadyActive_Returns409`
  - `SubscriptionFlow_FeatureFlagDisabled_Returns503`

### PR3 acceptance
- [ ] All 10 e2e tests pass
- [ ] All 6 gates pass: lint, typecheck, test, e2e, build, constitution-check
- [ ] E2E tests pass (89/89 = 79 + 10)
- [ ] Backward compat: 011/012/013/014/015 test suites still pass
- [ ] Work-unit commits:
  - `feat(016): api — SubscriptionEndpoints + DTOs`
  - `feat(016): api — 3 rate limit policies + Program.cs wiring`
  - `feat(016): web — BFF routes + subscription card + subscribe modal`
  - `feat(016): web — cancel modal + i18n copy + dashboard integration`
  - `test(016): e2e API (5) + e2e Web (5 Playwright)`
- [ ] PR merges to `main`

## Test count forecast

| Phase | Before 016 | After 016 | Delta |
|-------|------------|-----------|-------|
| API unit (App) | 208 | 208 + 10 = 218 | +10 |
| API unit (Domain) | 129 | 129 + 7 = 136 | +7 |
| API integration | 109 | 109 + 10 = 119 | +10 |
| API e2e | 93 | 93 + 5 = 98 | +5 |
| **API total** | **732** | **767** | **+35** |
| Web (no major changes, e2e only) | 745 | 745 + 3 = 748 | +3 |
| E2E Playwright | 79 | 79 + 5 = 84 | +5 |
| **TOTAL** | **1556** | **1599** | **+43** |

## Dependency graph (per PR)

```
PR1 (Domain + Application)
  ├── T1.1: Domain entities (no deps)
  ├── T1.2: Application ports (depend on T1.1)
  └── T1.3: Handlers (depend on T1.2)
PR1 → PR2 (blocked until PR1 merges)

PR2 (Infrastructure + DB)
  ├── T2.1: EF config + DbContext (depends on PR1)
  ├── T2.2: Migration (depends on T2.1)
  ├── T2.3: Stores (depends on T2.1)
  ├── T2.4: Wompi adapter + providers (depends on T2.3)
  ├── T2.5: Reconciliation worker (depends on T2.4)
  ├── T2.6: Extend webhook handler (depends on PR1 + T2.4)
  └── T2.7: DI + config (depends on T2.4)
PR2 → PR3 (blocked until PR2 merges)

PR3 (API + Web)
  ├── T3.1: Admin endpoints (depends on PR2)
  ├── T3.2: Rate limit + Program.cs (depends on T3.1)
  ├── T3.3: Web BFF + components (depends on T3.2)
  └── T3.4: E2E tests (depends on T3.3)
```

## Critical execution order

1. **PR1 first** (T1.1 → T1.2 → T1.3)
2. **PR2 second** (T2.1 → T2.2 → T2.3 → T2.4 → T2.5 → T2.6 → T2.7)
3. **PR3 last** (T3.1 → T3.2 → T3.3 → T3.4)

Each PR's `dotnet test` + `pnpm test` MUST be green before merge.

## Conventions per PR

- **Conventional commits**, Spanish messages, no AI attribution
- **Work-unit commits** (1 commit per logical group, not per file)
- **Branch**: only `main` (no feature branches)
- **Direct merge** to main
- **Pre-commit hook** runs `dotnet format --verify-no-changes` automatically

## Out of scope (deferred)

- Multiple tiers beyond 2 plans (v1.5)
- Annual plans (v1.5)
- Free trials (v1.5)
- Promotional pricing / discount codes (v1.5)
- Proration on plan change (v1.5)
- Family/shared plans (out of scope)
- Subscription pause (out of scope)

## Risks

1. **Wompi API complexity** — recurring billing API is more complex than one-time. Mitigation: thorough integration tests + sandbox testing.
2. **Webhook ordering** — recurring charge webhooks may arrive out of order or be delayed. Mitigation: idempotency keys + reconciliation worker.
3. **State machine** — subscription has multiple states. Mitigation: explicit enum + state transition tests.
4. **ARCO anonymize** — subscription data must be anonymized on ARCO delete. Mitigation: cascade-delete subscription rows on user anonymize.
5. **Refunds** — no refunds for canceled subscriptions (per Art. IV). Mitigation: explicit copy + ToS.

## Next

`sdd-apply` → implement the 3 PRs in order, each green, each mergeable on main
