# Proposal: 013-credit-consumption — Credit Consumption for Paid Features

## Status

[Proposal] — Pending spec (no spec.md / design.md / tasks.md exist yet).

## Context

**The problem.** A user pays Wompi for a 10/50/100-credit package. The webhook fires, the payment row moves to `Approved`, the Factus invoice is created — and the user **never receives the credits**. The "buy → spend" loop is broken at the seam, so v1 monetization is non-functional: a user can pay, see "Approved", but the `adapt` endpoint will never deduct from a balance that does not exist.

**Why now.** This was a known deferred item in 012-wompi (`specs/012-wompi/proposal.md` line 24: "Credit consumption logic (separate feature)"). 012 proved the purchase pipe (Wompi → invoice) but explicitly stopped short of the consume pipe. 011-factus integration was wired in the same webhook transaction, so the surface for adding credit ledger is already touched. The Clean Architecture ports pattern is established (`IAiClient`, `ICvParser`, `IPdfGenerator`, `IPaymentProvider`, `ICvStore`), and a complete v1 design for the credit ledger already exists in the archive (`BuildCv-web/specs/_archive/001-web-mvp-original/data-model.md` §B.5-B.6).

**The upstream blocker.** `BuildCv-api/src/BuildCv.Application/Features/Payments/HandleWebhookHandler.cs` lines 45-83 update the payment status and call `IInvoiceProvider.CreateInvoiceAsync` (lines 68-80), but the handler has zero references to a credit ledger. The 012 `PaymentReconciliationService` (background reconciliation) has the same gap. The 009-auth `User` entity has no `CreditBalance` field, and `BuildCvDbContext` has no `DbSet<CreditLedgerEntry>`. Everything needs to be built.

**Constitutional pressure.** v0 (Art. VII) launches without accounts or payments. v1 (the current target) introduces accounts (009 ✅), persistence (010 ✅), optional DIAN invoicing (011 ✅), and Wompi payments (012 ✅). The credit ledger is the missing business primitive that closes the v1 monetization loop while staying compatible with Art. III (no CV content, no PII in ledger rows) and Art. IX (Habeas Data: ARCO delete cascade, server-side confirmation only).

## Goal

After 013 ships, an authenticated user can (a) buy credits via Wompi and see them reflected in their balance within seconds of the webhook, (b) spend exactly 1 credit per `POST /api/v1/adapt` call, (c) be auto-refunded if the LLM call fails before the first token is emitted, (d) inspect their full ledger history, (e) see a live "N créditos" badge in the web nav, and (f) hit HTTP 402 with a clear "Comprar más créditos" CTA when balance is 0.

## Non-goals

- **Subscriptions / recurring billing.** One-time credit packs only (10/50/100 COP packages from 012).
- **Refund flows beyond LLM failure.** No customer-support refund endpoint, no partial refunds, no Stripe-like dispute flow. Operator tooling only (`POST /credits/gift` for manual adjustments).
- **Multi-currency.** COP only, matches 012 pricing.
- **Other endpoints consuming credits.** `score`, `import`, `export` remain free + IP-rate-limited (Art. VII v0.5 behavior). Only `adapt` consumes 1 credit per call.
- **Credit expiration.** Credits never expire. No `ExpiresAt` on ledger entries.
- **Per-user rate limit on `ai`.** The existing IP-based `ai` 5/h policy stays (no Constitution amendment). Authenticated users with 100 credits still hit 429 at 6 adaptations/hour.
- **Constitution amendment.** v1.1.0 stays in force. The credit gate is a business limit layered on top of the existing rate limits, not a replacement.
- **Tiptap / Rich editor / push-notifications / analytics.** Unrelated.
- **Constitution v1.2.0.** Deferred to whatever triggers it (likely the ZDR Anthropic Enterprise gate, per Art. IX note).

## Decisions

All 8 user-facing defaults are **ACCEPTED** in this proposal. They are listed here so the proposal-review step has a single source of truth and any override can be made before spec/design/tasks are written.

