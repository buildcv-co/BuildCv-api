# Tasks: 010-persistence — PostgreSQL Persistence via EF Core

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 650–800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Interface Extraction) → PR 2 (EF Core Setup + Adapters) → PR 3 (DI Wiring + Health + Tests) |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Interface extraction + handler refactor (Art. VI fix) | PR 1 | Foundation — everything else depends on this. ~120 lines changed. |
| 2 | EF Core packages, DbContext, entity configs, RefreshToken entity | PR 2 | Infrastructure foundation. ~250 lines new. |
| 3 | EfConsentStore + EfUserDataStore + EfRefreshTokenStore adapters | PR 2 (continued) | Depends on Unit 2. ~200 lines new. |
| 4 | DI wiring, PostgresSettings, feature flag, health check, auto-migrate | PR 3 | Integration layer. ~150 lines changed. |
| 5 | Integration tests (InMemory provider) | PR 3 (continued) | ~150 lines new. |

## Phase 1: Interface Extraction (Art. VI Compliance) — Foundation

- [ ] 1.1 **[TDD-RED]** Write tests verifying handlers compile with `IConsentStore`/`IUserDataStore` interfaces — test that injecting mocks of these interfaces into handlers produces correct behavior. File: `tests/BuildCv.Application.Tests/Features/Auth/ConsentHandlerTests.cs` (update existing) + `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs` (update existing). **S**
- [ ] 1.2 Create `IConsentStore` interface in `src/BuildCv.Application/Features/Auth/IConsentStore.cs` — methods: `Add`, `GetActiveAsync`, `GetLatestAsync`, `GetHistoryAsync`. Follow `IRefreshTokenStore` pattern. **S**
- [ ] 1.3 Create `IUserDataStore` interface in `src/BuildCv.Application/Features/Auth/IUserDataStore.cs` — methods: `GetByIdAsync`, `UpsertAsync`, `DeleteAsync`, `AddTreatmentLogAsync`, `GetTreatmentLogsAsync`. **S**
- [ ] 1.4 **[TDD-GREEN]** Modify `GrantConsentHandler` — change constructor param from `InMemoryConsentStore` to `IConsentStore`. Adapt `store.Add(record)` to `await store.AddAsync(record, ct)`. File: `src/BuildCv.Application/Features/Auth/GrantConsentHandler.cs`. **S**
- [ ] 1.5 Modify `RevokeConsentHandler` — replace `InMemoryConsentStore` → `IConsentStore`. File: `src/BuildCv.Application/Features/Auth/RevokeConsentHandler.cs`. **S**
- [ ] 1.6 Modify `HasActiveConsentHandler` — replace `InMemoryConsentStore` → `IConsentStore`. File: `src/BuildCv.Application/Features/Auth/HasActiveConsentHandler.cs`. **S**
- [ ] 1.7 Modify `GetConsentHistoryHandler` — replace `InMemoryConsentStore` → `IConsentStore`. File: `src/BuildCv.Application/Features/Auth/GetConsentHistoryHandler.cs`. **S**
- [ ] 1.8 Modify `GetUserDataHandler` — replace both `InMemoryConsentStore` → `IConsentStore` and `InMemoryUserDataStore` → `IUserDataStore`. File: `src/BuildCv.Application/Features/Auth/GetUserDataHandler.cs`. **S**
- [ ] 1.9 Modify `RectifyUserDataHandler` — replace both stores with interfaces. File: `src/BuildCv.Application/Features/Auth/RectifyUserDataHandler.cs`. **S**
- [ ] 1.10 Modify `DeleteUserDataHandler` — replace both stores with interfaces. File: `src/BuildCv.Application/Features/Auth/DeleteUserDataHandler.cs`. **S**
- [ ] 1.11 Update Application DI: register `IConsentStore` → `InMemoryConsentStore` and `IUserDataStore` → `InMemoryUserDataStore` adapters (create thin adapter classes or use lambda wrapping). Remove direct `AddSingleton<InMemoryConsentStore>()`/`AddSingleton<InMemoryUserDataStore>()` from Application DI (move to Infrastructure). File: `src/BuildCv.Application/DependencyInjection.cs`. **S**
- [ ] 1.12 **Verify**: `dotnet build BuildCv.slnx -c Release` — zero errors, `dotnet list src/BuildCv.Domain package references` returns 0. **S**

**Phase 1 total: ~120 lines changed, 0 new files (2 interfaces)**

## Phase 2: EF Core Setup (Infrastructure Foundation)

