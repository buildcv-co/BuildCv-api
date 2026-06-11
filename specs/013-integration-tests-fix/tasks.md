# Tasks: 013-integration-tests-fix

## Task 1: Register Invoicing handlers in DI

**File**: `BuildCv-api/src/BuildCv.Application/DependencyInjection.cs`

**Steps**:
1. Add `using BuildCv.Application.Features.Invoicing;` to the usings block
2. Add 7 `services.AddSingleton<THandler>()` lines after the existing handlers (after line 52)

**Acceptance Criteria**:
- `dotnet build BuildCv.slnx -c Release` — 0 warnings, 0 errors
- `dotnet format --verify-no-changes` — passes
- `dotnet test` — 53/57 integration tests pass (4 still fail with JSON deserialization)

**Estimated size**: S (small, ~10 lines)

## Task 2: Configure JSON enum string conversion

**File**: `BuildCv-api/src/BuildCv.Api/Program.cs`

**Steps**:
1. Add `using System.Text.Json.Serialization;` to the usings block
2. Add `ConfigureHttpJsonOptions` configuration with `JsonStringEnumConverter` after `AddInfrastructure()`

**Acceptance Criteria**:
- `dotnet build BuildCv.slnx -c Release` — 0 warnings, 0 errors
- `dotnet format --verify-no-changes` — passes
- `dotnet test` — all 57 integration tests pass
- All enum values serialize as strings (e.g., `"Draft"` instead of `0`)

**Estimated size**: S (small, ~5 lines)
