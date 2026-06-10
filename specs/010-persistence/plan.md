# Plan: 010-persistence — PostgreSQL Persistence via EF Core

## Intent

Replace in-memory stores with PostgreSQL + EF Core for production persistence. Fix Art. VI violation (concrete classes in Application layer) by extracting proper interfaces. Enable persistent user data, consent records, and audit trail for Habeas Data compliance.

## Scope

### In Scope
- Interface extraction (IConsentStore, IUserDataStore) — Art. VI compliance
- EF Core + Npgsql setup (BuildCvDbContext, entity configurations)
- EF Core adapters (EfConsentStore, EfUserDataStore, EfRefreshTokenStore)
- DI wiring with feature flag (InMemory vs Postgres)
- PostgreSQL health check
- Auto-migration strategy (Dev: auto, Prod: explicit)

### Out of Scope
- Database hosting setup (Render PostgreSQL — already configured)
- Data migration from InMemory to PostgreSQL (no existing data to migrate)
- Soft-delete pattern (deferred)
- Log rotation (deferred)
- Backup strategy (deferred)

### Deferred
- Testcontainers for integration tests (Docker dependency)
- Performance benchmarks
- Connection pooling tuning

## Approach

Follow Clean Architecture (Art. VI): ports in Application layer, adapters in Infrastructure. Use EF Core's fluent configuration for entity mapping. Feature flag toggles between InMemory (dev/test) and PostgreSQL (prod). Auto-migrate in Development, explicit migrations in Production.

Key design decisions:
- **Interface extraction first**: Fix Art. VI violation before adding EF Core
- **EF Core over Dapper**: Migration story, fluent config, health checks integration
- **Feature flag**: `Persistence:Provider` setting for InMemory/Postgres toggle
- **Infrastructure-owned RefreshToken**: JWT implementation detail, not domain concept

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/BuildCv.Application/Features/Auth/` | Modified | 7 handlers refactored to use interfaces |
| `src/BuildCv.Application/Features/Auth/IConsentStore.cs` | New | Port interface for consent persistence |
| `src/BuildCv.Application/Features/Auth/IUserDataStore.cs` | New | Port interface for user data persistence |
| `src/BuildCv.Infrastructure/Persistence/` | New | DbContext, adapters, configurations |
| `src/BuildCv.Api/Health/PostgresHealthCheck.cs` | New | PostgreSQL connectivity check |
| `src/BuildCv.Infrastructure/DependencyInjection.cs` | Modified | Feature flag + EF Core registration |
| `src/BuildCv.Application/DependencyInjection.cs` | Modified | Remove concrete store registrations |

## Constitution Compliance

| Article | Implementation |
|---------|----------------|
| **Art. III** | No CV content persisted; only auth/consent/audit metadata |
| **Art. VI** | Ports in Application, adapters in Infrastructure, Domain at 0 packages |
| **Art. IX** | Consent + ARCO persistence enables legal compliance audit trail |
