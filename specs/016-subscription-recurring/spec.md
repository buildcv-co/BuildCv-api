# Spec: 016-subscription-recurring

## Status

[Spec] — Pending design

## Overview

Allow authenticated users to subscribe to monthly credit packages that auto-renew via Wompi `payment_sources` + scheduled charges. On each successful monthly charge, credits are granted automatically through the existing `AccreditPurchaseHandler` (013-credit-consumption). The system adds retry logic, a reconciliation worker, ARCO cascade deletion, feature-flag gating, and an honest "se renueva automáticamente" / "sin reembolso" copy contract.

## Domain model

### Subscription (new aggregate root)

- `Id: Guid` (PK, UUIDv7)
- `UserId: Guid` (FK → `users.id`, `ON DELETE CASCADE`)
- `Plan: SubscriptionPlan` (enum: Starter, Standard)
- `PaymentSourceId: string` (Wompi token, never raw PAN)
- `WompiSubscriptionId: string?` (set when Wompi confirms schedule)
- `Status: SubscriptionStatus` (enum: Active, PastDue, Canceled)
- `StartedAt: DateTime` (UTC)
- `CurrentPeriodStart: DateTime` (UTC)
- `CurrentPeriodEnd: DateTime` (UTC)
- `CanceledAt: DateTime?` (null unless canceled)
- `LastChargeAt: DateTime?` (null before first charge)
- `NextChargeAt: DateTime` (computed: `CurrentPeriodEnd - 3d` retry buffer; in `PastDue` = next retry attempt)
- `LastRetryAt: DateTime?` (null if no retries yet)
- `RetryCount: int` (`CHECK (0..3)`)
- `xmin: uint` (EF shadow, Postgres concurrency)

### SubscriptionPlan enum

```csharp
public enum SubscriptionPlan
{
    Starter = 1,    // 30 credits/month, $30,000 COP, currency "COP"
    Standard = 2,   // 100 credits/month, $80,000 COP, currency "COP"
}
```

### SubscriptionStatus enum

```csharp
public enum SubscriptionStatus
{
    Active = 1,    // Auto-renewing, charges succeeding
    PastDue = 2,   // Last charge failed, retrying within grace window
    Canceled = 3,  // User canceled or auto-canceled after retries exhausted
}
```

### Constraints / Indexes

- `UNIQUE(UserId) WHERE Status IN (Active, PastDue)` — one active subscription per user
- `INDEX(Status, NextChargeAt)` — reconciliation worker queries
- `CHECK (Status IN (1,2,3))`, `CHECK (Plan IN (1,2))`, `CHECK (RetryCount BETWEEN 0 AND 3)`
- `xmin` optimistic concurrency on subscription rows

### State machine

```
              Subscribe
                  ↓
         ┌────→ Active ←──────── (recurring_charge.successful)
         │          ↓
         │      PastDue (recurring_charge.failed or first charge failure)
         │          ↓
         │      [retry @ day 1, 3, 7]
         │          ↓
         │      Active (retry succeeds)
         │      OR
         │      Canceled (all retries fail; auto after 14-day grace)
         │
         └────── Canceled ←────── (user cancels anytime via DELETE)
```

## Requirements

### R1: Domain entities + state machine

The system MUST persist subscription state in `subscriptions` with the entity, enums, constraints, and state machine defined above. Transitions MUST be explicit (`TryTransition` returns `Result<T>`; invalid transitions fail closed). (Art. VI)

#### Scenario: New subscription starts Active

- GIVEN an authenticated user calls SubscribeHandler with `plan="starter"` and a valid Wompi token
- WHEN the handler completes
- THEN a row is inserted with `Status=Active`, `StartedAt=now`, `CurrentPeriodStart=now`, `CurrentPeriodEnd=now+30d`, `NextChargeAt=now+27d`

#### Scenario: Successful renewal advances the period

- GIVEN a Subscription with `Status=Active` and a verified `recurring_charge.successful` webhook arrives
- WHEN `HandleRecurringChargeHandler` processes it
- THEN `CurrentPeriodStart = old.CurrentPeriodEnd`, `CurrentPeriodEnd = old.CurrentPeriodStart + 30d`, `LastChargeAt=now`, `Status=Active`, `RetryCount=0`

#### Scenario: Invalid transition is rejected

- GIVEN a Subscription with `Status=Canceled`
- WHEN any handler tries to set `Status=Active`
- THEN the call returns `Result.Failure("SUBSCRIPTION/INVALID_TRANSITION")` and no DB write occurs

