# Feature Specification: 010-persistence

**Feature Branch**: `010-persistence`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "PostgreSQL persistence with EF Core replacing in-memory stores, interface extraction for Art. VI compliance"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Interface Extraction (Priority: P0)

As a developer, I need the application layer handlers to depend on abstractions (interfaces) rather than concrete in-memory implementations, so that the architecture is Clean Architecture compliant (Art. VI) and persistence can be swapped without changing business logic.

**Why this priority**: 009-auth implemented handlers that depend directly on `InMemoryConsentStore` and `InMemoryUserDataStore` — violating Art. VI. This MUST be fixed before any EF Core work, as it's a prerequisite for the whole change.

**Independent Test**: All existing auth handler tests pass after injecting interfaces instead of concrete classes. No handler file contains a `using` for `InMemory*` classes. `dotnet list src/BuildCv.Domain package references` returns 0.

**Acceptance Scenarios**:

1. **Given** the `IConsentStore` interface exists with methods `Add`, `RevokeAll`, `GetHistoryAsync`, `GetActiveAsync`, `GetLatestAsync`, **When** a handler constructor accepts `IConsentStore`, **Then** it can operate on any implementation without compile errors.
2. **Given** the `IUserDataStore` interface exists with methods `Upsert`, `GetByIdAsync`, `Delete`, `AddLog`, `GetTreatmentLogsAsync`, **When** a handler constructor accepts `IUserDataStore`, **Then** it can operate on any implementation without compile errors.
3. **Given** the 7 handlers that currently depend on `InMemoryConsentStore` (GrantConsent, RevokeConsent, HasActiveConsent, GetConsentHistory, GetUserData, DeleteUserData, RectifyUserData), **When** the interfaces are extracted, **Then** all 7 handlers compile and all existing tests pass without modification (only DI wiring changes).
4. **Given** the 3 handlers that depend on `InMemoryUserDataStore` (GetUserData, DeleteUserData, RectifyUserData), **When** the interfaces are extracted, **Then** all 3 handlers compile and pass tests.
5. **Given** `InMemoryConsentStore` and `InMemoryUserDataStore` still exist, **When** registered via DI as the implementations of their respective interfaces, **Then** the system behaves identically to before the refactoring.
6. **Given** `Domain/BuildCv.Domain.csproj`, **When** inspected, **Then** it has zero external package references (pure domain, Art. VI).

### User Story 2 - EF Core + PostgreSQL Setup (Priority: P0)

As a developer, I need Entity Framework Core with Npgsql configured against PostgreSQL, with a `BuildCvDbContext` that models the auth domain entities, so that persistent storage replaces in-memory stores in production.

**Why this priority**: Without the database context and entity configuration, there is no persistence layer. This is the foundation for all EF Core adapters.

**Independent Test**: `dotnet build` succeeds with EF Core packages added only to Infrastructure. `BuildCvDbContext` has `DbSet<User>`, `DbSet<ConsentRecord>`, `DbSet<DataTreatmentLog>`, `DbSet<RefreshTokenEntity>`. Fluent API configuration produces correct table/column names. An initial migration can be generated.

**Acceptance Scenarios**:

1. **Given** `BuildCv.Infrastructure.csproj`, **When** EF Core and Npgsql packages are added, **Then** `BuildCv.Domain.csproj` has zero new package references and `BuildCv.Application.csproj` has zero new package references.
2. **Given** `BuildCvDbContext` is configured, **When** entity types are registered, **Then** `User` maps to a `users` table with columns `id` (UUID PK), `provider`, `provider_id`, `email`, `name`, `created_at`, `updated_at`.
3. **Given** `BuildCvDbContext`, **When** `ConsentRecord` is configured, **Then** it maps to `consent_records` with columns `id`, `user_id` (FK), `purpose`, `scope`, `policy_version`, `granted_at`, `revoked_at`, `metadata` (JSONB).
4. **Given** `BuildCvDbContext`, **When** `DataTreatmentLog` is configured, **Then** it maps to `data_treatment_logs` with columns `id`, `user_id` (FK), `operation`, `scope`, `policy_version`, `timestamp`, `metadata` (JSONB).
5. **Given** `BuildCvDbContext`, **When** `RefreshTokenEntity` is configured, **Then** it maps to `refresh_tokens` with columns `id` (string PK, token hash), `user_id` (FK), `expires_at`, `revoked_at`, `created_at`.
6. **Given** EF Core naming conventions, **When** entities are mapped, **Then** table and column names use snake_case (PostgreSQL convention).
7. **Given** `BuildCvDbContext` is registered in DI, **When** `ASPNETCORE_ENVIRONMENT=Development`, **Then** `Database.EnsureCreated()` or automatic migration runs on startup. **When** `ASPNETCORE_ENVIRONMENT=Production`, **Then** no automatic migration occurs — an explicit `dotnet ef database update` is required.

