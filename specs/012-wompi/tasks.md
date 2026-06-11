# Tasks: 012-wompi — Wompi Payment Gateway Integration

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain+Application) → PR 2 (Infrastructure) → PR 3 (API+Web) |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain types + Application ports + Handlers | PR 1 → base: feature/wompi | Foundation; all tests for handlers included |
| 2 | Infrastructure adapters + DB + DI | PR 2 → base: PR 1 branch | WompiAdapter, EfPaymentStore, InMemoryPaymentStore, migration, config |
| 3 | API endpoints + Web widget + BFF | PR 3 → base: PR 2 branch | Minimal API routes, frontend component, integration wiring |

## Phase 1: Domain Types

- [x] 1.1 Create `src/BuildCv.Domain/Payments/PaymentStatus.cs` — enum: Pending, Approved, Failed, Error (size: **S**)
- [x] 1.2 Create `src/BuildCv.Domain/Payments/CreditPackage.cs` — sealed record with static Starter/Standard/Pro catalog (size: **S**)
- [x] 1.3 Create `src/BuildCv.Domain/Payments/Payment.cs` — sealed record entity (14 props: `ProviderSessionId` added during PR1 for idempotent session replay) (size: **M**)

## Phase 2: Application Ports + Handlers (TDD)

- [x] 2.1 RED: Write failing tests for `CreateCheckoutHandler` — valid checkout, idempotent duplicate, invalid package (size: **M**)
- [x] 2.2 GREEN: Create `IPaymentProvider.cs`, `IPaymentStore.cs`, `CheckoutSession.cs`, `TransactionStatus.cs`, `CreateCheckoutHandler.cs` — make tests pass (size: **M**)
- [x] 2.3 RED: Write failing tests for `HandleWebhookHandler` — valid Approved, tampered sig, duplicate idempotent (size: **M**)
- [x] 2.4 GREEN: Create `HandleWebhookHandler.cs` — HMAC verify, status update (invoice trigger deferred to PR2 infrastructure) (size: **M**)
- [x] 2.5 RED: Write failing tests for `GetPaymentHandler` + `ListPaymentsHandler` (size: **S**)
- [x] 2.6 GREEN: Create `GetPaymentHandler.cs` + `ListPaymentsHandler.cs` (size: **S**)

## PR1 Deliverables (Shipped)

| Files | Count |
|-------|-------|
| Domain | 3 (PaymentStatus, CreditPackage, Payment) |
| Application | 8 (2 ports + 4 records + 4 handlers) |
| Tests | 7 files (14 tests passing) |

**Verification**: `dotnet build -c Release` ✅ 0 warnings | `dotnet format --verify-no-changes` ✅ | `dotnet test --filter Payments` ✅ 14/14

## Phase 3: Infrastructure

- [x] 3.1 Create `src/BuildCv.Infrastructure/Payments/WompiSettings.cs` — Options class with Enabled, Environment, keys (size: **S**)
- [x] 3.2 Create `src/BuildCv.Infrastructure/Payments/WompiAdapter.cs` — HttpClient, CreateCheckout, GetTransactionStatus, VerifyWebhookSignature (size: **L**)
- [x] 3.3 Write tests for `WompiAdapter.VerifyWebhookSignature` with known HMAC payloads (size: **M**)
- [x] 3.4 Create `src/BuildCv.Infrastructure/Payments/InMemoryPaymentStore.cs` — for testing (size: **S**)
- [x] 3.5 Create `src/BuildCv.Infrastructure/Persistence/PaymentConfiguration.cs` — EF Core config, indexes (size: **M**)
- [x] 3.6 Create `src/BuildCv.Infrastructure/Payments/EfPaymentStore.cs` — Postgres persistence (size: **M**)
- [x] 3.7 Add `DbSet<Payment>` to `BuildCvDbContext` + EF migration (size: **S**)
- [x] 3.8 Update `DependencyInjection.cs` — register Wompi services behind `Wompi:Enabled` flag (size: **S**)