### R2: Subscribe endpoint (`POST /api/v1/subscriptions`)

The system MUST accept a plan + payment source, create a Wompi scheduled charge, persist the subscription, grant first-month credits via `AccreditPurchaseHandler` (idempotency key `subscription_period:{subscriptionId}:{periodStartUtc}`), and return HTTP 201. The endpoint MUST be gated by `IFeatureFlag.IsEnabledAsync("subscription-recurring-enabled")`. (Art. VI, Art. IX FR-046)

#### Scenario: First subscribe succeeds

- GIVEN an authenticated user with no active subscription and feature flag enabled
- WHEN `POST /api/v1/subscriptions` is called with body `{ "plan": "starter", "paymentSourceId": "ps_test_xxx" }`
- THEN a Wompi scheduled charge is created, a Subscription row is persisted with `Status=Active`, 30 credits are granted, and the response is HTTP 201 with `{ id, plan, status, currentPeriodStart, currentPeriodEnd }`

#### Scenario: Already subscribed returns 409

- GIVEN the user has an `Active` or `PastDue` subscription
- WHEN `POST /api/v1/subscriptions` is called again
- THEN the response is HTTP 409 with `{ error: "SUBSCRIPTION/ALREADY_ACTIVE" }` and no second row is inserted

#### Scenario: Feature flag off returns 503

- GIVEN `subscription-recurring-enabled=false`
- WHEN `POST /api/v1/subscriptions` is called
- THEN the response is HTTP 503 with `{ error: "SUBSCRIPTION/DISABLED" }` and no DB or Wompi call occurs

### R3: `HandleRecurringChargeHandler` (webhook branch)

The system MUST extend the existing 012-wompi webhook handler to dispatch on `event_type`: `transaction.updated` (one-time, existing path) and `recurring_charge.successful` / `recurring_charge.failed` (new path). HMAC verification is unchanged. The new handler MUST call `AccreditPurchaseHandler.HandleAsync` with idempotency key `subscription_period:{subscriptionId}:{periodStartUtc}`. (Art. VI, Art. IX FR-046/048/049)

#### Scenario: Successful recurring charge grants credits

- GIVEN a verified webhook with `event_type=recurring_charge.successful` and matching `payment_source_id`
- WHEN `HandleRecurringChargeHandler` processes it
- THEN the subscription period advances (R1), 30 or 100 credits are granted (per Plan) via `AccreditPurchaseHandler`, the ledger gains one `Purchase` row, and the response is HTTP 200 `{ received: true }`

#### Scenario: Duplicate webhook is idempotent

- GIVEN the same `recurring_charge.successful` webhook arrives twice
- WHEN both fire
- THEN only one ledger row exists (unique `(UserId, Reason, Reference)` blocks the second) and the period is advanced exactly once

#### Scenario: Failed charge triggers retry

- GIVEN a verified webhook with `event_type=recurring_charge.failed`
- WHEN `HandleRecurringChargeHandler` processes it
- THEN `Status=PastDue`, `NextChargeAt=now+1d`, `LastRetryAt=now`, and `RetryCount` increments by 1 (capped at 3)

### R4: Get subscription status (`GET /api/v1/subscriptions/me`)

The system MUST return the authenticated user's current subscription (including `Canceled`) or 404 if none. (Art. IX — access)

#### Scenario: Active subscription returns 200

- GIVEN an authenticated user with an Active subscription
- WHEN `GET /api/v1/subscriptions/me` is called
- THEN the response is HTTP 200 with `{ id, plan, status, currentPeriodStart, currentPeriodEnd, nextChargeAt, canceledAt? }`

#### Scenario: No subscription returns 404

- GIVEN an authenticated user with no subscription
- WHEN `GET /api/v1/subscriptions/me` is called
- THEN the response is HTTP 404 with `{ error: "SUBSCRIPTION/NOT_FOUND" }`

### R5: Cancel subscription (`DELETE /api/v1/subscriptions/me`)

The system MUST set `Status=Canceled`, `CanceledAt=now`, cancel the Wompi scheduled charge, and return HTTP 200. Credits already granted remain usable until `CurrentPeriodEnd`. No refund is issued for the current period. (Art. IV honest framing)

#### Scenario: User cancels active subscription

- GIVEN an authenticated user with an Active subscription
- WHEN `DELETE /api/v1/subscriptions/me` is called
- THEN `Status=Canceled`, `CanceledAt=now`, the Wompi scheduled charge is canceled, and the response is HTTP 200 with `{ status: "canceled", accessUntil: currentPeriodEnd }`