| # | Decision | Rationale | Constitution |
|---|---|---|---|
| **1** | Welcome credits on signup: **3 credits, idempotency key `welcome:{userId}`** | Onboarding is the first monetizable moment; 3 = 3 free adaptations = "show, don't tell". Idempotency prevents replay abuse. | Art. IV (encuadre honesto: "3 adaptaciones gratis" copy, not "3 créditos gratis" as a tease). |
| **2** | What consumes: **only `adapt` (1 credit/call)** | Matches the v1 archived design. Keeps `score`/`import`/`export` frictionless (Art. VII). | Art. VII (v0 lanzable sin fricción for non-AI endpoints). |
| **3** | Expiration: **never** | Matches archived design. Simplest. Avoids cron/cleanup code. Can be added later via batch-expire. | Art. III (no extra storage of PII; expiration = more state). |
| **4** | AI rate limit: **keep IP-based 5/h** | Zero Constitution change. Already a working anti-abuse wall. Credits are a business limit, not anti-abuse. | Art. VII (unchanged). |
| **5** | Refund on LLM failure: **yes, before first token** | Fairness: user paid for value they didn't get. Symmetric with consumption idempotency. | Art. IX FR-046 (server-side confirmation: refund is server-side, not browser-driven). |
| **6** | Auth on `adapt`: **full gate, breaking change** | Aligned with v1 hardening (009-auth shipped). Cleanest path. The v0→v1 transition is implicit (v0 used `StubAiClient`, 0 cost). | Art. VII (v0 explicit; v1 hardening is the spec). |
| **7** | Constitution amendment: **none** | All 4 rate-limit policies stay; `credit` is a business limit, not a 5th policy. Art. III, IV, VI, VIII, IX need no text change. | All articles preserved. |
| **8** | Privacy policy update: **yes, owner updates externally** | One new line in `GET /api/v1/privacy-policy` disclosing "credit balance tracking" + "ARCO delete cascades ledger". | Art. IX FR-053 (política de tratamiento). |
| **9** | **ARCO delete vs DIAN invoices: anonymize user + cascade ledger + KEEP payments/invoices** | `payments` has `ON DELETE RESTRICT` because the DIAN invoice is a tax document with Colombian legal hold (separate from user data). `credit_ledger_entries` has no legal hold, cascades. The 009 ARCO handler must know: when a user has paid invoices, **anonymize** the user row (`email='[deleted]@anonymized'`, `name='[Deleted User]'`) and cascade only the ledger. **Highest legal risk — flag for review.** | Art. IX FR-052 (ARCO rights) + 011-factus (DIAN legal hold). |

## Architecture

**Option A (chosen): Denormalized `users.credit_balance` + append-only `credit_ledger_entries`**

The archived v1 design from 001 is the strongest starting point. Ledger is the source of truth, balance is a denormalized cache. This matches the 012-wompi `xmin` shadow + EF concurrency + unique-index idempotency patterns already proven in production.

### Backend — Domain (PR1)

```csharp
// BuildCv.Domain/Credits/
public enum CreditReason { Purchase, Gift, Consumption, Refund, Adjustment }

public sealed record CreditLedgerEntry
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int Delta { get; init; }                       // +N for purchase/gift, -N for consumption/refund
    public CreditReason Reason { get; init; }
    public string? Reference { get; init; }               // payment.id | adaptation.id | "welcome:{userId}"
    public string? Description { get; init; }             // human-readable, no PII
    public int BalanceAfter { get; init; }                // audit snapshot
    public DateTime CreatedAt { get; init; }
}

// BuildCv.Domain/Auth/User.cs — additive property
public sealed record User
{
    // ... existing 7 properties
    public int CreditBalance { get; init; }               // NEW, default 0
}
```

### Backend — Application (PR1)

