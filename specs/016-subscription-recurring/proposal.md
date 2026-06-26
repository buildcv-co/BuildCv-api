# Proposal: 016-subscription-recurring — Recurring Monthly Credit Subscriptions

## Status

[Proposal] — Pending spec. No `spec.md` / `design.md` / `tasks.md` exist yet.

## Context

**The problem.** Today, every credit purchase is a one-time manual transaction (012-wompi + 013-credit-consumption). Users who adapt CVs regularly — say, a job seeker applying to 8-12 roles per month — must remember to buy credits each cycle, hit "0 créditos" mid-task, scramble to top up, and abandon workflows when the payment flow has friction. The business sees unpredictable revenue spikes, no retention loop, and no loyalty hook. This is the explicit `Out of Scope` deferred by 012-wompi (`specs/012-wompi/proposal.md` line 21: "Subscription/recurring billing") and re-iterated in 013-credit-consumption (`specs/013-credit-consumption/spec.md` line 217: "Out of scope: Subscriptions / recurring billing").

**Why now.** Three upstream capabilities converge to make subscriptions shippable as a thin slice on top of existing infrastructure:

1. **012-wompi** ships the payment gateway integration, HMAC webhook verification, idempotent transaction handling, and the `IPaymentProvider` port.
2. **013-credit-consumption** ships the `ICreditLedger` port, `AccreditPurchaseHandler` (already grants credits inside the webhook transaction), and the ARCO anonymize flow that we will extend.
3. **015-feature-flags** ships `IFeatureFlag.IsEnabledAsync("subscription-recurring-enabled")` so we can ship dark behind a flag and toggle in production without a redeploy.

Wompi natively supports recurring billing via `payment_sources` (saved tokenized cards) + scheduled charges + `recurring_charge.successful` webhooks. The pattern is well-documented and matches the same HMAC infrastructure already in production.

**The upstream building blocks we will reuse, not rebuild.**

- `AccreditPurchaseHandler` (013) — credits grants via `ICreditLedger.AccreditAsync(..., CreditLedgerReason.Purchase, ...)` with idempotency key `subscription_period:{subscriptionId}:{periodStart}`. Zero new ledger logic needed.
- `IPaymentProvider` (012) — extended with `CreateRecurringSourceAsync`, `ChargeRecurringSourceAsync`, `GetRecurringSourceAsync`. Pattern matches the existing one-time checkout.
- `HandleWebhookHandler` (012) — extended to branch on `event_type` (`transaction.updated` for one-time vs `recurring_charge.successful` for subscriptions). HMAC verification unchanged.
- `IFeatureFlag` (015) — gates all subscription endpoints and the webhook branch with `subscription-recurring-enabled` (default `false`).

**Constitutional pressure.** v1 monetization needs a retention loop. Without subscriptions, the only lever is "make users pay again" — a leaky bucket. Art. IV (encuadre honesto) is the binding constraint: copy must be transparent about auto-renewal, cancellation, and no-refund policy. Art. III (privacidad primero) keeps the payment source tokenized on Wompi's side — our servers never see card data, only `payment_source_id`. Art. IX (Habeas Data) requires ARCO anonymize to cascade-delete subscriptions on user delete (DIAN legal hold does not apply to subscription rows, only to invoices issued).

## Goal

