# Design: 010-persistence — PostgreSQL Persistence via EF Core

## Technical Approach

Replace all in-memory stores (`InMemoryConsentStore`, `InMemoryUserDataStore`, `InMemoryRefreshTokenStore`) with EF Core adapters backed by PostgreSQL. Extract proper Application-layer interfaces (`IConsentStore`, `IUserDataStore`) so handlers depend on abstractions, not concrete in-memory implementations. This fixes Art. VI violation: concrete classes in Application layer.

Feature flag (`Persistence:Provider`) toggles between `InMemory` (dev/test) and `Postgres` (prod). Auto-migration in dev, explicit migrations in prod.

## Architecture Decisions

### Decision: Extract `IConsentStore` and `IUserDataStore` interfaces in Application layer

**Choice**: Create dedicated port interfaces in `Application/Features/Auth/`
**Alternatives considered**: 
- Keep using `InMemoryConsentStore` directly (violates Art. VI — concrete in Application)
- Use `IConsentService`/`IUserDataService` as the storage port (too high-level — they include business logic)
**Rationale**: Current handlers inject concrete `InMemoryConsentStore` and `InMemoryUserDataStore`. These are storage adapters that belong behind ports. `IConsentService` and `IUserDataService` remain business-logic interfaces with different method signatures (e.g., `GrantAsync` handles idempotency). The new store interfaces are pure CRUD for the persistence layer.

### Decision: EF Core with Npgsql PostgreSQL

**Choice**: `Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore`
**Alternatives considered**:
- Dapper (micro-ORM, no migration story)
- raw ADO.NET (too low-level for 3 entities)
- SQLite (wrong production target)
**Rationale**: EF Core provides fluent configuration, auto-migrations, health checks, and integrates with `IOptions<T>`. PostgreSQL is the target prod database. Three entities don't justify Dapper's manual mapping overhead.

### Decision: Feature flag for provider selection

**Choice**: `Persistence:Provider` setting with `InMemory`/`Postgres` values
**Alternatives considered**:
- Always Postgres (breaks dev/test without database)
- Separate DI extension methods (over-engineered for 2 modes)
**Rationale**: Allows running tests without PostgreSQL, keeps local dev frictionless, and matches the existing pattern (Infrastructure DI takes `IConfiguration`).

## Data Flow

```
                        ┌──────────────────────┐
                        │    Api Layer          │
                        │  Program.cs           │
                        │  (DI composition)     │
                        └──────────┬───────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    ▼                              ▼
        ┌───────────────────┐        ┌──────────────────────┐
        │  Application Layer │        │  Infrastructure Layer │
        │                    │        │                       │
        │ IConsentStore      │◄───────│ EfConsentStore        │
        │ IUserDataStore     │◄───────│ EfUserDataStore       │
        │ IRefreshTokenStore │◄───────│ EfRefreshTokenStore   │
        │                    │        │                       │
        │ Handlers use       │        │ BuildCvDbContext      │
        │ interfaces only    │        │ (Npgsql/EF Core)      │
        └───────────────────┘        └──────────────────────┘
                                               │
                                               ▼
                                    ┌─────────────────────┐
                                    │    PostgreSQL        │
                                    │  users               │
                                    │  consent_records     │
                                    │  data_treatment_logs │
                                    │  refresh_tokens      │
                                    └─────────────────────┘
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/BuildCv.Application/Features/Auth/IConsentStore.cs` | Create | Port interface: CRUD for `ConsentRecord` |
| `src/BuildCv.Application/Features/Auth/IUserDataStore.cs` | Create | Port interface: CRUD for `User` + `DataTreatmentLog` |
| `src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs` | Create | EF Core DbContext with DbSets + entity configurations |
| `src/BuildCv.Infrastructure/Persistence/EfConsentStore.cs` | Create | EF Core adapter implementing `IConsentStore` |
| `src/BuildCv.Infrastructure/Persistence/EfUserDataStore.cs` | Create | EF Core adapter implementing `IUserDataStore` |
| `src/BuildCv.Infrastructure/Persistence/EfRefreshTokenStore.cs` | Create | EF Core adapter implementing `IRefreshTokenStore` |
| `src/BuildCv.Infrastructure/Persistence/PostgresSettings.cs` | Create | Settings POCO: `ConnectionString`, `EnableAutoMigrate` |
| `src/BuildCv.Api/Health/PostgresHealthCheck.cs` | Create | Npgsql connectivity health check |
| `src/BuildCv.Application/Features/Auth/GrantConsentHandler.cs` | Modify | Replace `InMemoryConsentStore` → `IConsentStore` |
| `src/BuildCv.Application/Features/Auth/RevokeConsentHandler.cs` | Modify | Replace `InMemoryConsentStore` → `IConsentStore` |
| `src/BuildCv.Application/Features/Auth/HasActiveConsentHandler.cs` | Modify | Replace `InMemoryConsentStore` → `IConsentStore` |
| `src/BuildCv.Application/Features/Auth/GetConsentHistoryHandler.cs` | Modify | Replace `InMemoryConsentStore` → `IConsentStore` |
| `src/BuildCv.Application/Features/Auth/GetUserDataHandler.cs` | Modify | Replace both stores → `IConsentStore` + `IUserDataStore` |
| `src/BuildCv.Application/Features/Auth/RectifyUserDataHandler.cs` | Modify | Replace both stores → `IConsentStore` + `IUserDataStore` |
| `src/BuildCv.Application/Features/Auth/DeleteUserDataHandler.cs` | Modify | Replace both stores → `IConsentStore` + `IUserDataStore` |
| `src/BuildCv.Application/DependencyInjection.cs` | Modify | Register `IConsentStore` + `IUserDataStore` via feature flag |
| `src/BuildCv.Infrastructure/DependencyInjection.cs` | Modify | Add EF Core + Npgsql registration, `PostgresSettings` binding |
| `src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj` | Modify | Add EF Core + Npgsql package references |
| `src/BuildCv.Api/Program.cs` | Modify | Add Postgres health check, auto-migrate on startup |
| `tests/BuildCv.Infrastructure.Tests/BuildCv.Infrastructure.Tests.csproj` | Modify | Add EF Core InMemory provider for testing |
| `tests/BuildCv.Infrastructure.Tests/Persistence/EfConsentStoreTests.cs` | Create | Integration tests for consent persistence |
| `tests/BuildCv.Infrastructure.Tests/Persistence/EfUserDataStoreTests.cs` | Create | Integration tests for user data persistence |