```csharp
// BuildCv.Application/Features/Credits/Ports/
public interface ICreditLedger
{
    Task<CreditBalanceSnapshot> GetBalanceAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<CreditLedgerEntry>> GetHistoryAsync(Guid userId, int page, int perPage, CancellationToken ct);
    Task<Result<CreditLedgerEntry>> AccreditPurchaseAsync(Guid userId, Guid paymentId, int credits, CancellationToken ct);
    Task<Result<CreditLedgerEntry>> AccreditGiftAsync(Guid userId, int credits, string reference, string description, CancellationToken ct);
    Task<Result<CreditLedgerEntry>> TryConsumeAsync(Guid userId, int amount, CreditReason reason, string reference, string description, CancellationToken ct);
    Task<Result<CreditLedgerEntry>> RefundAsync(Guid userId, Guid originalLedgerEntryId, string description, CancellationToken ct);
    Task<ReconcileResult> ReconcileAsync(Guid userId, CancellationToken ct);  // operator tool
}

public interface ICreditConsumptionService
{
    Task<Result<CreditConsumptionResult>> ConsumeForAdaptationAsync(Guid userId, Guid adaptationId, CancellationToken ct);
    Task<Result<CreditConsumptionResult>> RefundForAdaptationAsync(Guid userId, Guid adaptationId, Guid ledgerEntryId, CancellationToken ct);
}
```

**5 handlers:** `AccreditPurchaseHandler`, `ConsumeForAdaptationHandler`, `RefundConsumptionHandler`, `GetBalanceHandler`, `GetHistoryHandler`. The higher-level `ICreditConsumptionService` is the abstraction `AdaptCvHandler` calls — it doesn't know about ledger internals.

### Backend — Infrastructure (PR2)

- `EfCreditLedger` — implements `ICreditLedger`. Single transaction: `INSERT credit_ledger_entries` + `UPDATE users SET credit_balance = credit_balance + delta`. Translates `DbUpdateException` (unique violation on `(reason, reference)`) into idempotent no-op success.
- `InMemoryCreditLedger` for the InMemory provider (tests).
- `CreditLedgerEntryConfiguration` — snake_case columns, indexes on `user_id+created_at` (history queries), **unique index on `(reason, reference)` for idempotency** (the bulletproof defense).
- `UserConfiguration` — add `credit_balance` column with `CHECK (credit_balance >= 0)` + `xmin` shadow property.
- `BuildCvDbContext` — register `DbSet<CreditLedgerEntry>`.
- EF migration: `ALTER TABLE users ADD COLUMN credit_balance int NOT NULL DEFAULT 0` + `CHECK` + new `credit_ledger_entries` table with all indexes/constraints.
- DI: register `EfCreditLedger`, `InMemoryCreditLedger`, 5 handlers in `AddApplication()` and `AddInfrastructure()`.

### Backend — API (PR3)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/credits/balance` | JWT | `{ balance: int, lastUpdatedAt: DateTime }` |
| `GET` | `/api/v1/credits/history?page&perPage` | JWT | Paginated ledger entries |
| `GET` | `/api/v1/credits/health` | JWT + Admin | Operator reconcile: `Σ delta vs users.credit_balance` |
| `POST` | `/api/v1/credits/gift` | JWT + Admin | Manual `Adjustment` entry (welcome credits, promo, support credit) |

**Endpoint modifications:**
- `POST /api/v1/adapt` — add `.RequireAuthorization()` and a custom `.RequireCredits(1)` endpoint filter that maps `CREDIT/INSUFFICIENT` → HTTP 402 with `Retry-After: 0` and `code: "INSUFFICIENT_CREDITS"` in ProblemDetails body (RFC 9457).
- `POST /api/v1/payments/webhook` — `HandleWebhookHandler.HandleAsync` lines 68-80 gain an `await _creditLedger.AccreditPurchaseAsync(payment.UserId, payment.Id, payment.Credits, ct)` call inside the same `try` as the invoice creation. Both must succeed or both must roll back (single transaction).
- `PaymentReconciliationService` — same credit grant added so background reconciliation also credits if the original webhook failed.
- `HandleOAuthCallbackHandler` (009-auth) — on first signup, post a welcome `AccreditGiftAsync(userId, 3, reference: "welcome:{userId}", description: "Welcome credits")` after the user is created.
- `DeleteUserDataHandler` (009-auth ARCO) — gain branch: when user has `payments` rows, anonymize the user row (`email`, `name`, `provider_id` → `[deleted]@anonymized` / `[Deleted User]` / `redacted`) and cascade-delete `credit_ledger_entries`; payments + invoices stay. The reconciliation log records the anonymization.
- `score` / `import` / `export` — **unchanged** (anonymous + IP rate-limited).

