# Design: 012-wompi — Wompi Payment Gateway Integration

## Technical Approach

Widget Checkout Web pattern (same as 011-factus). Backend creates checkout session server-side, returns widget parameters to frontend. Wompi widget renders in iframe. Webhook confirms payment server-side — browser events are advisory only (Art. IX FR-049). Payment→invoice flow is event-driven, decoupled from 011-factus via `IInvoiceProvider`.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|-------------|-----------|
| **Checkout flow** | Widget Checkout Web | Direct API, Redirect | Same-origin BFF pattern; widget handles card input in Wompi's PCI-compliant iframe |
| **Payment truth source** | Webhook + GET /v1/transactions + background reconciliation worker | Browser events only | Constitution Art. IX FR-046/048/049: server-side confirmation mandatory; worker handles webhook delivery failures |
| **Idempotency** | Unique index on `idempotency_key` + `wompi_transaction_id` | Application-level dedup | Database constraint is bulletproof; application check for UX (return existing session) |
| **Invoice trigger** | Inline in webhook handler (same DbContext transaction) | MediatR/domain events | Simpler for v1; decouples later if needed |
| **Feature gating** | `Wompi:Enabled` config bool, checked at endpoint registration | Middleware, attribute | Follows 011-factus pattern; endpoints not mapped when disabled |
| **HMAC verification** | `System.Security.Cryptography.HMACSHA256` | External library | Zero dependencies in Domain (Art. VI); infrastructure handles crypto |
| **Reconciliation** | `IHostedService` polling every 60s for Pending > 5min | Manual operator review | Automatic recovery from webhook delivery failures; no human-in-the-loop |
| **EF update pattern** | `EntityEntry.CurrentValues.SetValues()` with rowversion concurrency | Detach-then-Update | Avoids marking all properties as Modified; optimistic concurrency |

## Data Flow

```
Browser ──POST /api/payments/checkout──> BFF ──proxy──> API
  │                                        │
  │  ┌─ CreateCheckoutHandler ─────────────┤
  │  │  1. Validate package                │
  │  │  2. Check idempotency key           │
  │  │  3. IPaymentProvider.CreateCheckout │
  │  │  4. IPaymentStore.AddAsync          │
  │  │  5. Return CheckoutSession          │
  │  └─────────────────────────────────────┘
  │
  ◄── { sessionId, publicKey, amountInCents, currency, reference }
  │
  ▼ Widget iframe (Wompi)
  │
  ▼ User pays in widget
  │
Wompi ──POST /api/payments/webhook──> API (no BFF — direct to backend)
  │
  ├─ 1. Verify HMAC SHA256 signature
  ├─ 2. Find payment by wompiTransactionId
  ├─ 3. Update status (idempotent)
  ├─ 4. If Approved → credit user + create invoice via IInvoiceProvider
  └─ 5. Return 200

[Background, every 60s]
PaymentReconciliationWorker (IHostedService)
  │
  ├─ 1. Find all Pending payments > 5 minutes old
  ├─ 2. For each: call IPaymentProvider.GetTransactionStatusAsync
  ├─ 3. Update status based on Wompi's response (idempotent)
  └─ 4. Log reconciliation activity (no payload)
```

## Domain Model

```
src/BuildCv.Domain/Payments/
├── Payment.cs              (sealed record — entity)
├── PaymentStatus.cs        (enum: Pending, Approved, Failed, Error)
└── CreditPackage.cs        (sealed record — value object, static catalog)
```

**Payment entity**: `Id`, `UserId`, `PackageId`, `Credits`, `AmountInCents`, `Currency`, `Status`, `WompiTransactionId?`, `WompiPaymentLink?`, `ProviderSessionId?`, `IdempotencyKey`, `CreatedAt`, `UpdatedAt`, `PaidAt?`