- [ ] 2.1 Add NuGet packages to Infrastructure: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`. File: `src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj`. **S**
- [ ] 2.2 Create `PostgresSettings` POCO: `ConnectionString`, `EnableAutoMigrate`. File: `src/BuildCv.Infrastructure/Persistence/PostgresSettings.cs`. **S**
- [ ] 2.3 Create `RefreshToken` entity (Infrastructure-owned, not Domain): `Token` (string PK), `UserId`, `ExpiresAt`. File: `src/BuildCv.Infrastructure/Persistence/RefreshToken.cs`. **S**
- [ ] 2.4 Create `BuildCvDbContext` with DbSets for `User`, `ConsentRecord`, `DataTreatmentLog`, `RefreshToken`. Override `OnModelCreating` with `ApplyConfigurationsFromAssembly`. File: `src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs`. **M**
- [ ] 2.5 Create `UserConfiguration` — table `users`, snake_case columns, unique index on `(Provider, ProviderId)`. File: `src/BuildCv.Infrastructure/Persistence/Configurations/UserConfiguration.cs`. **S**
- [ ] 2.6 Create `ConsentRecordConfiguration` — table `consent_records`, FK to `users`, index on `UserId`, ignore computed `IsValid`. File: `src/BuildCv.Infrastructure/Persistence/Configurations/ConsentRecordConfiguration.cs`. **S**
- [ ] 2.7 Create `DataTreatmentLogConfiguration` — table `data_treatment_logs`, FK to `users`, index on `UserId`. File: `src/BuildCv.Infrastructure/Persistence/Configurations/DataTreatmentLogConfiguration.cs`. **S**
- [ ] 2.8 Create `RefreshTokenConfiguration` — table `refresh_tokens`, PK on `Token`, index on `UserId`. File: `src/BuildCv.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`. **S**
- [ ] 2.9 **Verify**: `dotnet build src/BuildCv.Infrastructure -c Release` — zero errors. **S**

**Phase 2 total: ~250 lines new code**

## Phase 3: EF Core Store Adapters

- [ ] 3.1 **[TDD-RED]** Write integration tests for `EfConsentStore` using EF Core InMemory provider — test Add, GetActive, GetLatest, GetHistory, RevokeAll. File: `tests/BuildCv.Infrastructure.Tests/Persistence/EfConsentStoreTests.cs`. Add `Microsoft.EntityFrameworkCore.InMemory` package to test project. **M**
- [ ] 3.2 Implement `EfConsentStore : IConsentStore` — map all methods to EF Core LINQ queries. AddAsync inserts record; GetActiveAsync filters by `(UserId, Purpose)` and checks `IsValid`; RevokeAll sets `RevokedAt` on active records. File: `src/BuildCv.Infrastructure/Persistence/EfConsentStore.cs`. **M**
- [ ] 3.3 **[TDD-GREEN]** Run `EfConsentStoreTests` — verify all pass with InMemory provider. **S**
- [ ] 3.4 **[TDD-RED]** Write integration tests for `EfUserDataStore` using InMemory provider — test GetByIdAsync, UpsertAsync, DeleteAsync (cascade), AddTreatmentLogAsync, GetTreatmentLogsAsync. File: `tests/BuildCv.Infrastructure.Tests/Persistence/EfUserDataStoreTests.cs`. **M**
- [ ] 3.5 Implement `EfUserDataStore : IUserDataStore` — map methods to EF Core operations. GetByIdAsync returns `Result.Failure` with `ARCO/DATA_NOT_FOUND` when not found. DeleteAsync cascade-deletes related entities. File: `src/BuildCv.Infrastructure/Persistence/EfUserDataStore.cs`. **M**
- [ ] 3.6 **[TDD-GREEN]** Run `EfUserDataStoreTests` — verify all pass. **S**
- [ ] 3.7 Implement `EfRefreshTokenStore : IRefreshTokenStore` — CreateAsync inserts row, ValidateAsync checks expiry + revocation, RevokeAsync sets `RevokedAt`. File: `src/BuildCv.Infrastructure/Persistence/EfRefreshTokenStore.cs`. **M**
- [ ] 3.8 **Verify**: `dotnet test --filter "FullyQualifiedName~Persistence"` — all adapter tests pass. **S**

**Phase 3 total: ~200 lines new code + ~150 lines tests**

## Phase 4: DI Wiring, Feature Flag, Health Check

- [ ] 4.1 Update Infrastructure DI: bind `PostgresSettings`, read `Persistence:Provider` flag. If `Postgres` → register `BuildCvDbContext` + `EfConsentStore`/`EfUserDataStore`/`EfRefreshTokenStore`. If `InMemory` → register `InMemoryConsentStore`/`InMemoryUserDataStore` + adapters wrapping them as `IConsentStore`/`IUserDataStore`. File: `src/BuildCv.Infrastructure/DependencyInjection.cs`. **M**
- [ ] 4.2 Create `PostgresHealthCheck : IHealthCheck` — executes `SELECT 1` via DbContext, returns Healthy/Unhealthy. File: `src/BuildCv.Api/Health/PostgresHealthCheck.cs`. **S**
- [ ] 4.3 Update `Program.cs`: register Postgres health check, add auto-migrate on startup when `EnableAutoMigrate=true` and provider is Postgres. File: `src/BuildCv.Api/Program.cs`. **S**
- [ ] 4.4 Remove `InMemoryConsentStore`/`InMemoryUserDataStore` singleton registrations from Application DI (they now live in Infrastructure DI with feature flag). File: `src/BuildCv.Application/DependencyInjection.cs`. **S**
- [ ] 4.5 **Verify**: `dotnet build BuildCv.slnx -c Release` — zero errors, zero warnings. **S**

**Phase 4 total: ~150 lines changed**

## Phase 5: Integration Tests + Verification

- [ ] 5.1 Add `Microsoft.EntityFrameworkCore.InMemory` package to `BuildCv.Infrastructure.Tests.csproj`. **S**
- [ ] 5.2 Write DI wiring integration test: resolve `IConsentStore` from container with `Persistence:Provider=InMemory` → should be `InMemoryConsentStore`. File: `tests/BuildCv.Infrastructure.Tests/DependencyInjectionTests.cs`. **S**
- [ ] 5.3 Write DI wiring integration test: resolve `IConsentStore` with `Persistence:Provider=Postgres` → should be `EfConsentStore` (mock DbContext). **S**
- [ ] 5.4 Run full test suite: `dotnet test` — all 290+ existing auth tests pass + new persistence tests pass. **S**
- [ ] 5.5 Run `dotnet format --verify-no-changes` — zero formatting issues. **S**
- [ ] 5.6 Verify Art. VI compliance: `dotnet list src/BuildCv.Domain package references` → 0. `dotnet list src/BuildCv.Application package references` → 0 new (EF Core only in Infrastructure). **S**

**Phase 5 total: ~80 lines new tests**