### Frontend (PR3)

- **`app/api/credits/balance/route.ts`** + **`app/api/credits/history/route.ts`** — BFF proxies with `cookie: request.headers.get('cookie')` pattern.
- **`lib/api/credits.ts`** — `fetchBalance()`, `fetchHistory({ page, perPage })`, `CreditError` class. Mirrors `lib/api/payment.ts` style.
- **`lib/api/adapt.ts`** — add 402 branch: `AdaptError({ code: 'INSUFFICIENT_CREDITS', kind: 'payment_required' })`.
- **`components/wompi/WompiWidget.tsx`** + **`LazyWompiWidget.tsx`** — gain `onPaymentApproved` callback that re-fetches `/api/credits/balance` and updates the badge (optimistic; webhook is source of truth per Art. IX).
- **`components/layout/credit-badge.tsx`** — "N créditos" in nav, color states (green ≥ 5, yellow 1-4, red 0), `aria-live="polite"` for SR.
- **`components/layout/low-credit-banner.tsx`** — dismissible banner when balance ≤ 2, with "Comprar más" link to `/pricing`. Threshold via `NEXT_PUBLIC_LOW_CREDIT_THRESHOLD`.
- **`app/analizar/page.tsx`** — "Adaptar" button disabled when balance = 0, tooltip + link to pricing.
- **`lib/copy/es.ts`** — new strings: "Te quedan {N} créditos", "Créditos insuficientes", "Comprar más créditos", "1 crédito = 1 adaptación".

### Test coverage (all PRs, TDD red→green per Art. VIII)

- **Application unit tests** (PR1): 5+ per handler. Domain invariants (`BalanceAfter == previous + delta`, sign rules per reason, idempotency on `reference`).
- **Infrastructure tests** (PR2): concurrency race (two parallel consumes with balance=1 → one wins, other gets 402), idempotent re-apply (duplicate `(reason, reference)` → success no-op), `CHECK >= 0` enforcement, EF migration runs clean.
- **API integration tests** (PR3): auth required, 200 happy path, 401 anonymous, 402 when balance=0, refund on LLM failure, admin-only endpoints reject non-admin, 400 on negative amount.
- **Web unit tests**: `lib/api/credits.ts` + `lib/api/adapt.ts` 402 mapping.
- **Web Playwright e2e**: sign in → buy 10 credits (sandbox) → adapt once (200) → adapt again (402) → buy more → adapt (200). This is the user-facing acceptance test.

Baseline: API 451/451, Web 718/718 = 1169/1169. 013 must add tests but not regress.

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| **1** | **`HandleWebhookHandler` couples invoice + ledger in one transaction.** If either throws, both must roll back or the user gets credits without an invoice (or vice versa). | Med | Integration test: webhook + invoice + ledger all succeed → 3 rows. Webhook + invoice OK + ledger throws → 0 rows (transaction rollback). Webhook + ledger OK + invoice throws → catch in existing try, log, payment stays Approved but no credits (current 012 behavior). |
| **2** | **Race condition on parallel consumes with balance=1.** Two concurrent `POST /adapt` requests could both read 1, both decrement to 0, ending at -1. | Med | `xmin` shadow property on `users` (proven in `PaymentConfiguration.cs`) + `CHECK (credit_balance >= 0)` at DB level + `MaxRetry` on transient EF exceptions. One consume wins, the other gets `DbUpdateException` → mapped to 402. |
| **3** | **ARCO delete vs DIAN `ON DELETE RESTRICT` (legal).** A user with paid invoices exercising ARCO delete would currently fail. | High (legal) | Decision #9: anonymize user row + cascade ledger + keep payments/invoices. **Requires explicit user (and ideally lawyer) sign-off before PR1 implementation.** Disclose in `archive-report.md` as a "v1 limitation". |
| **4** | **EF migration runs before code deploys.** A Wompi webhook between migration and code rollout could credit via the OLD broken handler, silently losing the credit. | Low | Feature flag `Credits:Enabled` (default `false` in production). Webhook + reconciliation check the flag before calling `ICreditLedger`. Same pattern as `Wompi:Enabled`. |
| **5** | **Test data state pollution.** Integration tests that consume credits need a fresh balance per test. | Med | New xUnit `[Collection("CreditsDb")]` fixture: per-test `TRUNCATE credit_ledger_entries` + `UPDATE users SET credit_balance = 0`. EF InMemory provider for unit tests; PostgreSQL test container for integration. |
| **6** | **Frontend credit badge stale after Wompi payment.** Widget `APPROVED` event fires before webhook, so badge shows 0 for a few seconds. | Med | `onPaymentApproved` calls `fetchBalance()` optimistically; webhook is source of truth (Art. IX FR-049). If balance still 0 after 30s, show "procesando pago" toast. |
| **7** | **PR review budget > 400 lines.** Total diff estimated ~800 lines. | High | **3 chained PRs (250/300/250) per work-unit-commits skill.** Each keeps build+test green. Work only on main, direct merge. |