> **Note (PR1 deviation)**: `ProviderSessionId` was added beyond the original 13-column spec to store Wompi's session reference returned from the checkout API. This is required so that idempotent duplicate checkout requests can return the **same** `CheckoutSession` (same `SessionId`, same reference) without calling the provider again. Additive, non-breaking.

**CreditPackage** (static catalog, not persisted):
```csharp
public sealed record CreditPackage(string Id, int Credits, long PriceInCents, string Currency = "COP")
{
    public static readonly CreditPackage Starter = new("starter", 10, 1_500_000);
    public static readonly CreditPackage Standard = new("standard", 50, 6_000_000);
    public static readonly CreditPackage Pro = new("pro", 100, 10_000_000);
    public static readonly IReadOnlyList<CreditPackage> All = [Starter, Standard, Pro];
}
```

## Application Ports

```
src/BuildCv.Application/Features/Payments/
├── IPaymentProvider.cs     (port — Wompi API interaction)
├── IPaymentStore.cs        (port — persistence)
├── CreateCheckoutHandler.cs
├── HandleWebhookHandler.cs
├── GetPaymentHandler.cs
└── ListPaymentsHandler.cs
```

**IPaymentProvider** — method signatures:
```csharp
Task<CheckoutSession> CreateCheckoutAsync(string userId, CreditPackage package, string idempotencyKey, CancellationToken ct);
Task<TransactionStatus?> GetTransactionStatusAsync(string wompiTransactionId, CancellationToken ct);
bool VerifyWebhookSignature(string payload, string signatureHeader);
```

**IPaymentStore** — method signatures:
```csharp
Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct);
Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct);
Task<Payment?> GetByWompiTransactionIdAsync(string wompiTransactionId, CancellationToken ct);
Task<IReadOnlyList<Payment>> ListByUserIdAsync(string userId, int page, int perPage, CancellationToken ct);
Task AddAsync(Payment payment, CancellationToken ct);
Task UpdateAsync(Payment payment, CancellationToken ct);
```

## Infrastructure Adapters

```
src/BuildCv.Infrastructure/Payments/
├── WompiAdapter.cs         (IPaymentProvider → Wompi REST API)
├── WompiSettings.cs        (PublicKey, PrivateKey, WebhookSecret, Environment, Enabled)
├── EfPaymentStore.cs       (IPaymentStore → EF Core/Postgres)
└── InMemoryPaymentStore.cs (IPaymentStore → testing)
```

**WompiAdapter HTTP calls**:
- `POST {baseUrl}/v1/merchants/{publicKey}/payment_links` — create checkout session
- `GET {baseUrl}/v1/transactions/{id}` — verify transaction (Bearer: PrivateKey)
- `HMACSHA256(WebhookSecret, rawBody)` — verify webhook signature

**WompiSettings**: `Enabled`, `Environment` ("sandbox"|"production"), `PublicKey`, `PrivateKey`, `WebhookSecret`. Base URL derived from environment: sandbox → `https://api.wompi.sandbox`, production → `https://api.wompi.co`.

## API Endpoints

```
src/BuildCv.Api/Endpoints/PaymentEndpoints.cs
```

| Method | Path | Auth | Handler | Response |
|--------|------|------|---------|----------|
| `POST` | `/api/v1/payments/checkout` | Yes | `CreateCheckoutHandler` | `{ sessionId, publicKey, amountInCents, currency, reference }` |
| `POST` | `/api/v1/payments/webhook` | No (HMAC) | `HandleWebhookHandler` | `200 OK` |
| `GET` | `/api/v1/payments/{id}` | Yes | `GetPaymentHandler` | Payment DTO |
| `GET` | `/api/v1/payments` | Yes | `ListPaymentsHandler` | Paginated payments |

## Database Schema

**Table `payments`** (EF Core migration):