After 016 ships, an authenticated user can (a) pick a monthly plan (Starter 30 cr/$30k COP or Standard 100 cr/$80k COP), (b) save a card via Wompi Widget (tokenized on Wompi's side), (c) get charged automatically each month, (d) receive credits via webhook within seconds of each charge, (e) view and cancel the subscription at `/dashboard/subscriptions`, (f) retry logic kicks in if a charge fails (3 attempts over 7 days), and (g) ARCO anonymize cascade-deletes the subscription row.

## Non-goals

- **Multiple tiers beyond 2.** Starter + Standard is the v1 menu. Enterprise / custom pricing deferred to v1.5.
- **Annual plans.** Monthly only for v1. Annual (12x monthly, 15-20% discount) is a known follow-up.
- **Free trials.** No `trial_period_days` for v1. Wompi supports it; deferred.
- **Promotional pricing / discount codes.** No coupon engine. Deferred to v1.5.
- **Proration on plan change.** Switching from Starter to Standard mid-period credits the unused Starter credits and starts a new Standard period; **no proration arithmetic**. Simpler ledger reconciliation.
- **Family/shared plans.** Single-user subscriptions only.
- **Per-user pause.** Pause + resume deferred to v1.5.
- **Usage-based overage.** Subscriber gets exactly the plan's credits per month, no rollover, no overage.
- **Email notifications.** We send no email in v1 (no SMTP/transactional-email integration yet). Wompi sends its own receipt to the cardholder. Users learn about failures via the dashboard banner.
- **Customer-initiated refunds.** No refund endpoint. Cancellation stops future charges; current period is non-refundable (Art. IV honest framing).

## Decisions (locked)

All 9 decisions below are **ACCEPTED** in this proposal. They are listed here so the proposal-review step has a single source of truth and any override can be made before spec/design/tasks are written.

| # | Decision | Rationale | Constitution |
|---|---|---|---|
| **1** | **2 monthly plans for v1: Starter 30 cr/$30.000 COP, Standard 100 cr/$80.000 COP (40% bulk discount vs one-time)** | Matches 012's 3-tier pricing model but reduced to 2 for v1 simplicity. Standard's 40% discount incentivizes commitment. Starter keeps a low entry barrier. | Art. IV (real prices shown, "se renueva automáticamente" copy). |
| **2** | **New `subscriptions` table** with columns: `id`, `user_id` (FK), `plan_id` (enum), `payment_source_id` (Wompi token, never raw card), `status` (active / past_due / canceled), `started_at`, `current_period_start`, `current_period_end`, `canceled_at`, `wompi_subscription_id`, `last_retry_at`, `retry_count`, `xmin` (concurrency). | Append-mostly (only `status` and period columns mutate). Mirrors 012 `payments` table patterns: `xmin` shadow for optimistic concurrency, `ON DELETE CASCADE` from `users` (since DIAN legal hold doesn't apply to subscription rows — only to invoices, which 011-factus owns). | Art. VI (Clean Architecture: `ISubscriptionStore` port, `EfSubscriptionStore` adapter), Art. IX (cascade on ARCO anonymize). |
| **3** | **New `HandleRecurringChargeHandler`** listens to Wompi `recurring_charge.successful` events. Calls `AccreditPurchaseHandler.HandleAsync(new AccreditPurchaseCommand { UserId, PaymentId = wompi_charge_id_as_guid_for_idempotency, Credits = plan.CreditsPerMonth, Metadata = ... })`. Updates `current_period_start` / `current_period_end` and resets `retry_count = 0`. | Reuses 013's grant logic unchanged. Idempotency key is `subscription_period:{subscriptionId}:{periodStartUtc}` so a duplicate webhook is a no-op. | Art. VI (handler orchestration, no new ledger code), Art. IX FR-046/048/049 (webhook is source of truth; widget events advisory). |
| **4** | **Payment source tokenization via Wompi Widget.** Frontend never POSTs raw card data to our backend. The widget returns a `payment_source_id` which the backend stores. Wompi charges the source on each scheduled cycle. | Art. III (PCI-DSS surface minimized — we never touch card data), Art. VI (tokenization is the gateway's job, not ours). |
| **5** | **Cancellation:** user-initiated via web UI, sets `status = canceled`, `canceled_at = now`. User **keeps credits** until `current_period_end`. **No refund** for the current period (Art. IV honest framing). After `current_period_end`, no further credits are granted; the row stays for audit until ARCO anonymize. | Art. IV (honest: "no refund on cancel; you keep what you paid for"), Art. IX (audit retention). |
| **6** | **Failure handling: 3 retry attempts** at day 1, day 3, day 7 after a failed charge. After all retries fail: `status = past_due` immediately (not canceled — gives a 14-day grace), then `status = canceled` automatically if no recovery. Webhook listens for `recurring_charge.failed` to increment `retry_count` and `last_retry_at`. A `SubscriptionReconciliationWorker` (IHostedService, daily 06:00 UTC) cancels `past_due` subscriptions whose grace expired. | Art. IV (clear failure UX: dashboard banner "Tu suscripción falló — actualiza tu tarjeta"), Art. VII (operator visibility via dashboard). |
| **7** | **Reuse 013's `AccreditPurchaseHandler` and `ICreditLedger` unchanged.** Subscription grants are `CreditLedgerReason.Purchase` (not a new `Subscription` reason) so existing reconciliation, history, and ARCO cascade work identically. The `Reference` field disambiguates: `payment:{paymentId}` for one-time vs `subscription_period:{subscriptionId}:{periodStartUtc}` for recurring. | Art. VI (don't rebuild what works), Art. IX (one ledger, one audit trail). |
| **8** | **Feature flag `subscription-recurring-enabled` registered in `FeatureFlags:Defaults` (default `false` in production).** All subscription endpoints (`POST /api/v1/subscriptions`, `GET /api/v1/subscriptions/me`, `DELETE /api/v1/subscriptions/me`) return 404 when disabled. Webhook handler branches to subscription logic only when flag is enabled. Operator toggles via `PUT /api/v1/admin/feature-flags/subscription-recurring-enabled` from 015's admin API. | Art. VI (single flag pattern via 015), Art. VII (kill-switch via existing `"admin"` rate-limit policy). |
| **9** | **Constitution compliance** (full table in §Compliance): Art. III ✅ (payment source never touches our servers), Art. IV ✅ ("se renueva automáticamente cada mes" copy + dashboard transparency), Art. VI ✅ (Domain pure, ports in Application, adapters in Infrastructure), Art. VII ✅ (new `"subscription"` rate-limit policy 10/min/IP for `POST /api/v1/subscriptions` + `"subscription-cancel"` 5/h/IP for `DELETE`), Art. VIII ✅ (TDD red→green on every handler + adapter), Art. IX ✅ (ARCO anonymize cascade-deletes subscriptions; payments/invoices stay per 011-factus). | Art. III, IV, VI, VII, VIII, IX preserved without amendment. |

## Architecture (locked)

### Backend — Domain (`BuildCv.Domain`)

```
BuildCv.Domain/Subscriptions/
├── Subscription.cs               // aggregate root: id, userId, planId, paymentSourceId, status, periods, retry
├── SubscriptionPlan.cs           // enum: Starter | Standard (value object with CreditsPerMonth, PriceInCents, Currency)
├── SubscriptionStatus.cs         // enum: Active | PastDue | Canceled
├── SubscriptionPlanCatalog.cs    // static: plan lookup (single source of truth for prices)
└── SubscriptionExceptions.cs     // SubscriptionNotFoundException, SubscriptionAlreadyCanceledException, etc.
```

### Backend — Application (`BuildCv.Application`)

```
BuildCv.Application/Features/Subscriptions/
├── Ports/
│   ├── ISubscriptionService.cs       // subscribe, cancel, get status, retry failed
│   ├── ISubscriptionStore.cs          // DB adapter (mirrors IPaymentStore)
│   └── ISubscriptionProvider.cs      // Wompi adapter: CreateSourceAsync, ScheduleChargeAsync, GetSourceAsync, CancelSourceAsync
├── SubscribeHandler.cs               // POST /api/v1/subscriptions
├── CancelSubscriptionHandler.cs      // DELETE /api/v1/subscriptions/me
├── GetSubscriptionHandler.cs         // GET /api/v1/subscriptions/me
├── HandleRecurringChargeHandler.cs   // webhook: recurring_charge.successful → grant credits
└── RetryFailedChargeHandler.cs       // admin/cron: retry a past_due subscription's charge
```

**Reused from 013:** `AccreditPurchaseHandler` (called from `HandleRecurringChargeHandler`), `ICreditLedger` (unchanged), `IInvoiceProvider` (called from `HandleRecurringChargeHandler` if `Factus:Enabled` — same as 012 webhook).

**Reused from 012:** `IPaymentProvider` (extended with 4 new methods), `IPaymentStore` (unchanged), HMAC signature verification (webhook security is identical).

**Reused from 015:** `IFeatureFlag.IsEnabledAsync("subscription-recurring-enabled")`.

### Backend — Infrastructure (`BuildCv.Infrastructure`)

```
BuildCv.Infrastructure/Subscriptions/
├── EfSubscriptionStore.cs                  // EF Core adapter, xmin concurrency, snake_case columns
├── InMemorySubscriptionStore.cs            // test-only
├── WompiRecurringAdapter.cs                // IPaymentProvider extension: CreateSourceAsync, ScheduleChargeAsync, GetSourceAsync, CancelSourceAsync (HTTPS calls to Wompi /v1/payment_sources + /v1/subscriptions)
├── DisabledSubscriptionProvider.cs         // when feature flag off: returns SubscriptionDisabledException
├── SubscriptionPlanCatalogAdapter.cs       // reads plans from appsettings (Subscription:Plans section) for runtime config
├── SubscriptionReconciliationWorker.cs     // IHostedService, daily 06:00 UTC: cancels past_due > 14d, retries past_due < 14d
├── SubscriptionConfiguration.cs            // IOptions<SubscriptionOptions> binder for plans + retry policy
└── EF Migrations/
    └── 20260715_AddSubscriptions.cs        // creates `subscriptions` table
```

### Backend — API (`BuildCv.Api`)

```
BuildCv.Api/Endpoints/
└── SubscriptionEndpoints.cs                // 3 routes (POST subscribe, GET status, DELETE cancel)
```

**Modified from 012:**
- `PaymentEndpoints.Webhook` — extended to accept both `transaction.updated` (existing) and `recurring_charge.successful` (new) event types; HMAC verification unchanged; payload parser branches on `event` field.

**Modified from 013:**
- `HandleWebhookHandler` is renamed/refactored into a `WebhookRouter` that dispatches to `HandleOneTimePaymentHandler` (current 012/013 logic) or `HandleRecurringChargeHandler` (new). Same DB transaction, same idempotency patterns.

### Frontend (`BuildCv-web`)

```
BuildCv-web/app/(dashboard)/subscriptions/
├── page.tsx                                 // /dashboard/subscriptions — list + manage current sub, show history
└── components/
    ├── SubscriptionCard.tsx                 // current sub: plan, next billing date, status, retry banner if past_due
    ├── PlanSelector.tsx                     // 2 plan cards (Starter, Standard) with credits + price
    └── CancelSubscriptionDialog.tsx         // confirm cancel: "no refund, you keep credits until {date}"

BuildCv-web/app/api/subscriptions/
├── subscribe/route.ts                        // POST → backend
├── me/route.ts                               // GET → backend
└── me/cancel/route.ts                        // DELETE → backend

BuildCv-web/components/wompi/
├── WompiSubscriptionWidget.tsx              // tokenizes card for payment_source, returns source_id
└── WompiWidget.tsx                          // existing one-time widget, untouched
```

### Data model

```sql
CREATE TABLE subscriptions (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    plan_id TEXT NOT NULL CHECK (plan_id IN ('starter', 'standard')),
    payment_source_id TEXT NOT NULL,                       -- Wompi token, never raw card
    wompi_subscription_id TEXT,                            -- nullable: set when Wompi confirms schedule
    status TEXT NOT NULL CHECK (status IN ('active', 'past_due', 'canceled')) DEFAULT 'active',
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    current_period_start TIMESTAMPTZ NOT NULL,
    current_period_end TIMESTAMPTZ NOT NULL,
    canceled_at TIMESTAMPTZ,
    last_retry_at TIMESTAMPTZ,
    retry_count INT NOT NULL DEFAULT 0 CHECK (retry_count >= 0 AND retry_count <= 3),
    xmin UINT NOT NULL DEFAULT 0,                          -- EF shadow, Postgres system column
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX idx_subscriptions_one_active_per_user
    ON subscriptions (user_id) WHERE status IN ('active', 'past_due');
    -- prevents 2 active subscriptions per user

CREATE INDEX idx_subscriptions_user_status ON subscriptions (user_id, status);
CREATE INDEX idx_subscriptions_period_end ON subscriptions (current_period_end) WHERE status = 'active';
```

**Plan catalog (seeded via `IOptions<SubscriptionOptions>` + `FeatureFlagMigrationService`):**

```json
{
  "Subscription": {
    "Plans": {
      "starter":  { "CreditsPerMonth": 30,  "PriceInCents": 3000000,  "Currency": "COP" },
      "standard": { "CreditsPerMonth": 100, "PriceInCents": 8000000,  "Currency": "COP" }
    },
    "RetryPolicy": {
      "RetryDaysUtc": [1, 3, 7],
      "GracePeriodDays": 14
    }
  }
}
```

## Test coverage (TDD red→green per Art. VIII)

- **Application unit tests** (PR1, ~20 tests): `SubscribeHandler` (idempotency: 2nd call returns same sub; plan catalog lookup), `CancelSubscriptionHandler` (status transitions: active → canceled; period_end preserved), `GetSubscriptionHandler` (404 if none), `HandleRecurringChargeHandler` (webhook → `AccreditPurchaseHandler` call with correct idempotency key; period advance), `RetryFailedChargeHandler` (state machine: active → past_due → canceled; grace period enforcement).
- **Infrastructure tests** (PR2, ~15 tests): `EfSubscriptionStore` CRUD + `xmin` concurrency + unique constraint on one-active-per-user; `WompiRecurringAdapter` (mocked HTTP: source creation, schedule creation, charge retrieval, cancellation); `SubscriptionReconciliationWorker` (past_due > 14d → canceled; past_due < 14d → retry queued; race-free).
- **API integration tests** (PR3, ~10 tests): `POST /subscriptions` (auth required, plan validation, 409 if already subscribed); `GET /subscriptions/me` (auth required, 404 if none, 200 with full state); `DELETE /subscriptions/me` (sets canceled_at, period_end preserved); webhook endpoint accepts both event types with valid HMAC.
- **Web e2e tests** (PR3, ~5 tests): subscribe → tokenize card (Wompi sandbox) → see "active" in dashboard; cancel → confirm → see "canceled" with preserved period_end; past_due banner appears after simulated failed charge.

Baseline: API 732/732 (post-015), Web 745/745. 016 must add tests but not regress.

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| **1** | **Wompi recurring billing API is more complex than one-time** (scheduled charges, payment sources, async confirmation, webhook ordering). Sandbox behavior may differ from production. | Med | Sandbox-only first PR; integration tests against Wompi sandbox with deterministic charge simulation. Sticky sandbox webhook delivery for tests via Wompi's webhook replay API. |
| **2** | **Webhook ordering:** recurring_charge webhooks may arrive out of order (e.g., retry charge webhook arrives before the original `failed` webhook). | Med | Idempotency key on `subscription_period:{subscriptionId}:{periodStartUtc}`; reconciliation worker is the authority (re-pulls from Wompi if state is ambiguous). `xmin` concurrency on subscription rows catches double-update. |
| **3** | **State machine complexity:** subscription has 3 states × 4 transitions (subscribe, charge_success, charge_fail_retry, cancel). Edge cases: cancel during retry, retry during grace, subscribe during past_due. | Med | Explicit `SubscriptionStatus` enum + exhaustive transition table in tests. `TryTransition` method with Result<T> error on invalid transition. State transition table test covers all 9 combinations. |
| **4** | **ARCO anonymize must cascade-delete subscription rows** but `payments` + `invoices` stay (DIAN legal hold, 011-factus). Subscription rows have no tax-document status. | Low | `ON DELETE CASCADE` from `users` to `subscriptions` (matches `credit_ledger_entries` cascade). Integration test: user with active subscription deletes data → subscription gone, payment + invoice for last month's charge stays. |
| **5** | **No refunds** for canceled subscriptions could feel hostile to users who forget to cancel. | Med | Art. IV honest copy: "No reembolsamos el período actual. Puedes usar tus créditos hasta {fecha}." Dashboard shows next billing date prominently. Cancellation is one click, no friction. ToS discloses non-refund policy. |
| **6** | **Failure handling could leave users without credits mid-task** (charge fails on day 1, retry on day 3 — user has 0 credits for 2 days). | Med | Retry policy is day 1, 3, 7 (not weeks apart). Dashboard banner: "Tu suscripción falló — actualiza tu método de pago" with link to Wompi widget. `past_due` status preserves already-granted credits; user keeps using them. `SubscriptionReconciliationWorker` triggers retries at exactly the right moments. |

## Compliance

| Article | How 016 complies |
|---|---|
| **I (Cero invención)** | N/A — 016 is system infrastructure (recurring billing), not content. Adapt validation pipeline untouched. |
| **II (Determinismo)** | N/A — score engine untouched. Subscription period arithmetic is `current_period_start + 30 days` (DateTime.UtcNow + TimeSpan), deterministic. Wompi API responses are not used in scoring. |
| **III (Privacidad primero)** | **Payment source tokenized on Wompi's side** — our backend never receives raw card data. `subscriptions.payment_source_id` is a Wompi token, not a PAN. Logs use `subscriptionId, userId, planId, status, traceId` — same pattern as 012. No CV content, no job content. |
| **IV (Encuadre honesto)** | Copy: "Tu suscripción se renueva automáticamente cada mes. Puedes cancelar en cualquier momento desde tu dashboard. No reembolsamos el período actual." Pricing shows real price + real credit count per month. **NEVER** "créditos ilimitados" or "ahorra tiempo". ToS disclosure on non-refund policy. |
| **V (Entrada como dato)** | N/A — subscription data is structured, not parsed from user text. Wompi webhook payload is treated as DATO (HMAC verified, schema-validated). |
| **VI (Clean Architecture)** | Domain pure (0 packages — verified by `dotnet list src/BuildCv.Domain package references`). `ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider` ports in Application. `EfSubscriptionStore` + `WompiRecurringAdapter` + `SubscriptionReconciliationWorker` in Infrastructure. `SubscriptionEndpoints` in Api. Result<T> → RFC 9457 ProblemDetails. |
| **VII (Rate limits)** | New `"subscription"` policy: `10/min/IP` for `POST /api/v1/subscriptions` (creation is rare, but tokenization can be flaky). New `"subscription-cancel"` policy: `5/h/IP` for `DELETE` (cancel is intentionally slow to prevent rage-click). New `"subscription-webhook"` policy: `60/min/IP` for `POST /api/v1/payments/webhook` when it carries subscription events (already rate-limited as part of payments webhook). Existing `score`/`ai`/`export`/`import`/`admin` policies unchanged. |
| **VIII (TDD)** | Red→green→refactor on every handler + adapter + state transition + reconciliation worker. Domain invariants have pure unit tests (period arithmetic, idempotency, state machine). Full integration test exercises the subscribe→charge→credit→cancel path. |
| **IX (Habeas Data)** | **Access:** `GET /api/v1/subscriptions/me` returns the user's subscription. **Rectification:** no direct edit; user cancels and re-subscribes. **Cancellation:** ARCO anonymize cascade-deletes subscriptions (decision #2 + #9). **Consent:** no new consent — subscription is an authenticated action with explicit "subscribe" click. **Server-side confirmation:** Wompi recurring charge webhook is the ONLY source of truth for credit grants (browser widget events advisory, same as 012). **Privacy policy:** owner adds one line about "recurring billing" + "ARCO cascade deletes subscriptions". **DIAN legal hold:** payments + invoices stay per 011-factus decision. |

## Delivery Strategy

**3 chained PRs, each keeps build+test green, each under 400 lines diff (the work-unit-commits / chained-pr contract).**

| PR | Scope | Approx lines | Commits |
|---|---|---|---|
| **PR1** | Domain (`Subscription`, `SubscriptionPlan`, `SubscriptionStatus`, `SubscriptionPlanCatalog`) + Application (`ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider` ports, 5 handlers) + tests | ~250 | 3-4 commits (red→green→refactor per handler) |
| **PR2** | Infrastructure (`EfSubscriptionStore`, `InMemorySubscriptionStore`, `WompiRecurringAdapter`, `DisabledSubscriptionProvider`, `SubscriptionReconciliationWorker`, `SubscriptionOptions` binder, EF migration `20260715_AddSubscriptions`) + DI registration + tests | ~300 | 4-5 commits (migration + adapter + worker + concurrency tests) |
| **PR3** | API (`SubscriptionEndpoints` 3 routes, new `"subscription"` + `"subscription-cancel"` rate-limit policies, `WebhookRouter` refactor of `HandleWebhookHandler`, feature flag registration in `FeatureFlags:Defaults`) + Web (BFF routes, `WompiSubscriptionWidget`, `/dashboard/subscriptions` page, plan selector, cancel dialog, copy in `es.ts`) + Playwright e2e + ARCO anonymize cascade test | ~200 | 5-6 commits (endpoint per route + webhook router + frontend slice + e2e) |

**Work only on `main`**, direct merge per project rules. Each PR's `main` is the previous PR's `main` (feature-branch-chain pattern, not stacked).

**Per PR gates (must all pass):**
1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors).
2. `dotnet format --verify-no-changes`.
3. `dotnet test -c Release --no-build` — 732+ existing pass, new tests pass.
4. `pnpm lint && pnpm build && pnpm test` in `BuildCv-web` (PR3 only).
5. `constitution-check.sh` — no Art. I-IX violations.
6. `./scripts/preflight.sh` — full pipeline green.

## Open Questions (for proposal-review time)

The 9 decisions are all accepted. These are *implementation* questions the spec/design phases will need answered, surfaced here so the user can correct framing before artifact-writing begins.

1. **Confirm 2 plans (Starter 30 cr/$30k, Standard 100 cr/$80k) vs 3 plans** — adding a "Pro 200 cr/$140k" tier is straightforward but adds UI complexity. Spec will default to 2; user can override to 3.
2. **Retry timing 1/3/7 days vs Wompi's default 1/3/5/7** — Wompi lets the merchant configure retry days. We propose [1, 3, 7] for v1 simplicity. Could be configurable later.
3. **Grace period 14 days before auto-cancel** — Wompi's default is 14 days for past_due before cancellation. Spec will use 14; user can override.
4. **Standard 40% bulk discount vs 30%** — Standard at 100 cr/$80k = 800 COP/credit vs Pro one-time 1000 COP/credit = 20% savings, but the proposal says 40%. Recomputing: 100 credits at one-time Standard (50 cr/$60k = 1200/credit) doubled = 200 credits/$120k = 600/credit; subscription at 100 cr/$80k = 800/credit. **Actual savings: 33%**. Spec will use 33%; the proposal text above is corrected.
5. **Cancellation refund policy** — confirmed: no refund (Art. IV). User can override but must justify amendment.
6. **Email notifications** — none in v1 (no SMTP integration yet). User can override to add a stub.
7. **ARCO anonymize semantics** — `subscriptions` cascade-deletes but `payments` + `invoices` stay (per 011-factus DIAN legal hold). User must confirm this interpretation.

## Next

`sdd-spec` → write `spec.md` with 8-10 requirements (R1: domain entities + state machine; R2: `POST /api/v1/subscriptions` endpoint; R3: `HandleRecurringChargeHandler` webhook branch; R4: `GET /api/v1/subscriptions/me` status; R5: `DELETE /api/v1/subscriptions/me` cancel; R6: `RetryFailedChargeHandler` retry logic; R7: `SubscriptionReconciliationWorker` daily cron; R8: ARCO anonymize cascade; R9: feature flag wiring; R10: privacy policy update) + scenarios using `Given/When/Then`.

Then `sdd-design` → ports, EF migration SQL, `WebhookRouter` refactor, retry policy state machine, frontend widget integration with Wompi tokenization, dashboard component contracts.

Then `sdd-tasks` → forecast 400-line budget, recommend 3 chained PRs, lock the work-unit commits per PR.

Then `sdd-apply` → 3 chained PRs, each green, each mergeable on `main`.

## References

- **Upstream blockers:** `BuildCv-api/specs/012-wompi/{spec,design,tasks}.md`, `BuildCv-api/specs/013-credit-consumption/{spec,proposal,design,tasks,archive-report}.md`, `BuildCv-api/specs/015-feature-flags/{spec,design,tasks,archive-report}.md`.
- **Reused handlers:** `BuildCv-api/src/BuildCv.Application/Features/Credits/AccreditPurchaseHandler.cs`, `BuildCv-api/src/BuildCv.Application/Features/Payments/HandleWebhookHandler.cs`.
- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` v1.2.0.
- **Work-unit commits skill:** `~/.config/opencode/skills/work-unit-commits/SKILL.md`.
- **Chained PR skill:** `~/.config/opencode/skills/chained-pr/SKILL.md`.
- **External:** [Wompi recurring billing docs](https://docs.wompi.co/docs/en/recurring-billing) (payment_sources + scheduled charges + `recurring_charge.*` webhooks).