### User Story 3 - EF Core Store Adapters (Priority: P0)

As a developer, I need EF Core implementations of `IConsentStore`, `IUserDataStore`, and `IRefreshTokenStore` in the Infrastructure layer, so that auth data is persisted to PostgreSQL in production.

**Why this priority**: The interfaces exist (from Capability 1), the DbContext exists (from Capability 2) — now the adapters bridge them.

**Independent Test**: Each EF Core adapter can be tested with an in-memory SQLite or EF Core InMemory provider (unit tests). Integration tests use Testcontainers PostgreSQL. All existing auth endpoint tests pass with EF Core adapters registered.

**Acceptance Scenarios**:

1. **Given** `EfConsentStore : IConsentStore`, **When** `Add` is called, **Then** a `ConsentRecord` is inserted into the `consent_records` table and a duplicate `(userId, purpose)` overwrites the previous active record while preserving audit history.
2. **Given** `EfConsentStore`, **When** `RevokeAll` is called, **Then** all active records for the user are marked with `RevokedAt` and new audit entries are appended.
3. **Given** `EfConsentStore`, **When** `GetHistoryAsync` is called, **Then** all records (active + revoked) for the user are returned ordered by `GrantedAt` descending.
4. **Given** `EfConsentStore`, **When** `GetActiveAsync` is called for a user with an expired consent, **Then** `null` is returned (expiry is checked in-memory after retrieval, matching `InMemoryConsentStore` behavior).
5. **Given** `EfUserDataStore : IUserDataStore`, **When** `Upsert` is called, **Then** the user is inserted or updated (upsert by `Id`).
6. **Given** `EfUserDataStore`, **When** `GetByIdAsync` is called for a non-existent user, **Then** `Result.Failure` with error code `ARCO/DATA_NOT_FOUND` is returned.
7. **Given** `EfUserDataStore`, **When** `Delete` is called, **Then** the user row and all related `ConsentRecord` and `DataTreatmentLog` rows are cascade-deleted.
8. **Given** `EfRefreshTokenStore : IRefreshTokenStore`, **When** `CreateAsync` is called, **Then** a refresh token row is inserted with the token string as PK (or a hash), user_id, and expiry.
9. **Given** `EfRefreshTokenStore`, **When** `ValidateAsync` is called with an expired token, **Then** `Result.Failure` with error `AUTH/REFRESH_REVOKED` is returned.
10. **Given** `EfRefreshTokenStore`, **When** `RevokeAsync` is called, **Then** the token's `RevokedAt` is set (soft delete) and subsequent `ValidateAsync` returns failure.

### User Story 4 - DI Wiring + Health Check (Priority: P1)

As a developer, I need connection string configuration via `IOptions<PostgresSettings>`, a feature flag to toggle between InMemory and EF Core stores, and a PostgreSQL health check, so that the system is production-ready and observable.

**Why this priority**: Without DI wiring the new adapters cannot be used; without health checks the deploy monitor cannot detect database failures.

**Independent Test**: With `Persistence:Provider=PostgreSQL` in config, the DI container resolves `IConsentStore` as `EfConsentStore`. With `Persistence:Provider=InMemory`, it resolves as `InMemoryConsentStore`. `GET /health` returns `Healthy` when PostgreSQL is reachable, `Unhealthy` when it is not.

**Acceptance Scenarios**:

1. **Given** `PostgresSettings` with `ConnectionString`, **When** the configuration section `ConnectionStrings:Postgres` or `Postgres:ConnectionString` is present, **Then** `IOptions<PostgresSettings>` provides the value without hardcoding.
2. **Given** `Persistence:Provider=PostgreSQL`, **When** DI registration runs, **Then** `IConsentStore` resolves to `EfConsentStore`, `IUserDataStore` resolves to `EfUserDataStore`, and `IRefreshTokenStore` resolves to `EfRefreshTokenStore`.
3. **Given** `Persistence:Provider=InMemory`, **When** DI registration runs, **Then** `IConsentStore` resolves to `InMemoryConsentStore`, `IUserDataStore` resolves to `InMemoryUserDataStore`, and `IRefreshTokenStore` resolves to `InMemoryRefreshTokenStore`.
4. **Given** the PostgreSQL health check is registered, **When** `GET /health` is called and the database is reachable, **Then** the response is `200 OK` with status `Healthy` and a `postgres` entry.
5. **Given** the PostgreSQL health check is registered, **When** `GET /health` is called and the database is unreachable, **Then** the response is `503 Service Unavailable` with status `Unhealthy` and a `postgres` entry with error details.
6. **Given** the connection string is missing, **When** the application starts in `PostgreSQL` mode, **Then** a clear startup error is thrown (not a runtime NullReferenceException).

---

## Edge Cases & Error States

### Edge Cases