| Column | PG Type | Constraint |
|--------|---------|------------|
| `id` | `uuid` | PK |
| `user_id` | `uuid` | FK → `users.id`, ON DELETE RESTRICT |
| `package_id` | `varchar(20)` | NOT NULL |
| `credits` | `integer` | CHECK > 0 |
| `amount_in_cents` | `bigint` | CHECK > 0 |
| `currency` | `char(3)` | DEFAULT 'COP' |
| `status` | `text` | HasConversion; CHECK IN ('Pending','Approved','Failed','Error') |
| `wompi_transaction_id` | `varchar(100)` | UNIQUE, nullable |
| `wompi_payment_link` | `varchar(500)` | nullable |
| `provider_session_id` | `varchar(200)` | nullable |
| `idempotency_key` | `varchar(100)` | UNIQUE |
| `created_at` | `timestamptz` | DEFAULT now() |
| `updated_at` | `timestamptz` | |
| `paid_at` | `timestamptz` | nullable |

**Indexes**: `UX_payments_idempotency_key` (unique), `UX_payments_wompi_transaction_id` (unique), `IX_payments_user_id_created_at` (`user_id`, `created_at DESC`).

## DI Registration

In `Infrastructure/DependencyInjection.cs`:
```csharp
services.Configure<WompiSettings>(configuration.GetSection("Wompi"));
services.AddSingleton<IPaymentStore, InMemoryPaymentStore>(); // or EfPaymentStore

var wompiEnabled = configuration.GetValue<bool>("Wompi:Enabled");
if (wompiEnabled)
    services.AddHttpClient<IPaymentProvider, WompiAdapter>();
else
    services.AddSingleton<IPaymentProvider, DisabledPaymentProvider>();
```

In `Api/Program.cs`, endpoints conditionally mapped:
```csharp
if (configuration.GetValue<bool>("Wompi:Enabled"))
    app.MapPaymentEndpoints();
```

## Feature Flag Configuration

```json
{
  "Wompi": {
    "Enabled": false,
    "Environment": "sandbox",
    "PublicKey": "",
    "PrivateKey": "",
    "WebhookSecret": ""
  }
}
```

## Error Handling

| Condition | HTTP | Action |
|-----------|------|--------|
| Wompi disabled | 404 | Endpoints not mapped |
| Unauthenticated | 401 | ASP.NET auth middleware |
| Invalid package | 400 | `{ error: "INVALID_PACKAGE" }` |
| Idempotent duplicate | 200 | Return existing session |
| Webhook HMAC invalid | 401 | Log attempt, reject |
| Wompi API timeout/error | 502 | `{ error: "PROVIDER_UNAVAILABLE" }` |

All errors use `Result<T>` pattern from Domain, mapped to ProblemDetails at endpoint layer.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| **Unit** | Handlers (CreateCheckout, HandleWebhook, GetPayment, ListPayments) | Mock IPaymentProvider + IPaymentStore; verify idempotency, status transitions, invoice trigger |
| **Unit** | WompiAdapter.VerifyWebhookSignature | Known HMAC payloads from Wompi docs |
| **Unit** | CreditPackage catalog | Static values match spec |
| **Integration** | Full checkout→webhook→credit flow | WebApplicationFactory + InMemoryPaymentStore + mock WompiAdapter |
| **Integration** | API endpoints (auth, status codes, contracts) | WebApplicationFactory with test JWT |
| **E2E** | Widget renders, checkout completes | Playwright against sandbox (manual checklist for v0.5) |

**Coverage target**: ≥90% on handlers and WompiAdapter (Art. VIII).

## Migration / Rollout

1. EF Core migration: additive only (new `payments` table)
2. Feature flag `Wompi:Enabled=false` in production until sandbox testing passes
3. Deploy with flag off, enable after Wompi sandbox credentials verified
4. No data migration required

## Open Questions

- [ ] Wompi sandbox credentials availability for integration tests
- [ ] Webhook retry policy: does Wompi retry on 5xx? (affects idempotency design)
- [ ] Credit acreditation: immediate on webhookApproved, or async queue for v1?
