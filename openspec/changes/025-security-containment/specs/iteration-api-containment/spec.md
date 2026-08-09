# Iteration API Containment Specification

## Purpose

A0 containment under Articles III, VI, VII, IX.

## Requirements

### Requirement: Fail-closed iteration route registration

The API MUST map existing iteration POST and GET routes only when `Iteration:PublicApiEnabled` is explicitly `true`. If missing or `false`, both SHALL remain unmapped before body binding or application dispatch.

#### Scenario: Missing gate

- GIVEN `Iteration:PublicApiEnabled` is absent
- WHEN POST `/api/v1/adapt/iterate` or GET `/api/v1/adapt/iterate/{requestId}` is called
- THEN framework routing returns normal 404/405 without a custom response
- AND no deserialization, iteration application, or store call occurs

#### Scenario: Explicitly disabled gate

- GIVEN `Iteration:PublicApiEnabled` is explicitly `false`
- WHEN either iteration route is requested
- THEN unmapped 404/405 occurs before deserialization
- AND no iteration application or store call occurs

#### Scenario: Controlled compatibility enablement

- GIVEN the gate is `true` in a controlled compatibility/development test
- WHEN existing POST and GET contracts are exercised
- THEN both retain current auth, rate limits, contracts, and application behavior

### Requirement: Operational safety

Defaults/production MUST remain fail-closed. The gate MUST be independent from `Invoicing:PublicApiEnabled`. Rollback MUST be configuration-driven; routes MUST NOT silently enable. Logs MUST NOT contain secrets or sensitive payloads.

#### Scenario: Independent gates

- GIVEN only `Iteration:PublicApiEnabled` is `true`
- WHEN routes are inspected
- THEN iteration is mapped and public invoicing remains unmapped

#### Scenario: Fail-closed rollback and logging

- GIVEN production removes the gate or changes it to `false`
- WHEN the service restarts and receives requests
- THEN iteration routes are unmapped without fallback enablement
- AND emitted logs contain neither secrets nor sensitive payload content

## Non-requirements

A0 MUST NOT imply A1–A4 remediation. Database purge/schema removal, iteration ownership enforcement, persistence redesign, and all other durable A1–A4 units remain deferred.
