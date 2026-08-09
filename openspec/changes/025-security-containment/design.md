# Design: Fail-Closed Security Containment

## Technical Approach

Read two independent booleans from `builder.Configuration` and conditionally call the existing endpoint-mapping extensions in `Program.cs`. `GetValue<bool>` yields `false` for a missing key, so no options type, middleware, filter, or new architecture layer is justified. Because disabled extensions are never called, ASP.NET Core has no matching endpoint and performs no request binding or endpoint dependency resolution. This is containment only under Constitution Articles III, VI, VII, and IX; A1–A4 and B1–B6 remain deferred.

## Architecture Decisions

| Decision | Alternatives rejected | Rationale |
|---|---|---|
| Gate `MapIterationEndpoints()` and `MapInvoicingEndpoints()` in `Program.cs`. | Middleware, authorization policies, checks inside handlers. | Registration-time gating is the only option that prevents binding and handler/store/provider resolution while preserving native 404/405 behavior. |
| Use direct configuration reads with missing=`false`. | Options classes, feature-flag service, shared gate abstraction. | Two startup booleans do not repay another abstraction (Article VI). The gates remain independent by construction. |
| Keep committed `appsettings*.json` keys absent; document keys and set explicit `false` in `render.yaml`. | Committed development `true`; global test defaults. | Absence remains testable and fail-closed. Production is explicit without introducing secrets. |
| Keep payment composition untouched. | Disable invoicing registrations or gate `IInvoiceProvider`. | Public routing is unsafe; internal verified payment processing is required and uses the same provider independently. |

## Control Flow and Internal Seam

```text
startup config ──false/absent──> skip Map*Endpoints ──> router 404/405
       └────────true───────────> existing map extension ──> existing behavior

Wompi:Enabled ──true──> Program.cs: MapPaymentEndpoints()
                           └─> HandleWebhookHandler ──> IInvoiceProvider
Wompi:Enabled ──true──> PaymentReconciliationWorker ──> IInvoiceProvider
```

`Program.cs` currently maps payment routes separately at lines 189–192. `Infrastructure/DependencyInjection.cs` registers `IInvoiceProvider` at lines 147–154 regardless of public invoice mapping; `HandleWebhookHandler.cs:112–125` and `PaymentReconciliationService.cs:84–87` issue invoices. B0 changes none of these seams.

## Work Units and File Changes

| Unit | Files | Review boundary |
|---|---|---|
| **A0** | Modify `src/BuildCv.Api/Program.cs`, `tests/BuildCv.Api.IntegrationTests/IterationEndpointsTests.cs`, `README.md`, `render.yaml`; create `tests/BuildCv.Api.IntegrationTests/IterationEndpointsContainmentTests.cs`. | Iteration gate, RED tests, compatibility override, docs/config; forecast 180–260 lines. |
| **B0** | Modify `src/BuildCv.Api/Program.cs`, `tests/BuildCv.Api.IntegrationTests/Invoicing/InvoicingEndpointsTests.cs`, `tests/BuildCv.Api.IntegrationTests/Payments/PaymentEndpointsTests.cs`, `README.md`, `render.yaml`; create `tests/BuildCv.Api.IntegrationTests/Invoicing/InvoicingEndpointsContainmentTests.cs`. | Complete 12-route matrix, compatibility/internal-payment proof, docs/config; forecast 260–360 lines. |

Do not modify endpoint contracts, service registrations, legacy specs, or archived evidence.

## Configuration and Observability

Effective configuration priority is command line, environment variables, Development user-secrets, environment-specific JSON, then base JSON. Environment names are `Iteration__PublicApiEnabled` and `Invoicing__PublicApiEnabled`. Feature-specific test factories add in-memory values before `Program.cs` reads them: iteration compatibility enables only iteration; invoicing compatibility enables only invoicing; isolated independence tests may enable one or both. `CustomWebApplicationFactory` never enables either globally.

Production deploys both as `false` (omission is equally closed). Emit structured startup logs containing only each gate name and Boolean state, never secrets or payloads; add no custom disabled-route response. Deployment probes verify route absence and payment-webhook health.

## Testing Strategy

For each unit: first add and run RED integration tests; then add the minimal mapping condition; then run focused tests, full `dotnet test`, Release build, and format verification. Missing-key and explicit-false fixtures are distinct. Disabled tests use malformed JSON to prove binding is not reached and poison DI factories that fail if `IIterationService`, `IInvoiceStore`, or `IInvoiceProvider` is resolved. Enabled compatibility suites retain existing status/contracts. B0 also keeps `Invoicing:PublicApiEnabled` absent/false while a valid signed approved-payment webhook creates an invoice, and covers one-gate/both-gates matrices.

## Rollout, Rollback, and Risks

Merge/deploy A0 to main first; then rebase B0 onto updated main, keep its PR under 400 changed lines, and deploy. Deactivate by removing a key or setting it `false` and restarting. Code rollback is per work unit; production `true` is not a rollback strategy. Main risks are accidental global test enablement, incomplete invoicing route enumeration, and coupling the public gate to internal providers; the factory isolation, 12-route table, and webhook invoice assertion address them.

## Open Questions

None.
