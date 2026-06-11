# 012-wompi — Wompi Payment Gateway Integration

## Overview

Integrate Wompi (Colombian payment gateway) for credit purchases via Widget Checkout Web. Backend creates checkout sessions, frontend renders the Wompi widget, and a server-side webhook confirms payment. Credits are acredited only after verified payment. Auto-creates Factus invoice on `Approved` status.

## Requirements

### R1: Credit Packages

The system MUST offer fixed credit tiers in COP only.

| Package | Credits | Price (COP) | Price in Cents |
|---------|---------|-------------|----------------|
| Starter | 10 | $15,000 | 1,500,000 |
| Standard | 50 | $60,000 | 6,000,000 |
| Pro | 100 | $100,000 | 10,000,000 |

#### Scenario: User selects a credit package

- GIVEN an authenticated user on the pricing page
- WHEN the user selects a package (e.g., "Pro — 100 credits")
- THEN the system creates a checkout session and returns widget configuration

### R2: Create Checkout Session

The system MUST create a Wompi checkout session server-side and return widget parameters to the frontend. The session MUST include an idempotency key derived from `userId + packageId`.

#### Scenario: Checkout session created successfully

- GIVEN an authenticated user with a valid package selection
- WHEN POST /api/payments/checkout is called
- THEN the system returns `{ sessionId, publicKey, amountInCents, currency: "COP", reference }`

#### Scenario: Duplicate checkout request (idempotent)

- GIVEN the same user submits the same package within 5 minutes
- WHEN POST /api/payments/checkout is called
- THEN the system returns the existing session without creating a new one

### R3: Webhook Verification

The system MUST verify Wompi webhooks server-side using HMAC SHA256 signature. The webhook MUST be the source of truth for payment status — browser widget events are advisory only.

#### Scenario: Valid webhook — Approved

- GIVEN a payment in `Pending` status
- WHEN a Wompi webhook arrives with status `APPROVED` and valid HMAC signature
- THEN the system updates payment status to `Approved` and credits the user

#### Scenario: Tampered webhook rejected

- GIVEN a webhook with invalid HMAC signature
- WHEN the system verifies the signature
- THEN the system returns HTTP 401 and does NOT update any state

#### Scenario: Duplicate webhook (idempotent)

- GIVEN a payment already in `Approved` status
- WHEN the same webhook arrives again
- THEN the system returns HTTP 200 without re-crediting

### R4: Server-Side Transaction Verification

The system MUST verify payment status by calling Wompi's GET /v1/transactions API. Browser redirects are never trusted as confirmation (Art. IX FR-049).

#### Scenario: Verify pending transaction

- GIVEN a payment in `Pending` status
- WHEN the system polls GET /v1/transactions/{wompiTransactionId}
- THEN the system updates status based on Wompi's response

### R5: Invoice Auto-Creation

The system MUST auto-create a Factus invoice when payment status becomes `Approved`. Invoice creation is decoupled via an internal event.

#### Scenario: Payment approved → invoice created

- GIVEN a payment transitions to `Approved`
- WHEN the internal event fires
- THEN the system calls IInvoiceProvider.CreateInvoiceAsync with payment details (amount, currency, customer info)

#### Scenario: Factus disabled → invoice in Draft

- GIVEN Factus is disabled (`Factus:Enabled=false`)
- WHEN payment is approved
- THEN the system creates a local Draft invoice without calling Factus

### R6: Environment Gating

The system MUST gate Wompi integration behind `Wompi:Enabled` feature flag. When disabled, all payment endpoints return HTTP 404 and no payment data persists.

#### Scenario: Wompi disabled

- GIVEN `Wompi:Enabled=false`
- WHEN any payment endpoint is called
- THEN the system returns HTTP 404

#### Scenario: Sandbox vs Production

- GIVEN `Wompi:Environment=sandbox`
- THEN the system uses Wompi sandbox API URLs and keys
- GIVEN `Wompi:Environment=production`
- THEN the system uses Wompi production API URLs and keys

### R7: Authenticated Users Only

The system MUST require an authenticated user (from 009-auth) for all payment operations. Anonymous payments are not supported.

#### Scenario: Unauthenticated checkout attempt

- GIVEN no valid session token
- WHEN POST /api/payments/checkout is called
- THEN the system returns HTTP 401

### R8: Payment Status Query

The system MUST expose payment status to the authenticated owner.

#### Scenario: Get payment by ID

- GIVEN an authenticated user with an existing payment
- WHEN GET /api/payments/{id} is called
- THEN the system returns the payment with status, amount, credits, and timestamps

#### Scenario: List user payments

- GIVEN an authenticated user with multiple payments
- WHEN GET /api/payments is called
- THEN the system returns paginated list of the user's payments

## Domain Model

