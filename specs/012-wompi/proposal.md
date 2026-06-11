# Proposal: 012-wompi — Wompi Payment Gateway Integration

## Intent

Monetize BuildCv at v1 by integrating Wompi (Colombian payment gateway) for credit purchases. Constitution Art. IX FR-046/048/049 mandate server-side confirmation, idempotent verification, and never trusting browser redirects. This feature implements `IPaymentProvider` (declared in Art. VI) and the payment→invoice flow required before billing.

## Scope

### In Scope
- `Payment` domain entity + `PaymentStatus` enum + `CreditPackage` entity
- `IPaymentProvider` port (CreateCheckout, VerifyTransaction, GetTransactionStatus)
- `IPaymentStore` port (Save, Get, Update, GetByWompiTransactionId)
- `WompiAdapter` (OAuth2, GET /v1/transactions, HMAC SHA256 webhook verification)
- `WompiSettings` (PublicKey, PrivateKey, WebhookSecret, Environment)
- Feature flag `Wompi:Enabled`
- API: POST /api/payments/checkout, POST /api/payments/webhook, GET /api/payments/{id}, GET /api/payments
- Frontend: Wompi widget React component, BFF proxy routes
- Integration with 011-factus: payment confirmed → invoice created

### Out of Scope
- Subscription/recurring billing
- Refund automation (manual via Wompi dashboard)
- Multiple payment gateways (future abstraction only)
- Credit consumption logic (separate feature)

## Approach

Widget Checkout Web — same pattern as 011-factus (optional plugin). Backend creates checkout session, returns widget config to frontend. Webhook confirms payment server-side. Idempotent by `wompiTransactionId`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/BuildCv.Domain/Payments/` | New | Payment, PaymentStatus, CreditPackage entities |
| `src/BuildCv.Application/Features/Payments/` | New | IPaymentProvider, IPaymentStore ports, 4 handlers |
| `src/BuildCv.Infrastructure/Payments/` | New | WompiAdapter, WompiSettings, EfPaymentStore |
| `src/BuildCv.Api/Endpoints/` | New | PaymentEndpoints (4 routes) |
| `src/BuildCv.Api/BuildCv.Api.csproj` | Modified | Add Wompi NuGet or HTTP client |
| `BuildCv-web/` | New | Wompi widget component, BFF proxy |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Webhook idempotency failure | Med | Unique index on wompiTransactionId, upsert pattern |
| Signature verification precision | Med | Test against Wompi sandbox with known payloads |
| Widget bundle size | Low | Lazy-load widget component, dynamic import |
| 011-factus integration order | Low | Payment→invoice is event-driven, decoupled |

## Rollback Plan

Feature flag `Wompi:Enabled=false` disables all payment endpoints and webhook. No payment data persists when disabled. Database migration is additive (new tables only).

## Dependencies

- 011-factus (IInvoiceProvider for payment→invoice flow)
- Wompi sandbox credentials for testing
- Constitution Art. IX compliance audit

## Success Criteria

- [ ] Payment flow works end-to-end in Wompi sandbox
- [ ] Webhook verification rejects tampered signatures
- [ ] Idempotent: duplicate webhooks produce same result
- [ ] `Wompi:Enabled=false` completely disables payment features
- [ ] Payment→invoice integration creates Factus-ready invoices
- [ ] ≥90% test coverage on handlers and WompiAdapter
- [ ] Zero suppressions (Art. VIII / project rules)

## Constitution Compliance

| Art. | Requirement | How Met |
|------|-------------|---------|
| **VI** | Clean Architecture | IPaymentProvider port in Application, WompiAdapter in Infrastructure |
| **IX FR-046** | Server-side confirmation | Webhook + GET /v1/transactions verification |
| **IX FR-048** | Verify amount/status with Wompi | Server-side GET verification, never trust redirect |
| **IX FR-049** | Never trust browser redirect | Widget events are advisory; webhook is source of truth |
| **III** | Privacy in logs | Log paymentId, wompiTransactionId, status. Never card data |

## Proposal Question Round

1. **Credit packages**: What packages/price tiers? Fixed COP amounts or configurable?
2. **Currency**: COP only, or multi-currency from day one?
3. **User association**: Payments linked to authenticated users only, or also anonymous with email receipt?
4. **Invoice trigger**: Auto-create invoice on payment approved, or manual trigger?
5. **Sandbox vs production**: Start with sandbox-only and gate production behind env var?