#### Scenario: Canceling twice is idempotent

- GIVEN a subscription already in `Canceled` status
- WHEN `DELETE /api/v1/subscriptions/me` is called again
- THEN the response is HTTP 200 with the same `accessUntil` (no second Wompi call)

#### Scenario: Credit balance is preserved

- GIVEN an Active subscription with `current_period_end = T+5d` and 30 credits in balance
- WHEN the user cancels
- THEN `credit_balance` is unchanged at 30 and remains usable until `T+5d`

### R6: Retry handler

The system MUST schedule retries on `Status=PastDue` subscriptions at day 1, 3, 7 after the first failure. After the 3rd retry fails, `Status=Canceled` is set automatically. A 14-day grace period applies between the first failure and auto-cancel. (Art. IV clear failure UX, Art. VI)

#### Scenario: Retry succeeds within grace

- GIVEN a PastDue subscription with `RetryCount=1` and `NextChargeAt <= now`
- WHEN the retry handler invokes Wompi and the charge succeeds
- THEN `Status=Active`, `LastChargeAt=now`, `NextChargeAt=current_period_end - 3d`, `RetryCount=0`, and the period advances

#### Scenario: Third retry fails and auto-cancels

- GIVEN a PastDue subscription with `RetryCount=3` and the latest retry fails
- WHEN the retry handler runs
- THEN `Status=Canceled`, `CanceledAt=now`, the Wompi scheduled charge is canceled, and no more retries are scheduled

#### Scenario: Grace period expired cancels without retry

- GIVEN a PastDue subscription with `now > lastRetryAt + 14d`
- WHEN the reconciliation worker runs
- THEN `Status=Canceled` is set and no further Wompi charge attempts occur

### R7: Reconciliation worker

The system MUST run `SubscriptionReconciliationWorker` (IHostedService, every 60s) that queries `subscriptions WHERE Status='past_due' AND NextChargeAt <= now`, invokes the retry handler for each, and is idempotent across runs. (Art. VI)

#### Scenario: Worker retries due subscriptions

- GIVEN 3 subscriptions with `Status=PastDue` and `NextChargeAt <= now`
- WHEN the worker ticks
- THEN all 3 retry handlers fire (Wompi charge attempts), results are persisted, and the next tick processes only what remains due

#### Scenario: Worker is idempotent

- GIVEN the worker runs twice in 60s with no new failures
- WHEN both ticks complete
- THEN no duplicate Wompi calls and no extra ledger rows are produced

### R8: ARCO anonymize cascade (Habeas Data)

The system MUST cascade-delete `subscriptions` rows on ARCO anonymize (no tax-document status — DIAN legal hold does not apply), while preserving `payments` + `invoices` per 011-factus. The Wompi scheduled charge MUST be canceled via Wompi API before the cascade runs. (Art. IX FR-052)

#### Scenario: User with active subscription deletes data

- GIVEN user U with an Active subscription
- WHEN `DELETE /api/v1/user/data` is called by U
- THEN the Wompi scheduled charge is canceled, the subscription row is cascade-deleted, the user is anonymized, and `payments` + `invoices` for prior charges remain intact

#### Scenario: No subscription → no Wompi call

- GIVEN user U with no subscription
- WHEN `DELETE /api/v1/user/data` is called by U
- THEN the anonymize flow runs unchanged from 013 (no Wompi call needed)

### R9: Feature flag wiring

The system MUST register `subscription-recurring-enabled` in `FeatureFlags:Defaults` (default `false` in production) and gate every subscription endpoint and the webhook subscription branch behind `IFeatureFlag.IsEnabledAsync("subscription-recurring-enabled")`. (Art. VI, Art. VII)

#### Scenario: Flag off → 503 on all subscription endpoints

- GIVEN `subscription-recurring-enabled=false`
- WHEN `POST /api/v1/subscriptions`, `GET /api/v1/subscriptions/me`, or `DELETE /api/v1/subscriptions/me` is called
- THEN the response is HTTP 503 with `{ error: "SUBSCRIPTION/DISABLED" }`

#### Scenario: Flag off → webhook ignores subscription events

- GIVEN `subscription-recurring-enabled=false` and a verified webhook with `event_type=recurring_charge.successful`
- WHEN the webhook handler dispatches
- THEN the subscription branch is skipped, no ledger row is inserted, and the response is HTTP 200 `{ received: true, ignored: "subscription-disabled" }`

#### Scenario: Flag on → all R1-R8 active

- GIVEN `subscription-recurring-enabled=true`
- WHEN any subscription flow runs
- THEN R1-R8 behaviors execute as specified