## Interfaces / Contracts

### IConsentStore (Application layer)

```csharp
// src/BuildCv.Application/Features/Auth/IConsentStore.cs
using BuildCv.Domain.Auth;

namespace BuildCv.Application.Features.Auth;

public interface IConsentStore
{
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);
    Task<ConsentRecord?> GetActiveAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<ConsentRecord?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### IUserDataStore (Application layer)

```csharp
// src/BuildCv.Application/Features/Auth/IUserDataStore.cs
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public interface IUserDataStore
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByProviderAsync(string provider, string providerId, CancellationToken ct = default);
    Task UpsertAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
    Task AddTreatmentLogAsync(DataTreatmentLog log, CancellationToken ct = default);
    Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### PostgresSettings

```csharp
// src/BuildCv.Infrastructure/Persistence/PostgresSettings.cs
namespace BuildCv.Infrastructure.Persistence;

public sealed class PostgresSettings
{
    public const string SectionName = "Postgres";
    public string ConnectionString { get; init; } = "";
    public bool EnableAutoMigrate { get; init; }
}
```

## DbContext Configuration

```csharp
// src/BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs
using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Persistence;

public sealed class BuildCvDbContext(DbContextOptions<BuildCvDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<DataTreatmentLog> DataTreatmentLogs => Set<DataTreatmentLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuildCvDbContext).Assembly);
    }
}
```

### Entity Configurations (Fluent API)

```csharp
// src/BuildCv.Infrastructure/Persistence/Configurations/UserConfiguration.cs
using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Provider).HasColumnName("provider").HasMaxLength(50);
        builder.Property(u => u.ProviderId).HasColumnName("provider_id").HasMaxLength(255);
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.HasIndex(u => new { u.Provider, u.ProviderId }).IsUnique();
    }
}

// src/BuildCv.Infrastructure/Persistence/Configurations/ConsentRecordConfiguration.cs
internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.PolicyVersion).HasColumnName("policy_version");
        builder.Property(c => c.ConsentDate).HasColumnName("consent_date");
        builder.Property(c => c.RevokedAt).HasColumnName("revoked_at");
        builder.Property(c => c.Purpose).HasColumnName("purpose").HasMaxLength(100);
        builder.HasIndex(c => new { c.UserId, c.Purpose });
        builder.Ignore(c => c.IsValid);
    }
}

// src/BuildCv.Infrastructure/Persistence/Configurations/DataTreatmentLogConfiguration.cs
internal sealed class DataTreatmentLogConfiguration : IEntityTypeConfiguration<DataTreatmentLog>
{
    public void Configure(EntityTypeBuilder<DataTreatmentLog> builder)
    {
        builder.ToTable("data_treatment_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.Property(l => l.DataType).HasColumnName("data_type").HasMaxLength(50);
        builder.Property(l => l.Action).HasColumnName("action").HasMaxLength(50);
        builder.Property(l => l.Timestamp).HasColumnName("timestamp");
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.HasIndex(l => l.UserId);
    }
}

// src/BuildCv.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs
// (RefreshToken is a new entity owned by Infrastructure — not in Domain)
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Token);
        builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(200);
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(t => t.UserId);
    }
}
```

