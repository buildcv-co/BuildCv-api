# Spec: 013-credit-consumption

## Status

[Spec] — ✅ SHIPPED (R1–R10 implemented and green; PR3 closes the v1 monetization loop)
## Overview
Close the v1 monetization loop by crediting authenticated users on verified Wompi payments and debiting one credit per `POST /api/v1/adapt`. The ledger is append-only with idempotent grants; insufficient balance returns HTTP 402 with a "buy more" CTA; LLM failures pre-first-token auto-refund. Frontend surfaces live balance, low-credit warnings, and 402 modals — all without leaking PII or bypassing server-side confirmation (Art. III, Art. IX).

## Domain model

### User (modified)
- New field: `CreditBalance: int` (denormalized, `CHECK (credit_balance >= 0)`, default `0`, `xmin` concurrency)

### CreditLedgerEntry (new, append-only)
| Field | Type | Constraint |
|---|---|---|
| `Id` | `Guid` (UUIDv7) | PK |
| `UserId` | `Guid` | FK → `users.id`, `ON DELETE CASCADE` |
| `Reason` | `CreditReason` enum | `HasConversion<string>` + `CHECK` |
| `Reference` | `string?` (≤ 80) | idempotency key |
| `Delta` | `int` | `CHECK (Delta <> 0)` |
| `BalanceAfter` | `int` | audit snapshot |
| `Description` | `string?` (≤ 200) | operator text, no PII |
| `CreatedAt` | `DateTime` (UTC) | `timestamptz`, `DEFAULT now()` |

### Constraints / Indexes
- `UNIQUE(UserId, Reason, Reference)` — idempotency (R1)
- `INDEX(UserId, CreatedAt DESC)` — history (R4)
- `CHECK (Delta <> 0)`, `CHECK (BalanceAfter >= 0)`

### CreditReason enum
`Purchase = 1` · `Gift = 2` · `Consumption = 3` · `Refund = 4` · `Adjustment = 5`

## Requirements

### R1: Credit ledger (append-only, idempotent)
The system MUST persist every credit movement in `credit_ledger_entries` (append-only) and grant credits only via the ledger, never by mutating `users.credit_balance` directly outside the same transaction. (Art. VI, Art. IX)

#### Scenario: Grant credits on approved payment
- GIVEN an `Approved` payment with 10 credits
- WHEN `HandleWebhookHandler` processes the verified webhook
- THEN the ledger gets one `Purchase` row (`Reference = payment.Id`) and `users.credit_balance` increases by 10 in the same transaction

#### Scenario: Duplicate webhook is idempotent
- GIVEN an `Approved` payment already credited
- WHEN the same webhook arrives again
- THEN the unique `(UserId, Reason, Reference)` violation maps to a no-op success and the balance is unchanged

### R2: Credit consumption gates `adapt` (HTTP 402)
The system MUST consume exactly 1 credit per `POST /api/v1/adapt`, require JWT authentication, and return HTTP 402 (RFC 9457) when balance is insufficient. The gate is orthogonal to the existing `ai` 5/h IP rate limit. (Art. IV, Art. VII, Art. IX)

#### Scenario: Authenticated user with balance ≥ 1 adapts
- GIVEN a user with `credit_balance = 5`
- WHEN `POST /api/v1/adapt` is called with a valid JWT
- THEN the system debits 1 credit, runs adapt, returns 200, and the ledger gains one `Consumption` row

#### Scenario: Balance = 0 returns 402
- GIVEN a user with `credit_balance = 0`
- WHEN `POST /api/v1/adapt` is called
- THEN the system returns 402 with `code: "INSUFFICIENT_CREDITS"` and `Retry-After: 0`, and the ledger has no new row

#### Scenario: Anonymous request rejected
- GIVEN no JWT
- WHEN `POST /api/v1/adapt` is called
- THEN the system returns 401 (auth required) — credit check is skipped

### R3: Refund on LLM failure (pre-first-token)
The system MUST post a compensating `Refund` ledger entry if the LLM call fails before the first token is emitted. After the first token, no refund is issued (user received partial value). (Art. IV)

#### Scenario: LLM timeout before first token
- GIVEN a credit was debited for `adaptationId = X`
- WHEN the LLM call throws / times out before streaming any token
- THEN the ledger gains one `Refund` row (`Reference = adaptationId:X`) and `credit_balance` returns to the pre-consume value

#### Scenario: Failure after first token
- GIVEN a credit was debited and the LLM streamed at least one token
- WHEN the LLM call then fails mid-stream
- THEN no refund is issued and the partial adaptation is persisted

### R4: Credit history (paginated)
The system MUST return the authenticated user's ledger history, newest first, paginated by `page` (1-based) and `perPage` (≤ 50). (Art. IX — access)

#### Scenario: First page with default size
- GIVEN a user with 30 ledger entries
- WHEN `GET /api/v1/credits/history` is called
- THEN the response returns the 20 newest entries and a `hasMore: true` flag

#### Scenario: Other users cannot read this history
- GIVEN user A authenticated
- WHEN A calls `GET /api/v1/credits/history?userId=B`
- THEN the system returns A's history only (no cross-user access)