```
Payment
  ├── Id: Guid
  ├── UserId: Guid (FK → Auth.User)
  ├── PackageId: string ("starter" | "standard" | "pro")
  ├── Credits: int
  ├── AmountInCents: long
  ├── Currency: string ("COP")
  ├── Status: PaymentStatus
  ├── WompiTransactionId: string? (unique index)
  ├── WompiPaymentLink: string?
  ├── ProviderSessionId: string?
  ├── IdempotencyKey: string (unique index)
  ├── CreatedAt: DateTime
  ├── UpdatedAt: DateTime
  └── PaidAt: DateTime?
```

> **Note (PR1 deviation)**: `ProviderSessionId` was added during PR1 implementation to store Wompi's session reference returned from the checkout API. This is required so that idempotent duplicate checkout requests can return the **same** `CheckoutSession` (same `SessionId`, same reference) without calling the provider again. Additive, non-breaking.

PaymentStatus: Pending | Approved | Failed | Error

CreditPackage (value object)
  ├── Id: string
  ├── Credits: int
  ├── PriceInCents: long
  └── Currency: string ("COP")
```

## Integration Contracts

### IPaymentProvider (Application port)

```csharp
public interface IPaymentProvider
{
    Task<CheckoutSession> CreateCheckoutAsync(string userId, CreditPackage package, string idempotencyKey, CancellationToken ct);
    Task<TransactionStatus?> GetTransactionStatusAsync(string wompiTransactionId, CancellationToken ct);
    bool VerifyWebhookSignature(string payload, string signatureHeader);
}
```

### IPaymentStore (Application port)

```csharp
public interface IPaymentStore
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct);
    Task<Payment?> GetByWompiTransactionIdAsync(string wompiTransactionId, CancellationToken ct);
    Task<IReadOnlyList<Payment>> ListByUserIdAsync(string userId, int page, int perPage, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task UpdateAsync(Payment payment, CancellationToken ct);
}
```

### CheckoutSession (returned by provider)

```csharp
public sealed record CheckoutSession
{
    public string SessionId { get; init; }
    public string PublicKey { get; init; }
    public long AmountInCents { get; init; }
    public string Currency { get; init; } = "COP";
    public string Reference { get; init; }
}
```

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | /api/payments/checkout | Yes | Create checkout session |
| POST | /api/payments/webhook | No | Wompi webhook (HMAC verified) |
| GET | /api/payments/{id} | Yes | Get payment status |
| GET | /api/payments | Yes | List user payments |

## Wompi Integration Details

- **Widget**: Frontend loads Wompi Widget Checkout Web script, renders in an iframe with session parameters
- **Webhook URL**: `POST /api/payments/webhook` — receives `{ transaction: { id, status, amount_in_cents } }`
- **Signature verification**: HMAC SHA256 with `WebhookSecret`, computed over raw request body
- **Transaction lookup**: `GET https://api.wompi.sandbox/v1/transactions/{id}` with Bearer token from `Wompi:PrivateKey`
- **Status mapping**: Wompi `APPROVED` → `Approved`, `PENDING` → `Pending`, `DECLINED`/`ERROR` → `Failed`/`Error`

## Configuration

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
| Wompi disabled | 404 | Return NotFound |
| Unauthenticated | 401 | Return Unauthorized |
| Invalid package | 400 | Return BadRequest |
| Idempotent duplicate | 200 | Return existing session |
| Webhook signature invalid | 401 | Reject, log attempt |
| Wompi API timeout | 502 | Return BadGateway, retry later |
| Wompi API error | 502 | Return BadGateway, log error |

## Testing Requirements

- **Unit tests**: IPaymentProvider mock, IPaymentStore mock, webhook signature verification, idempotency logic
- **Integration tests**: Full checkout→webhook→credit flow with Wompi sandbox
- **Coverage**: ≥90% on handlers and WompiAdapter
- **Constitution**: Zero suppressions (Art. VIII)

## Constitution Compliance

| Art. | Requirement | How Met |
|------|-------------|---------|
| **III** | Privacy in logs | Log paymentId, wompiTransactionId, status. Never card data |
| **VI** | Clean Architecture | IPaymentProvider port in Application, WompiAdapter in Infrastructure |
| **IX FR-046** | Server-side confirmation | Webhook + GET /v1/transactions verification |
| **IX FR-048** | Verify amount/status | Server-side GET, never trust browser redirect |
| **IX FR-049** | Never trust browser redirect | Widget events advisory; webhook is truth |
| **VIII** | TDD | Test-first on payment handlers |

## Risks and Mitigations

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Webhook idempotency failure | Med | Unique index on wompiTransactionId, upsert pattern |
| Signature verification precision | Med | Test against Wompi sandbox with known payloads |
| Widget bundle size | Low | Lazy-load widget component, dynamic import |
| 011-factus integration order | Low | Payment→invoice is event-driven, decoupled |
| Wompi API downtime | Med | Retry with exponential backoff, queue verification |