### RefreshToken Entity (Infrastructure-owned)

```csharp
// src/BuildCv.Infrastructure/Persistence/RefreshToken.cs
namespace BuildCv.Infrastructure.Persistence;

public sealed class RefreshToken
{
    public string Token { get; init; } = "";
    public Guid UserId { get; init; }
    public DateTime ExpiresAt { get; init; }
}
```

## DI Registration

### Infrastructure DI changes

```csharp
// Updated: src/BuildCv.Infrastructure/DependencyInjection.cs
// Additions:

// 1. PostgresSettings binding
services.Configure<PostgresSettings>(configuration.GetSection(PostgresSettings.SectionName));

// 2. Feature-flagged persistence
var persistenceProvider = configuration["Persistence:Provider"] ?? "InMemory";

if (persistenceProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    services.AddDbContext<BuildCvDbContext>(options =>
        options.UseNpgsql(configuration.GetSection(PostgresSettings.SectionName)["ConnectionString"]));
    
    services.AddScoped<IConsentStore, EfConsentStore>();
    services.AddScoped<IUserDataStore, EfUserDataStore>();
    services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();
}
else
{
    services.AddSingleton<InMemoryConsentStore>();
    services.AddSingleton<InMemoryUserDataStore>();
    services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
    // Register store interfaces for in-memory mode (wrapping existing stores)
    services.AddSingleton<IConsentStore>(sp => new InMemoryConsentStoreAdapter(sp.GetRequiredService<InMemoryConsentStore>()));
    services.AddSingleton<IUserDataStore>(sp => new InMemoryUserDataStoreAdapter(sp.GetRequiredService<InMemoryUserDataStore>()));
}
```

### Application DI changes

```csharp
// Updated: src/BuildCv.Application/DependencyInjection.cs
// Remove direct InMemoryConsentStore/InMemoryUserDataStore singleton registrations
// (moved to Infrastructure DI with feature flag)
// Handlers now use IConsentStore / IUserDataStore interfaces
```

### Program.cs changes

```csharp
// Add after builder.Build():
if (persistenceProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var postgresSettings = builder.Configuration.GetSection(PostgresSettings.SectionName).Get<PostgresSettings>();
    if (postgresSettings?.EnableAutoMigrate == true)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuildCvDbContext>();
        db.Database.Migrate();
    }
}
```

### Health Check

```csharp
// src/BuildCv.Api/Health/PostgresHealthCheck.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildCv.Api.Health;

public sealed class PostgresHealthCheck(BuildCvDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL connection OK.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unreachable.", ex);
        }
    }
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Handler logic with `IConsentStore`/`IUserDataStore` mocks | xUnit + Moq (same as 009-auth) |
| Integration | EF Core adapters against InMemory provider | `UseInMemoryDatabase()` — verifies LINQ queries + entity config without PostgreSQL |
| Integration | Full DI wiring with InMemory provider | `WebApplicationFactory` with `Persistence:Provider=InMemory` |
| E2E | PostgreSQL adapters against real DB | Dockerized PostgreSQL in CI (optional, gated by env var) |

## Migration / Rollout

### Dev Environment
- `Persistence:Provider=InMemory` (default) — no database required
- `Persistence:Provider=Postgres` + `Postgres:EnableAutoMigrate=true` — auto-migrate on startup

### Production
- `Persistence:Provider=Postgres` + `Postgres:EnableAutoMigrate=false`
- Explicit migrations: `dotnet ef migrations add <Name> --project src/BuildCv.Infrastructure`
- Apply: `dotnet ef database update --project src/BuildCv.Infrastructure`

### Database Schema (PostgreSQL)

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,
    provider VARCHAR(50) NOT NULL,
    provider_id VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    last_login_at TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX ix_users_provider_provider_id ON users (provider, provider_id);

CREATE TABLE consent_records (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    policy_version INT NOT NULL,
    consent_date TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,
    purpose VARCHAR(100) NOT NULL
);

CREATE INDEX ix_consent_records_user_id ON consent_records (user_id);

CREATE TABLE data_treatment_logs (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    data_type VARCHAR(50) NOT NULL,
    action VARCHAR(50) NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    reason VARCHAR(500) NOT NULL
);

CREATE INDEX ix_data_treatment_logs_user_id ON data_treatment_logs (user_id);

CREATE TABLE refresh_tokens (
    token VARCHAR(200) PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    expires_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);
```

## Open Questions

- [ ] Should `RefreshToken` entity live in Domain or Infrastructure? (Decision: Infrastructure — it's a JWT implementation detail, not a domain concept)
- [ ] Connection string from env var or `appsettings.json`? (Decision: Both via `IOptions<PostgresSettings>` — env var for prod, json for dev)
- [ ] Should we add `ApplyPendingMigrationsAsync` health check or separate it? (Decision: Separate — health check just tests connectivity, migration is explicit)