### R5: Credit balance (with last update)
The system MUST expose `GET /api/v1/credits/balance` returning `{ balance: int, lastUpdatedAt: DateTime }` for the authenticated user. (Art. IV — honest framing)

#### Scenario: Balance reflects current state
- GIVEN a user with `credit_balance = 7`
- WHEN `GET /api/v1/credits/balance` is called
- THEN the response is `{ balance: 7, lastUpdatedAt: <recent UTC> }`

### R6: Welcome grant on signup (3 credits, idempotent)
The system MUST post a 3-credit `Gift` entry on first OAuth signup with idempotency key `welcome:{userId}`. Replays return no-op success. (Art. IV — encuadre honesto: "3 adaptaciones gratis")

#### Scenario: First signup grants 3 credits
- GIVEN a brand-new OAuth signup creates user U
- WHEN `HandleOAuthCallbackHandler` completes
- THEN the ledger gains one `Gift` row (`Reference = "welcome:U"`, `Delta = +3`) and `credit_balance = 3`

#### Scenario: Replayed callback does not double-grant
- GIVEN user U already received the welcome grant
- WHEN the same callback is replayed (retried)
- THEN no second `Gift` row is inserted (unique `(UserId, Reason, Reference)` blocks it)

### R7: ARCO anonymize (Habeas Data)
The system MUST, on ARCO delete, anonymize the user row (`email = "[deleted]@anonymized"`, `name = "[Deleted User]"`, `provider_id = "redacted"`), cascade-delete `credit_ledger_entries`, and KEEP `payments` + `invoices` (DIAN legal hold). (Art. IX FR-052, 011-factus)

#### Scenario: User with no payments deletes their data
- GIVEN user U with ledger rows only
- WHEN `DELETE /api/v1/user/data` is called by U
- THEN all `credit_ledger_entries` for U are deleted and U is hard-deleted

#### Scenario: User with paid invoices deletes their data
- GIVEN user U with at least one `payments` row tied to a Factus invoice
- WHEN `DELETE /api/v1/user/data` is called by U
- THEN U's `email`/`name`/`provider_id` are anonymized, `credit_ledger_entries` cascade-delete, `payments` and `invoices` remain intact, and a `data_treatment_log` row records the anonymization

### R8: Webhook integration (Wompi + reconciliation)
The system MUST credit the user inside the same transaction as invoice creation on verified webhook, and additionally credit on `PaymentReconciliationService` if the original webhook failed. The `Credits:Enabled` feature flag (default `false` in production) MUST gate both paths. (Art. IX FR-046/048/049)

#### Scenario: Approved webhook credits and invoices atomically
- GIVEN a verified Approved webhook for payment P
- WHEN `HandleWebhookHandler.HandleAsync` runs
- THEN the same `await` boundary executes `(invoice, ledger entry, balance update)` — either all three commit or all three roll back

#### Scenario: Reconciliation heals a missed webhook
- GIVEN a `Pending` payment that timed out before webhook delivery
- WHEN `PaymentReconciliationService` transitions it to `Approved`
- THEN the same credit grant runs, the ledger gains a `Purchase` row, and `credit_balance` increases

#### Scenario: Feature flag off
- GIVEN `Credits:Enabled = false`
- WHEN any webhook or reconciliation fires
- THEN no ledger row is inserted and `credit_balance` is unchanged

### R9: 402 endpoint filter
The system MUST implement `RequireCredits(amount)` as a Minimal API endpoint filter that runs after `RequireAuthorization`, maps `Result.Failure("CREDIT/INSUFFICIENT")` to HTTP 402 with RFC 9457 ProblemDetails, and is applied to `POST /api/v1/adapt` only. (Art. VI, Art. VII)

#### Scenario: Filter runs after auth
- GIVEN an unauthenticated request to `/adapt`
- WHEN the pipeline executes
- THEN `RequireAuthorization` short-circuits with 401 before `RequireCredits(1)` is evaluated

#### Scenario: Filter writes RFC 9457 body
- GIVEN a 402 condition
- WHEN the response is returned
- THEN the body includes `type`, `title`, `status: 402`, `code: "INSUFFICIENT_CREDITS"`, and `Retry-After: 0` header

### R10: Privacy disclosure
The system MUST add one line to the existing `GET /api/v1/privacy-policy` response disclosing credit-balance tracking and ARCO cascade semantics. (Art. IX FR-053)

#### Scenario: Privacy policy mentions credit ledger
- GIVEN the privacy-policy endpoint
- WHEN called
- THEN the response includes "credit balance tracked per Art. IX; ARCO delete anonymizes the user and cascades the ledger; payments and DIAN invoices are preserved"

## API contracts

| Method | Path | Auth | Returns |
|---|---|---|---|
| `GET` | `/api/v1/credits/balance` | JWT | `{ balance, lastUpdatedAt }` |
| `GET` | `/api/v1/credits/history?page&perPage` | JWT | `{ items: [...], hasMore: bool, page, perPage }` |
| `GET` | `/api/v1/credits/health` | JWT + Admin | `{ sumDelta, usersBalance, drift }` |
| `POST` | `/api/v1/credits/gift` | JWT + Admin | `{ ledgerEntryId, newBalance }` |