## Compliance

| Article | How 013 complies |
|---|---|
| **I (Cero invención)** | N/A. 013 is system infrastructure, not content. Adapt validation pipeline untouched. |
| **II (Determinismo)** | N/A. Score engine untouched. Credit math is integer arithmetic (deterministic by definition). |
| **III (Privacidad)** | `CreditLedgerEntry` stores metadata only: no CV content, no job content, no PII beyond `UserId` (already known) and operator metadata. Logs use the 012 pattern: `userId`, `amount`, `reason`, `reference`, `traceId`. No card data, no CV content, no IP. |
| **IV (Encuadre honesto)** | Copy: "1 crédito = 1 adaptación" or "1 credit per AI adaptation". **NEVER** "créditos ilimitados" or "garantiza entrevista". Pricing page shows real price / real credit count / no hidden tiers. |
| **V (Entrada como dato)** | N/A. The ledger is downstream of validated input, not subject to prompt injection. |
| **VI (Clean Architecture)** | Domain has 0 packages (proven via `dotnet list src/BuildCv.Domain package references`). `ICreditLedger` and `ICreditConsumptionService` ports in Application; `EfCreditLedger` in Infrastructure; `CreditEndpoints` in Api. `Result<T>` → RFC 9457 `ProblemDetails` mapping follows the 012 pattern. |
| **VII (Rate limits)** | `score`/`export`/`import` unchanged. `ai` 5/h by IP unchanged. New `credit` business limit (1/adapt) layered on top, not replacing. No new policy added; no amendment. |
| **VIII (TDD)** | All 7 new handlers + 1 new EF adapter + 2 new endpoints have tests written first (red→green→refactor). Domain invariants have pure unit tests. Full integration test exercises the webhook→invoice→ledger→consume path. |
| **IX (Habeas Data)** | **Access:** `GET /credits/history` gives the user their full ledger. **Rectification:** ledger is append-only; corrections post `Adjustment` entries (operator tool). **Cancellation:** ARCO delete anonymizes user + cascades ledger (decision #9). **Consent:** no new consent needed (operational metadata, not content). **Server-side confirmation:** Wompi webhook + reconciliation is the ONLY source of truth for credit grants; widget `APPROVED` event is advisory. **Privacy policy update:** owner adds one line about credit balance tracking + ARCO cascade. |

## Delivery Strategy

**3 chained PRs, each keeps build+test green, each under 400 lines diff (the work-unit-commits / chained-pr contract).**

| PR | Scope | Approx lines | Commits |
|---|---|---|---|
| **PR1** | Domain (`User.CreditBalance`, `CreditLedgerEntry`, `CreditReason`) + Application (`ICreditLedger`, `ICreditConsumptionService`, 5 handlers) + tests | ~250 | 3-4 commits (red→green→refactor per handler) |
| **PR2** | Infrastructure (`EfCreditLedger`, `InMemoryCreditLedger`, `CreditLedgerEntryConfiguration`, `UserConfiguration` +1 column, `BuildCvDbContext` +1 DbSet, EF migration) + DI + tests | ~300 | 4-5 commits (migration + adapter + concurrency tests) |
| **PR3** | API (`CreditEndpoints` 4 routes, `.RequireCredits(1)` filter, 402 mapping) + `HandleWebhookHandler` + `PaymentReconciliationService` + `HandleOAuthCallbackHandler` welcome grant + `DeleteUserDataHandler` ARCO anonymize + Web (BFF routes, `lib/api/credits.ts`, `lib/api/adapt.ts` 402, badge, banner, adapt page 402, WompiWidget refresh) + Playwright e2e | ~250 | 5-6 commits (endpoint per route + 402 filter + welcome grant + ARCO branch + frontend slice) |

**Work only on `main`**, direct merge per project rules. Each PR's `main` is the previous PR's `main` (feature-branch-chain pattern, not stacked).

**Per PR gates (must all pass):**
1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors).
2. `dotnet format --verify-no-changes`.
3. `dotnet test -c Release --no-build` — 451+ existing pass, new tests pass.
4. `pnpm lint && pnpm build && pnpm test` in `BuildCv-web` (PR3 only).
5. `constitution-check.sh` — no Art. I-IX violations.
6. `./scripts/preflight.sh` — full pipeline green.

