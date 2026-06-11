# Spec: 013-integration-tests-fix — DI registration for Invoicing handlers

## Overview

Register the 7 Invoicing handler classes in the Application DI container AND configure JSON serialization to use string enums. Currently:
1. The handlers are not registered, which causes `RouteEndpointDataSource` to throw `Failure to infer one or more parameters` at `app.Run()`, breaking all 57 integration tests across 10 test classes.
2. ASP.NET Core serializes enums as numbers by default, but integration tests expect string enum values (e.g., `"Draft"` instead of `0`).

## Root Causes

### Root Cause 1: Missing DI registrations

In `BuildCv-api/src/BuildCv.Application/DependencyInjection.cs`, the following 7 handlers from `BuildCv.Application.Features.Invoicing` are missing from `AddApplication()`:

| Handler | Constructor |
|---------|-------------|
| `CreateInvoiceHandler` | `(IInvoiceStore)` |
| `GetInvoiceHandler` | `(IInvoiceStore)` |
| `ListInvoicesHandler` | `(IInvoiceStore)` |
| `CreateCreditNoteHandler` | `(IInvoiceStore)` |
| `CreateSupportDocumentHandler` | `(IInvoiceStore)` |
| `GetNumberingRangesHandler` | `(INumberingRangeStore)` |
| `GetCompanyHandler` | `(IInvoiceProvider)` |

These were added in commit `68ee74b` (feat 011-factus) but never registered.

### Root Cause 2: Enum serialization as numbers

ASP.NET Core's default JSON serializer treats enums as numbers. Integration tests in `InvoicingEndpointsTests` define `InvoiceResponse.Status` as `string`, expecting serialized values like `"Draft"` instead of `0`. The fix is to add `JsonStringEnumConverter` to the HTTP JSON options.

## Requirements

### R1: Register all 7 handlers

The system MUST register all 7 Invoicing handlers in `BuildCv.Application.DependencyInjection.AddApplication()` as singletons, following the existing pattern (lines 30-52).

#### Scenario: All integration tests pass

- GIVEN the 7 handlers are registered in `AddApplication()`
- WHEN `dotnet test` runs all 57 integration tests
- THEN all 57 tests pass

### R2: No new dependencies

The fix MUST NOT add new NuGet packages or project references.

### R3: Follow existing convention

The fix MUST follow the existing `AddSingleton<Handler>()` pattern used for other handlers in the file.

### R4: Zero suppressions

The fix MUST NOT introduce any `#pragma`, `[Skip]`, or other suppressions.

## Approach

Add 7 `AddSingleton<THandler>()` lines after the existing handlers (after line 52), with a `using BuildCv.Application.Features.Invoicing;` directive.

## Constitution Compliance

- **Art. VI** (Clean Architecture): Handlers are Application layer, DI registration is correct layer
- **Art. VIII** (TDD): Fix is verified by running existing integration tests
- **Zero suppressions**: Respected

## Estimated Size

~10 changed lines. Single PR. No chained PRs needed.
