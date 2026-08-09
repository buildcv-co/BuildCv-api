# Tasks: Fail-Closed Security Containment

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | A0: 180–260; B0: 260–360; combined: 440–620 |
| 400-line budget risk | High combined; each slice below 400 |
| Chained PRs recommended | Yes |
| Suggested split | A0 → main → B0 |
| Delivery strategy | force-chained |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Boundary |
|---|---|---|---|
| A0 | Contain iteration routes | PR 1 → main | Independently deployable |
| B0 | Contain public invoicing | PR 2 → main | Starts after A0; preserves internal invoicing |

## Work Unit A0 — Iteration API Containment

### RED

- [x] 1.1 Create `tests/BuildCv.Api.IntegrationTests/IterationEndpointsContainmentTests.cs` with missing/false factories. Malformed POST/GET return 404/405; unresolved poison `IIterationService` proves no binding/application/store path.
- [x] 1.2 Set only `Iteration:PublicApiEnabled=true` in `IterationEndpointsTests.cs`; retain auth/rate/contracts/behavior. Run `dotnet test tests/BuildCv.Api.IntegrationTests/BuildCv.Api.IntegrationTests.csproj --filter "FullyQualifiedName~IterationEndpoints"`; record RED failures in apply-progress.

### GREEN

- [x] 1.3 In `src/BuildCv.Api/Program.cs`, read missing=`false`, emit a Boolean-only log, and map iteration only when true; add no middleware, custom response, or DI change.
- [x] 1.4 Re-run the focused command; prove missing/false closure and explicit-true compatibility.

### REFACTOR, VERIFY, ROLLBACK

- [x] 1.5 Refactor factories; document compatibility-only `Iteration__PublicApiEnabled` in `README.md`, set false in `render.yaml`, and keep `appsettings*.json` key-free.
- [x] 1.6 Run `dotnet build BuildCv.slnx -c Release`, `dotnet test`, `dotnet format --verify-no-changes`, both Domain dependency checks, and `git diff --stat`; keep A0 below 400 lines.
- [ ] 1.7 Merge/deploy A0 before B0. Rollback: omit/set false plus restart, or revert A0; true is never rollback. Sync checked tasks to OpenSpec/Engram and TDD evidence to `sdd/025-security-containment/apply-progress`.

## Work Unit B0 — Public Invoicing Containment

Start from main containing A0.

### RED

- [ ] 2.1 Create `Invoicing/InvoicingEndpointsContainmentTests.cs` for all 12 method/path pairs with missing/false gates. Malformed bodies return 404/405; poisoned handlers/stores/providers prove zero resolution or side effects.
- [ ] 2.2 Give `InvoicingEndpointsTests.cs` an invoicing-only true factory; prove legacy compatibility and one-gate/both-gates matrices, including independent iteration mapping.
- [ ] 2.3 Update `Payments/PaymentEndpointsTests.cs`: with public invoicing absent/false, a signed APPROVED webhook records one internal invoice. Retain approved-webhook and reconciliation invoice Application tests.
- [ ] 2.4 Run `dotnet test tests/BuildCv.Api.IntegrationTests/BuildCv.Api.IntegrationTests.csproj --filter "FullyQualifiedName~InvoicingEndpoints"`; record RED failures before production edits.

### GREEN

- [ ] 2.5 In `Program.cs`, gate invoicing independently with missing=`false` and Boolean-only logging; leave payment mapping, DI, webhook, and reconciliation untouched.
- [ ] 2.6 Run `dotnet test tests/BuildCv.Api.IntegrationTests/BuildCv.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PaymentEndpointsTests"` and `dotnet test tests/BuildCv.Application.Tests/BuildCv.Application.Tests.csproj --filter "FullyQualifiedName~HandleWebhookHandlerTests.HandleAsync_creates_invoice_on_approved_webhook|FullyQualifiedName~PaymentReconciliationServiceTests.ReconcileAsync_creates_invoice_when_status_transitions_to_approved"`; prove B0 scenarios.

### REFACTOR, VERIFY, ROLLBACK

- [ ] 2.7 Refactor without hiding the route table; document the gate in `README.md`, set `Invoicing__PublicApiEnabled=false` in `render.yaml`, and keep `appsettings*.json` key-free.
- [ ] 2.8 Repeat A0's verification and `git diff --stat`; keep B0 below 400 lines. Never alter `specs/011-factus/*` or `specs/018-cv-iteration-loop/verify-report.md`.
- [ ] 2.9 Rollback: omit/set false plus restart, or revert B0. Immediately sync checkboxes to OpenSpec/Engram and cumulative apply-progress.