## Open Questions (for proposal-review time)

The 8 decisions are all accepted. These are *implementation* questions the spec/design phases will need answered, surfaced here so the user can correct framing before artifact-writing begins.

1. **ARCO anonymization (Decision #9) — confirm?** The `email='[deleted]@anonymized'` + `name='[Deleted User]'` approach is the cleanest interpretation of Art. IX + DIAN legal hold, but it's a legal call. **Does the user want a Colombian data-protection lawyer review before PR1 ships, or is the current anonymization sufficient for v1?**
2. **Welcome credits amount = 3 — confirm?** The archived design had this as D20 (deferred decision). 3 is a guess. Could be 1, 5, or 10. The spec phase will set the exact constant.
3. **Low-credit banner threshold = 2 — confirm?** `NEXT_PUBLIC_LOW_CREDIT_THRESHOLD=2` is the proposed env var. Could be 1, 3, or 5.
4. **402 UX — modal vs inline?** When `adapt` returns 402, should the adapt UI show a modal with "Comprar más créditos" + cancel, or an inline error with a link, or a toast? Spec will default to modal; user can override.

## Next

`sdd-spec` → write `spec.md` with 9+ requirements (R1: ledger, R2: consume, R3: refund, R4: history, R5: balance, R6: welcome, R7: ARCO anonymize, R8: webhook integration, R9: 402 filter, R10: privacy disclosure) + scenarios using `Given/When/Then`.

Then `sdd-design` → ports, EF migration, endpoint filter implementation, frontend component contracts.

Then `sdd-tasks` → forecast 400-line budget, recommend 3 chained PRs, lock the work-unit commits per PR.

Then `sdd-apply` → 3 chained PRs, each green, each mergeable on `main`.

## References

- **Exploration report:** engram `sdd/013-credit-consumption/explore` (project: `buildcv`).
- **Archived v1 design:** `BuildCv-web/specs/_archive/001-web-mvp-original/data-model.md` §B.5-B.6 + `tasks.md` T102-T113.
- **012-wompi (upstream blocker):** `BuildCv-api/specs/012-wompi/spec.md`, `design.md`, `archive-report.md`.
- **010-persistence (EF pattern):** `BuildCv-api/specs/010-persistence/spec.md`.
- **009-auth (ARCO + JWT):** `BuildCv-api/specs/009-auth/spec.md`.
- **011-factus (DIAN legal hold):** `BuildCv-api/specs/011-factus/spec.md`.
- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` v1.1.0.
- **Work-unit commits skill:** `~/.config/opencode/skills/work-unit-commits/SKILL.md`.
- **Chained PR skill:** `~/.config/opencode/skills/chained-pr/SKILL.md`.
