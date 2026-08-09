# Proposal: Fail-Closed Security Containment

## Intent

Contain two unsafe public surfaces pending durable remediation. Iteration retrieval lacks ownership checks and request rows retain CV/job text; invoicing exposes fiscal/company reads and mutations anonymously. Fail-closed defaults enforce Constitution Articles III, VI, VII, and IX.

## Scope

### In Scope
- **A0:** Add `Iteration:PublicApiEnabled`, absent/default `false`, gating public iteration POST/GET registration.
- **B0:** Add `Invoicing:PublicApiEnabled`, absent/default `false`, gating the complete public 011 route set.
- Disabled routes use normal not-found/method-not-allowed behavior; no custom response advertises disabled sensitive surfaces.
- Document fail-closed defaults and secret-free configuration examples.

### Non-goals
- Defer durable remediation **A1–A4** and **B1–B6**.
- No database purge, schema removal, iteration ownership redesign, authorization redesign, provenance migration, or durable 018/011 fix.
- No speculative product expansion.

## Operational Behavior and Compatibility

| Gate | Disabled (default) | Explicit compatibility mode |
|---|---|---|
| `Iteration:PublicApiEnabled` | No mapping, body deserialization, or handler/store invocation. | Current behavior for compatibility tests/development only. |
| `Invoicing:PublicApiEnabled` | No public mapping or handler/provider/store side effects. | Current behavior for compatibility tests only. |

Public API availability intentionally breaks by default. Internal payment/webhook invoice processing remains available independently.

## Capabilities

### New Capabilities
- `iteration-api-containment`: Fail-closed registration for the 018 public API.
- `invoicing-api-containment`: Fail-closed registration for the 011 public API, preserving internal processing.

### Modified Capabilities
- None; main OpenSpec specs are absent. Legacy 018/011 remain evidence, not fixes.

## Approach and Review Strategy

Strict TDD: observe RED integration tests before each gate implementation. Cover absent/false configuration, zero downstream invocation, standard routing responses, and enabled compatibility behavior; then conditionally compose mappings and update config/docs.

Force-chained, stacked-to-main delivery: **A0 first, B0 second**. Each is an autonomous review unit with tests/docs and under **400 changed lines**.

## Affected Areas

| Area | Impact |
|---|---|
| `src/BuildCv.Api/Program.cs` | Conditional route composition |
| `src/BuildCv.Api/appsettings*.json` and deployment docs | Fail-closed examples |
| `tests/BuildCv.Api.IntegrationTests/` | RED containment and compatibility coverage |

## Deployment and Rollback

Deploy with keys omitted/`false`; verify route absence and internal payments. Rollback by reverting/redeploying the work unit, never by production enablement.

## Risks

| Risk | Mitigation |
|---|---|
| Existing clients fail | Document intentional containment and compatibility-only enablement. |
| Containment is mistaken for remediation | Track A1–A4/B1–B6 explicitly as deferred. |
| Internal invoicing regresses | Prove payment/webhook invoice tests remain green. |

## Dependencies

- Constitution v1.2.0 and existing 018/011 implementations.

## Success Criteria

- [ ] Both public surfaces are absent by default with no deserialization or downstream side effects.
- [ ] Explicit compatibility modes preserve current behavior within their permitted environments.
- [ ] Internal payment/webhook invoicing remains operational.
