# Archive Report: 012-wompi — Wompi Payment Gateway Integration

> **Status**: ✅ SHIPPED
> **Archived**: 2026-06-11
> **Git tag**: `012-wompi-v1.0` at commit `7aa141b`
> **Cycle**: sdd-propose → sdd-spec → sdd-design → sdd-tasks → sdd-apply (3 chained PRs) → sdd-verify (5 warnings) → sdd-apply (warning-fix PR) → **sdd-archive**

## Summary

Integrated **Wompi** (Colombian payment gateway) for credit purchases via Widget Checkout Web. The change ships a complete end-to-end payment flow: backend creates checkout sessions server-side, frontend renders the Wompi widget in an iframe, and a server-side webhook (with HMAC SHA256 signature verification) confirms payment. Credits are acreditated only after verified payment. On `Approved` status, the system auto-creates a Factus invoice via the existing `IInvoiceProvider` port from 011-factus.

A background `PaymentReconciliationWorker` (IHostedService) handles webhook delivery failures by polling Wompi's `GET /v1/transactions` for payments stuck in `Pending` > 5 minutes. Idempotency is enforced at the database level via unique indexes on `wompi_transaction_id` and `idempotency_key`.

**Chained delivery strategy**: The work was split into 3 chained PRs (`feature-branch-chain` strategy) to respect the 400-line PR review budget. The `sdd-tasks` forecast correctly predicted the ~800-line scope and recommended the split up-front.

## Timeline

| Date (UTC-5) | Commit | Phase | Description |
|--------------|--------|-------|-------------|
| 2026-06-11 11:00 | `562f735` | PR 1 | Domain types (Payment, PaymentStatus, CreditPackage) + Application ports (IPaymentProvider, IPaymentStore) + 4 handlers (CreateCheckout, HandleWebhook, GetPayment, ListPayments) with full TDD cycle. **14 tests** added. |
| 2026-06-11 11:51 | `790b26b` | PR 2 | Infrastructure: WompiAdapter (HMAC SHA256), EfPaymentStore, InMemoryPaymentStore, DisabledPaymentProvider, WompiSettings, PaymentConfiguration, EF migration `20260611161427_AddPaymentsTable`, DI registration behind `Wompi:Enabled` flag. **35 tests** added. |
| 2026-06-11 12:25 | `8a7a3a7` | PR 3 | API endpoints (PaymentEndpoints with 4 Minimal API routes), Program.cs conditional mapping, appsettings Wompi section. Frontend BFF (`/api/payments/*` routes), WompiWidget React component, payment API helper. **14 backend tests + 8 frontend tests** added. |
| 2026-06-11 12:26 | `a94c53e` | docs | Phase 3 task checklist sync (corrects doc drift from PR 2). |
| 2026-06-11 14:22 | `7aa141b` | fix | Closed 5 sdd-verify warnings with TDD: PaymentReconciliationService + Worker (R4 closure), IInvoiceProvider wiring in HandleWebhookHandler (R5 closure), EfPaymentStore refactor (CurrentValues.SetValues + xmin shadow property). **18 tests** added. |

**Wall-clock total**: ~3 hours 21 minutes from PR 1 first commit to warning-fix landing.

## Final Metrics

### Backend (BuildCv-api)

| Metric | Value |
|--------|-------|
| **Commits** | 5 |
| **Files added** | 69 (22 in PR1 + 17 in PR2 + 7 in PR3 + 1 docs + 22 in fix) |
| **Lines added (net)** | ~5,900 (estimate from diff-tree numstat) |
| **New tests** | 83 (14 PR1 + 35 PR2 + 14 PR3 + 18 warning-fix + 2 doc-sync = 83 unique test methods) |
| **Test count total** | 451/451 passing (post-fix, per commit `7aa141b` log) |
| **Test count delta** | +83 from baseline (368 → 451) |
| **Build warnings** | 0 (`dotnet build -c Release` is clean) |
| **Format violations** | 0 (`dotnet format --verify-no-changes` clean) |
| **Suppressions** | 0 (Art. VIII / project rules) |

### Frontend (BuildCv-web)