### R10: Privacy policy update (v3)

The system MUST add a section to `GET /api/v1/privacy-policy` disclosing subscription data handling, payment source tokenization, ARCO cascade, and the no-refund-on-cancel policy. (Art. IV, Art. IX FR-053)

#### Scenario: Privacy policy mentions subscriptions

- GIVEN the privacy-policy endpoint
- WHEN called
- THEN the response includes: "Subscription status and period dates are stored server-side. Payment sources are tokenized Wompi-side and never touch our servers. ARCO delete cascade-removes subscription rows. Cancellation is non-refundable for the current period."

## API contracts

| Method | Path | Auth | Rate limit | Returns |
|---|---|---|---|---|
| `POST` | `/api/v1/subscriptions` | JWT | `"subscription"` 10/min/IP | 201 / 401 / 409 / 503 |
| `GET` | `/api/v1/subscriptions/me` | JWT | (none new) | 200 / 401 / 404 |
| `DELETE` | `/api/v1/subscriptions/me` | JWT | `"subscription-cancel"` 5/h/IP | 200 / 401 / 404 |
| `POST` | `/api/v1/payments/webhook` | HMAC | (existing `"subscription-webhook"` 60/min/IP) | 200 `{ received: true }` |

### `POST /api/v1/subscriptions`

- **Body**: `{ plan: "starter" \| "standard", paymentSourceId: string }`
- **201**: `{ id, plan, status: "active", currentPeriodStart, currentPeriodEnd }`
- **409**: `{ error: "SUBSCRIPTION/ALREADY_ACTIVE" }`
- **503**: `{ error: "SUBSCRIPTION/DISABLED" }`

### `GET /api/v1/subscriptions/me`

- **200**: `{ id, plan, status, currentPeriodStart, currentPeriodEnd, nextChargeAt, canceledAt? }`
- **404**: `{ error: "SUBSCRIPTION/NOT_FOUND" }`

### `DELETE /api/v1/subscriptions/me`

- **200**: `{ status: "canceled", accessUntil: currentPeriodEnd }`

### Webhook (extends 012-wompi handler)

- **Endpoint**: `POST /api/v1/payments/webhook` (existing)
- **New events**: `recurring_charge.successful`, `recurring_charge.failed`
- **Branch logic**: `event_type` dispatches to `HandleOneTimePaymentHandler` (012/013 path) or `HandleRecurringChargeHandler` (new path)
- **HMAC verification**: unchanged from 012

## Application ports

### `ISubscriptionService` (Application)

```csharp
public interface ISubscriptionService
{
    Task<Result<Subscription>> SubscribeAsync(Guid userId, SubscriptionPlan plan, string paymentSourceId, CancellationToken ct);
    Task<Result<Subscription>> GetAsync(Guid userId, CancellationToken ct);
    Task<Result<Subscription>> CancelAsync(Guid userId, CancellationToken ct);
    Task<Result> HandleRecurringChargeSuccessAsync(string paymentSourceId, DateTime chargedAt, string chargeId, CancellationToken ct);
    Task<Result> HandleRecurringChargeFailureAsync(string paymentSourceId, DateTime attemptedAt, string reason, CancellationToken ct);
    Task<Result<int>> ProcessRetriesAsync(CancellationToken ct);
}
```

### `ISubscriptionStore` (Application)

```csharp
public interface ISubscriptionStore
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct);
    Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct);
    Task UpsertAsync(Subscription subscription, CancellationToken ct);
    Task<IReadOnlyList<Subscription>> GetDueForRetryAsync(DateTime now, int limit, CancellationToken ct);
}
```

### `ISubscriptionProvider` (Application — Wompi adapter)

```csharp
public interface ISubscriptionProvider
{
    Task<string> CreatePaymentSourceAsync(string wompiToken, CancellationToken ct);
    Task<string> ScheduleRecurringChargeAsync(string paymentSourceId, decimal amount, string currency, CancellationToken ct);
    Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct);
    bool VerifyWebhookSignature(string payload, string signature);
}
```

## Frontend integration