## Phase 4: API Endpoints

- [x] 4.1 Create `src/BuildCv.Api/Endpoints/PaymentEndpoints.cs` — 4 Minimal API routes with auth/HMAC guards (size: **M**)
- [x] 4.2 Conditionally map endpoints in `Program.cs` behind `Wompi:Enabled` (size: **S**)

## Phase 5: Web BFF + Widget

- [x] 5.1 Create Wompi widget React component (lazy-loaded) in `BuildCv-web/` (size: **M**)
- [x] 5.2 Create BFF proxy routes for `/api/payments/*` in `BuildCv-web/app/api/` (size: **M**)
- [x] 5.3 Add `Wompi` section to `appsettings.json` / `appsettings.Development.json` (size: **S**)

## Phase 6: Verification

- [x] 6.1 Run `dotnet build BuildCv.slnx -c Release` — 0 warnings (size: **S**)
- [x] 6.2 Run `dotnet test` — all pass, ≥90% coverage on handlers + WompiAdapter (size: **S**)
- [x] 6.3 Verify zero suppressions across all new files (size: **S**)

## Phase 7: Verification Follow-ups (from sdd-verify warnings)

### 7.1: Background polling worker for stale payments (R4 closure) [size: **M**]
- [x] 7.1.1 Create `src/BuildCv.Application/Features/Payments/PaymentReconciliationService.cs` — finds Pending payments > 5 min
- [x] 7.1.2 Create `src/BuildCv.Infrastructure/Payments/PaymentReconciliationWorker.cs` — `IHostedService` that polls every 60s
- [x] 7.1.3 Write tests for `PaymentReconciliationService` (finds stale, calls provider, updates status)
- [x] 7.1.4 Register worker in `DependencyInjection.cs` behind `Wompi:Enabled`

### 7.2: Wire invoice auto-creation on Approved (R5 closure) [size: **M**]
- [x] 7.2.1 Inject `IInvoiceProvider` into `HandleWebhookHandler` constructor
- [x] 7.2.2 On status=Approved, call `IInvoiceProvider.CreateInvoiceAsync` with payment details
- [x] 7.2.3 Update tests to verify invoice creation happens on Approved
- [x] 7.2.4 Document R5 implementation in spec.md

### 7.3: Refactor `EfPaymentStore.UpdateAsync` [size: **S**]
- [x] 7.3.1 Replace detach-then-Update with `EntityEntry.CurrentValues.SetValues()`
- [x] 7.3.2 Verify all existing tests still pass
- [x] 7.3.3 No new suppressions

### 7.4: Doc fixes [size: **S**]
- [x] 7.4.1 Mark Phase 3 tasks as complete in tasks.md (corrects doc drift)
- [x] 7.4.2 Fix checklist math error in PR1 deliverable summary

## PR3 Deliverables (Shipped)

| Files | Count |
|-------|-------|
| API Endpoints | 1 (PaymentEndpoints + CheckoutRequest + PaymentResponse) |
| API Config | 2 (appsettings.json + appsettings.Development.json Wompi section) |
| API Tests | 2 (14 endpoint tests) |
| DI registration | 1 (4 payment handlers) |
| Web BFF | 4 (checkout, webhook, [id], list) |
| Web Widget | 3 (WompiWidget, LazyWompiWidget, wompi-types) |
| Web API helper | 2 (payment.ts + payment.test.ts) |
| Web Widget test | 1 (WompiWidget.test.tsx) |
| Web Config | 1 (.env.example NEXT_PUBLIC_WOMPI_ENABLED) |

**Verification**: `dotnet build -c Release` ✅ 0 warnings | `dotnet format --verify-no-changes` ✅ | `dotnet test` ✅ 431/431 (14 new PR3 tests) | `pnpm lint` ✅ | `pnpm build` ✅ | `pnpm test` ✅ 718/718 (8 new PR3 tests) | Zero suppressions ✅