1. **Concurrent consent grants**: Two parallel requests grant consent for the same `(userId, purpose)` — last-write-wins at the row level, audit trail preserves both.
2. **Delete during active session**: User exercises ARCO deletion while another request is reading their data — `GetByIdAsync` returns `ARCO/DATA_NOT_FOUND` after deletion completes.
3. **Refresh token rotation race**: Two requests validate the same refresh token simultaneously — only one succeeds (first to revoke wins), the other gets `AUTH/REFRESH_REVOKED`.
4. **Large audit trail**: User with hundreds of consent history entries — `GetHistoryAsync` returns paginated or full list (full list acceptable at current scale, document limit).
5. **Connection pool exhaustion**: All EF Core connections are in use — new requests queue and timeout per `CommandTimeout` setting.

### Error States

1. **PostgreSQL unreachable**: Connection refused or DNS failure — health check returns `Unhealthy`, API continues to serve non-persistence endpoints (scoring, export).
2. **Migration missing**: Schema version mismatch — EF Core throws `InvalidOperationException` on first query; startup health check catches this.
3. **Unique constraint violation**: Duplicate refresh token (astronomically unlikely with GUID-based generation) — EF Core throws `DbUpdateException`; adapter translates to appropriate Result error.
4. **Transaction deadlock**: Concurrent upserts on same user row — EF Core retries or throws; adapter logs and returns failure.

---

## Constitution Compliance

| Article | Relevance | Implementation |
|---------|-----------|----------------|
| **Art. III** | Privacy first | EF Core stores ONLY handle auth data (User, Consent, Audit). CV/Job content is NOT persisted. No PII in logs — connection string and query logs excluded. |
| **Art. VI** | Clean Architecture | `IConsentStore`, `IUserDataStore`, `IRefreshTokenStore` interfaces in Application. `Ef*` implementations in Infrastructure. Domain has 0 package references. Handlers depend only on interfaces. |
| **Art. VII** | v0.5 no friction | InMemory provider remains default for dev/test without PostgreSQL. Feature flag allows zero-config local development. |
| **Art. IX** | Habeas Data | EF Core stores enable the consent + ARCO persistence that Art. IX requires. Consent records are append-only audit trails. Deletion cascades fully (ARCO compliance). |

---

## Non-Functional Requirements

- **NFR-P1**: `BuildCv.Domain` MUST have 0 external package references after this change (verified via `dotnet list`).
- **NFR-P2**: EF Core packages MUST appear ONLY in `BuildCv.Infrastructure.csproj`.
- **NFR-P3**: Connection string MUST be provided via `IOptions<PostgresSettings>` — no hardcoded connection strings.
- **NFR-P4**: PostgreSQL health check MUST be registered and accessible at `GET /health`.
- **NFR-P5**: Automatic migration MUST run only when `ASPNETCORE_ENVIRONMENT=Development`.
- **NFR-P6**: All existing 290 auth tests MUST continue passing after the change.
- **NFR-P7**: EF Core stores MUST be testable with the EF Core InMemory provider (no Docker required for unit tests).
- **NFR-P8**: Integration tests MUST use Testcontainers for real PostgreSQL validation.

---

## Technical Notes

### Files Affected (estimated)

**New files:**
- `Application/Features/Auth/IConsentStore.cs`
- `Application/Features/Auth/IUserDataStore.cs`
- `Infrastructure/Persistence/BuildCvDbContext.cs`
- `Infrastructure/Persistence/PostgresSettings.cs`
- `Infrastructure/Persistence/EfConsentStore.cs`
- `Infrastructure/Persistence/EfUserDataStore.cs`
- `Infrastructure/Persistence/EfRefreshTokenStore.cs`
- `Infrastructure/Persistence/Entities/UserEntity.cs`
- `Infrastructure/Persistence/Entities/ConsentRecordEntity.cs`
- `Infrastructure/Persistence/Entities/DataTreatmentLogEntity.cs`
- `Infrastructure/Persistence/Entities/RefreshTokenEntity.cs`
- `Infrastructure/Persistence/Configurations/` (one per entity)

**Modified files:**
- 7 handlers in `Application/Features/Auth/` (constructor change: `InMemoryConsentStore` → `IConsentStore`)
- 3 handlers (constructor change: `InMemoryUserDataStore` → `IUserDataStore`)
- `Application/DependencyInjection.cs` (register interfaces with InMemory for default)
- `Infrastructure/DependencyInjection.cs` (register EF Core stores when PostgreSQL mode)
- `Infrastructure/BuildCv.Infrastructure.csproj` (EF Core + Npgsql packages)
- `Api/Program.cs` (health check registration, DbContext auto-migrate in Dev)

**Unchanged:**
- `Domain/` — zero changes, remains pure
- All existing test files — only DI wiring in test fixtures may change

### Existing Pattern Reference

The `IRefreshTokenStore` → `InMemoryRefreshTokenStore` → (future) `EfRefreshTokenStore` pattern is already established and serves as the template for `IConsentStore` and `IUserDataStore`.
