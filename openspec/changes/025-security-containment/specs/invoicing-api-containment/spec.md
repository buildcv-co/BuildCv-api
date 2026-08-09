# Invoicing API Containment Specification

## Purpose

B0 containment under Articles III, VI, VII, IX; approved internal invoicing remains.

## Requirements

### Requirement: Fail-closed public invoicing registration

The API MUST NOT map any `InvoicingEndpoints` public route unless `Invoicing:PublicApiEnabled` is `true`: invoice create/list/read/delete/PDF/XML, credit-note/support-document create, numbering-range list/create, and company read/update.

#### Scenario: Missing gate

- GIVEN `Invoicing:PublicApiEnabled` is absent
- WHEN each declared method/path is requested
- THEN framework routing returns normal 404/405 without a custom response
- AND no deserialization or handler/store/provider side effect occurs

#### Scenario: Explicitly disabled gate

- GIVEN `Invoicing:PublicApiEnabled` is explicitly `false`
- WHEN any declared route is requested
- THEN the same unmapped 404/405 occurs before deserialization
- AND no handler/store/provider side effect occurs

#### Scenario: Controlled legacy compatibility

- GIVEN the gate is `true` in a controlled compatibility/development test
- WHEN the legacy route suite is exercised
- THEN every current route is mapped without B0 contract changes
- AND enablement does not assert production safety

### Requirement: Internal invoice processing remains available

Regardless of gate value, approved-payment, verified-webhook, and internal-reconciliation invoice processing MUST remain available.

#### Scenario: Public gate disabled during approved payment processing

- GIVEN the gate is absent/`false` and internal payment prerequisites are enabled
- WHEN valid approved-payment or verified-webhook processing requests an invoice
- THEN internal processing remains available with prior behavior
- AND public invoicing routes remain unmapped

### Requirement: Operational safety

Defaults/production MUST remain fail-closed. The gate MUST be independent from `Iteration:PublicApiEnabled`. Rollback MUST be configuration-driven; routes MUST NOT silently enable. Logs MUST NOT contain secrets or sensitive payloads.

#### Scenario: Independent gates

- GIVEN only `Invoicing:PublicApiEnabled` is `true`
- WHEN routes are inspected
- THEN legacy invoicing is mapped and iteration remains unmapped

#### Scenario: Fail-closed rollback and logging

- GIVEN production removes the gate or changes it to `false`
- WHEN the service restarts and receives requests
- THEN public invoicing routes are unmapped without fallback enablement
- AND emitted logs contain neither secrets nor sensitive payload content

## Non-requirements

B0 MUST NOT imply B1–B6 remediation. Invoice authorization, ownership/provenance redesign, migration, and all other durable B1–B6 units remain deferred; compatibility is not security approval.