| Layer | Change |
|---|---|
| Page | `BuildCv-web/app/(dashboard)/subscriptions/page.tsx` — current plan + status + next charge + history |
| Component | `components/subscriptions/subscription-card.tsx` — display current plan with status banner |
| Component | `components/subscriptions/plan-selector.tsx` — 2 plan cards (Starter/Standard) with credits + price |
| Component | `components/subscriptions/cancel-dialog.tsx` — confirmation with honest "sin reembolso" copy |
| Component | `components/wompi/wompi-subscription-widget.tsx` — tokenizes card for `payment_source` (no raw PAN to server) |
| BFF | `app/api/subscriptions/route.ts` (POST + GET), `app/api/subscriptions/cancel/route.ts` (DELETE) |
| Copy (`es.ts`) | "Suscripción activa", "Se renueva automáticamente cada mes", "30 créditos por $30.000 COP", "100 créditos por $80.000 COP (33% más barato que comprar 2 packs Standard)", "Sin reembolso al cancelar", "Suscripción cancelada — acceso hasta {fecha}" |

## Compliance

| Article | How 016 complies |
|---|---|
| **I (Cero invención)** | N/A — recurring billing is infrastructure; adapt pipeline untouched. |
| **II (Determinismo)** | N/A — score engine untouched. Period arithmetic is `now + TimeSpan.FromDays(30)` (deterministic). Wompi API responses are not used in scoring. |
| **III (Privacidad primero)** | ✅ Payment source tokenized on Wompi's side; `subscriptions.payment_source_id` is a Wompi token, not a PAN. Logs use `subscriptionId, userId, planId, status, traceId` — same pattern as 012/013. No CV, no job content. |
| **IV (Encuadre honesto)** | ✅ Copy: "Se renueva automáticamente cada mes" + "Sin reembolso al cancelar". Real prices shown. **NEVER** "créditos ilimitados" or "ahorra tiempo". Cancellation is one click. |
| **V (Entrada como dato)** | N/A — Wompi webhook is HMAC-verified structured data, treated as DATO. |
| **VI (Clean Architecture)** | ✅ Domain pure (0 packages). `ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider` ports in Application. `EfSubscriptionStore`, `WompiRecurringAdapter`, `DisabledSubscriptionProvider`, `SubscriptionReconciliationWorker` in Infrastructure. `SubscriptionEndpoints` in Api. `Result<T>` → RFC 9457. |
| **VII (Rate limits)** | ✅ New `"subscription"` policy 10/min/IP (POST), `"subscription-cancel"` 5/h/IP (DELETE), `"subscription-webhook"` 60/min/IP (extends existing webhook limit). Existing `score`/`ai`/`export`/`import`/`admin` unchanged. |
| **VIII (TDD)** | ✅ Red→green→refactor on every handler + adapter + state transition. State machine tested exhaustively. Idempotency, race, and cascade branches have explicit tests. |
| **IX (Habeas Data)** | ✅ Access (R4). Rectification via cancel + re-subscribe. Cancellation via ARCO cascade (R8) — `subscriptions` rows cascade-deleted; `payments` + `invoices` preserved per 011-factus DIAN legal hold. Consent unchanged (authenticated action). Server-side confirmation (R3) — webhook is source of truth; widget events advisory. Privacy policy updated (R10). |

## Acceptance criteria

- [ ] All 10 R's pass with green tests
- [ ] All 6 gates pass: `dotnet build`, `dotnet format`, `dotnet test`, `pnpm lint`, `pnpm build`, `pnpm test`, `preflight.sh`, `constitution-check.sh`
- [ ] Test counts: +45 (20 unit + 15 integration + 10 e2e); baseline 732/732 + 745/745 must not regress
- [ ] 012-wompi webhook handler extended (no regression on one-time path)
- [ ] 013-credit-consumption `AccreditPurchaseHandler` reused unchanged (no new ledger logic)
- [ ] 015-feature-flags `IFeatureFlag` port reused (default `subscription-recurring-enabled=false`)
- [ ] Zero suppressions, zero `mocks falsos` (real `InMemorySubscriptionStore` + Testcontainers PostgreSQL)
- [ ] ARCO anonymize cascade preserves `payments` + `invoices` (verified by integration test)

## Out of scope (deferred)

- More than 2 plans (v1.5: Pro tier)
- Annual plans (v1.5)
- Free trials (v1.5)
- Promotional pricing / discount codes (v1.5)
- Proration on plan change (v1.5)
- Family / shared plans (out of scope)
- Subscription pause (v1.5)
- Email notifications for failed charges (deferred until SMTP integration)
- Customer-initiated refunds (no refund endpoint; current period non-refundable per Art. IV)

## Next

`sdd-design` → ports (`ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider`), EF migration SQL (`AddSubscriptions`), `WebhookRouter` refactor of `HandleWebhookHandler`, retry state machine implementation, Wompi scheduled charge integration details, frontend widget tokenization flow, `SubscriptionReconciliationWorker` cron logic.