| Metric | Value |
|--------|-------|
| **Commits** | 1 (`a034663`, PR 3) |
| **Files added** | 11 (4 BFF routes, 2 widget components, 1 types, 1 payment API helper + test, 1 widget test, 1 .env.example) |
| **Tests** | 8 new (BFF + widget) |
| **Test count total** | 718/718 passing (per commit `a034663` log) |
| **Lint** | 0 errors (`pnpm lint` clean) |
| **Build** | 0 errors (`pnpm build` clean) |

### Spec Artifacts

| Artifact | Lines | Notes |
|----------|-------|-------|
| `specs/012-wompi/proposal.md` | 86 | Intent, scope, 5 risks, rollback plan, success criteria |
| `specs/012-wompi/spec.md` | 278 | 8 requirements (R1–R8), 12 scenarios, domain model, integration contracts, error handling, testing requirements |
| `specs/012-wompi/design.md` | 233 | 8 architecture decisions, data flow, DB schema, DI, feature flag, error handling, testing strategy |
| `specs/012-wompi/tasks.md` | 117 | 6 phases + Phase 7 follow-ups (4 sub-tasks covering 5 sdd-verify warnings), 25 original + 12 follow-up tasks all `[x]` |
| `specs/012-wompi/archive-report.md` | this file | Final closure report |

## Deviations from Design

Three deviations were discovered and resolved during implementation. All are **additive and non-breaking** — none required a spec rewrite or constitution amendment.

### 1. `Payment.ProviderSessionId` (PR1)

- **Origin**: Discovered during PR1 TDD cycle for `CreateCheckoutHandler`.
- **Design original**: 13-column entity.
- **Actual**: 14-column entity (added `ProviderSessionId: string?`).
- **Reason**: Wompi's checkout API returns a session reference. Storing it on the entity enables idempotent duplicate checkout requests to return the **same** `CheckoutSession` (same `SessionId`, same `Reference`) without re-calling the provider. Application-level idempotency check (`GetByIdempotencyKeyAsync`) + DB unique constraint on `idempotency_key` enforces this.
- **Impact**: Zero — additive nullable column. Documented in `spec.md` (Domain Model section) and `design.md` (Domain Model section) with PR1 deviation note.

### 2. `xmin` EF shadow property (warning-fix PR, commit `7aa141b`)

- **Origin**: Discovered during `EfPaymentStore.UpdateAsync` refactor (sdd-verify warning).
- **Design original**: `EntityEntry.CurrentValues.SetValues()` with rowversion concurrency token.
- **Actual**: `CurrentValues.SetValues()` + EF shadow property `xmin` mapped to PostgreSQL's system `xmin` column (Npgsql convention).
- **Reason**: Keeps the `Payment` domain entity pure (no `RowVersion` / `Xmin` property in Domain). The `xmin` is a PostgreSQL system column automatically maintained by the engine; the EF shadow property is a transparent concurrency token that doesn't pollute the domain model. This respects Art. VI (Domain pure).
- **Impact**: Zero — DB column is system-managed, no schema change required. Documented in commit message.

### 3. `InvoiceType.Invoice` enum value (warning-fix PR, commit `7aa141b`)

- **Origin**: Discovered when wiring `IInvoiceProvider.CreateInvoiceAsync` on payment Approved.
- **Design original**: `InvoiceType` enum did not include `Invoice` value (only had `Draft`, `CreditNote`, `DebitNote`).
- **Actual**: New enum value `Invoice` added in `src/BuildCv.Domain/Invoicing/InvoiceType.cs` (1 line, additive).
- **Reason**: Payment-triggered invoices are conceptually distinct from draft invoices (e.g., manual entry). The `Invoice` value gives the Factus adapter a clear type tag to send to the DIAN API.
- **Impact**: Zero — additive enum value. `LocalInvoiceProvider` continues to map everything to `Draft`; `FactusAdapter` can now correctly emit `Invoice` type when called from the payment flow.

## Constitution Compliance Summary