### Modified endpoints
- `POST /api/v1/adapt` — gains `.RequireAuthorization()` + `.RequireCredits(1)`; 401 if anon, 402 if balance = 0; rate-limit `"ai"` 5/h by IP unchanged (Art. VII).
- `POST /api/v1/payments/webhook` — gains ledger grant inside the same DB transaction as the invoice creation.
- `POST /api/v1/auth/{provider}/callback` — gains welcome grant on first signup.
- `DELETE /api/v1/user/data` — gains ARCO anonymize + cascade branch.

## Frontend integration

| Layer | Change |
|---|---|
| BFF | `app/api/credits/balance/route.ts` + `app/api/credits/history/route.ts` (GET, cookie-passthrough proxy) |
| API client | `lib/api/credits.ts` — `fetchBalance()`, `fetchHistory({ page, perPage })`, `CreditError` class |
| API client | `lib/api/adapt.ts` — new 402 branch → `AdaptError({ code: 'INSUFFICIENT_CREDITS', kind: 'payment_required' })` |
| Component | `components/layout/credit-badge.tsx` — "N créditos", `aria-live="polite"`, color states (green ≥ 5 / yellow 1-4 / red 0) |
| Component | `components/layout/low-credit-banner.tsx` — dismissible, threshold ≤ 2 (`NEXT_PUBLIC_LOW_CREDIT_THRESHOLD`) |
| Component | `components/wompi/WompiWidget.tsx` — `onPaymentApproved` callback calls `fetchBalance()` optimistically; 30s "procesando pago" toast if still 0 |
| Page | `app/analizar/page.tsx` — "Adaptar" disabled when balance = 0, tooltip + link to `/pricing` |
| Copy | `lib/copy/es.ts` — "Te quedan {N} créditos", "Créditos insuficientes", "Comprar más créditos", "1 crédito = 1 adaptación" |

## Compliance

| Article | How 013 complies |
|---|---|
| **I (Cero invención)** | N/A — credit math is infrastructure, not content. |
| **II (Determinismo)** | N/A — score engine untouched. Credit arithmetic is integer math (deterministic by definition). |
| **III (Privacidad)** | Ledger stores metadata only (no CV content, no job content, no PII beyond `UserId`). Logs use `userId, amount, reason, reference, traceId` — same pattern as 012-wompi. |
| **IV (Encuadre honesto)** | Copy: "1 crédito = 1 adaptación" or "3 adaptaciones gratis". Pricing shows real price / real credit count. **NEVER** "ilimitado" or "garantiza entrevista". |
| **V (Entrada como dato)** | N/A — ledger is downstream of validated input. |
| **VI (Clean Architecture)** | Domain pure (0 packages). `ICreditLedger` + `ICreditConsumptionService` in Application; `EfCreditLedger` in Infrastructure; `CreditEndpoints` in Api. `Result<T>` → RFC 9457 `ProblemDetails`. |
| **VII (Rate limits)** | `score`/`export`/`import` unchanged. `ai` 5/h by IP unchanged. New `credit` business limit is **layered**, not a 5th policy → no amendment. |
| **VIII (TDD)** | Tests written red before implementation for all 5 handlers + `EfCreditLedger` + 4 endpoints + 402 filter + ARCO branch. Golden cases: balance invariant, race, idempotency, refund, anonymize. |
| **IX (Habeas Data)** | Access via R4. Rectification via `Adjustment` entries (operator). Cancellation via R7 (anonymize + cascade, keep payments). Consent unchanged. Server-side confirmation: webhook is source of truth; widget events advisory. Privacy policy updated via R10. |

## Acceptance criteria
- [ ] All 10 R's pass with green tests
- [ ] All 6 gates pass: `dotnet build`, `dotnet format`, `dotnet test`, `pnpm lint`, `pnpm build`, `pnpm test`, `preflight.sh`, `constitution-check.sh`
- [ ] Test counts: +60 API, +30 Web (baseline 451 + 718 = 1169 must not regress)
- [ ] Zero suppressions, zero `mocks falsos` (real `InMemoryCreditLedger` + Testcontainers PostgreSQL)
- [ ] All 9 Constitution articles cited in `archive-report.md`
- [ ] Feature flag `Credits:Enabled` defaults to `false` in production until 013 ships end-to-end

## Out of scope
- Subscriptions / recurring billing
- User-requested refunds (only LLM failure pre-first-token)
- Multi-currency (COP only, matches 012 pricing)
- User-to-user gifting (admin gift endpoint only)
- Credit expiration (decision: never)
- Migration of existing 012-wompi credits (none exist)

## Next
`sdd-design` → ports (`ICreditLedger`, `ICreditConsumptionService`), EF migration SQL, `RequireCredits` endpoint filter implementation, frontend component contracts, ARCO anonymize flow.