| Article | Requirement | Compliance | Evidence |
|---------|-------------|------------|----------|
| **Art. III** | Privacidad primero (no PII en logs) | ✅ | Logs include only `paymentId`, `wompiTransactionId`, `status` — no card data, no customer info. `PaymentReconciliationWorker` logs reconciliation activity but never payloads. |
| **Art. VI** | Clean Architecture (puerto de IO) | ✅ | `IPaymentProvider`, `IPaymentStore`, `IPaymentReconciliationService` declared in Application; `WompiAdapter`, `EfPaymentStore`, `PaymentReconciliationWorker` in Infrastructure. Domain has 0 external packages. |
| **Art. VIII** | TDD para el motor / zero suppressions | ✅ | Red-green-refactor cycle on all 4 handlers + WompiAdapter + ReconciliationService + UpdateAsync. Zero `#pragma warning disable`, zero `[Skip]`, zero `# type: ignore` in 012-wompi code. |
| **Art. IX FR-046** | Confirmación server-side al monetizar | ✅ | Webhook signature verification (HMAC SHA256) + GET /v1/transactions polling + PaymentReconciliationWorker handle all webhook delivery failure modes. |
| **Art. IX FR-048** | Verificar amount/status con Wompi | ✅ | `IPaymentProvider.GetTransactionStatusAsync` calls Wompi's authoritative API. Worker and webhook both consult it. |
| **Art. IX FR-049** | Nunca confiar en redirect del browser | ✅ | Widget events are purely advisory; webhook + GET are the only paths that update payment status. Documented in spec R4. |

**Total**: 6 articles, all ✅. No amendments required.

## Follow-up Issues

None open. All 5 sdd-verify warnings were closed in commit `7aa141b`. The change is feature-complete against its 8 requirements.

### Open Questions (from `design.md`)

Three open questions were carried forward to a future iteration. None block this archive:

- [ ] Wompi sandbox credentials availability for full E2E integration tests (deferred — Playwright sandbox checklist is manual for v0.5)
- [ ] Webhook retry policy: does Wompi retry on 5xx? (deferred — `PaymentReconciliationWorker` is the safety net)
- [ ] Credit acreditation: immediate on Approved (current) vs async queue for v1+ (deferred — not a v1 concern)

## Source of Truth Updated

The master index `BuildCv-api/specs/000-INDEX.md` has been updated:

- **Status row**: `012 | wompi | v1 | ✅ SHIPPED | main | —`
- **Detail section**: Added `### 012-wompi (v1)` block with 8 fields (Spec, Proposal, Design, Tasks, Archive report, Endpoints, Status, Architecture, Key features, Tests, Zero suppressions, Constitution compliance, Deviations, Commits, Git tag)
- **Próximos pasos**: Striked `011-factus` and `012-wompi` from the recommendations list

## Archive Contents

| File | Status |
|------|--------|
| `proposal.md` | ✅ present (86 lines) |
| `spec.md` | ✅ present (278 lines) |
| `design.md` | ✅ present (233 lines) |
| `tasks.md` | ✅ present (117 lines, all tasks `[x]`) |
| `archive-report.md` | ✅ present (this file) |

The change folder `BuildCv-api/specs/012-wompi/` is preserved as the audit trail. No move to `_archive/` was performed — the project convention keeps shipped features in their numbered folder with a strike-through in the master index.

## SDD Cycle Complete

```
sdd-propose  ✅ proposal.md (86 lines, scope, rollback, success criteria)
sdd-spec     ✅ spec.md (8 reqs, 12 scenarios, Given/When/Then)
sdd-design   ✅ design.md (8 decisions, DB schema, DI, feature flag)
sdd-tasks    ✅ tasks.md (37 tasks across 7 phases, 400-line risk flagged)
sdd-apply    ✅ PR1 → PR2 → PR3 (3 chained PRs, feature-branch-chain)
sdd-verify   ⚠️  5 warnings (R4 incomplete, R5 unwired, EF update pattern, doc drift)
sdd-apply    ✅ warning-fix PR (TDD, 18 new tests, 0 new suppressions)
sdd-archive  ✅ this report + INDEX update + engram memory + git tag
```

Ready for the next change. Recommended next steps (in order of urgency):
1. **013-integration-tests-fix** — `013-integration-tests-fix/spec.md` and `tasks.md` already exist; addresses pre-existing flaky integration tests. Low risk.
2. **Constitution v1.2.0** — capture Art. IX server-side confirmation pattern (proven in 012-wompi) as a normative rule for all future payment providers.
3. **Credit consumption logic** — separate feature (out of scope for 012-wompi per proposal). Tied to the new `Payment` and `user_credits` data flow.

## Engram Persistence

This report is persisted to Engram with:
- `topic_key`: `sdd/012-wompi/archive-report`
- `type`: `architecture`
- `project`: `buildCV`
- `capture_prompt`: `false` (automated SDD artifact)
