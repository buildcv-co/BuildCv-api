# Data Model: 010-persistence

## Domain Entities (existing, no changes)

### User

```csharp
// src/BuildCv.Domain/Auth/User.cs
public sealed class User
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime LastLoginAt { get; init; }
}
```

### ConsentRecord

```csharp
// src/BuildCv.Domain/Auth/ConsentRecord.cs
public sealed class ConsentRecord
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int PolicyVersion { get; init; }
    public DateTime ConsentDate { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string Purpose { get; init; } = "";
    public bool IsValid => RevokedAt is null;
}
```

### DataTreatmentLog

```csharp
// src/BuildCv.Domain/Auth/DataTreatmentLog.cs
public sealed class DataTreatmentLog
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string DataType { get; init; } = "";
    public string Action { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public string Reason { get; init; } = "";
}
```

## Infrastructure Entity (new)

### RefreshToken

Infrastructure-owned entity (JWT implementation detail, not domain concept).

```csharp
// src/BuildCv.Infrastructure/Persistence/RefreshToken.cs
namespace BuildCv.Infrastructure.Persistence;

public sealed class RefreshToken
{
    public string Token { get; init; } = "";  // PK
    public Guid UserId { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

## Application Port Interfaces (new)

### IConsentStore

```csharp
// src/BuildCv.Application/Features/Auth/IConsentStore.cs
namespace BuildCv.Application.Features.Auth;

public interface IConsentStore
{
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);
    Task<ConsentRecord?> GetActiveAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<ConsentRecord?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
    Task RevokeAllAsync(Guid userId, string purpose, CancellationToken ct = default);
}
```

### IUserDataStore

```csharp
// src/BuildCv.Application/Features/Auth/IUserDataStore.cs
namespace BuildCv.Application.Features.Auth;

public interface IUserDataStore
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByProviderAsync(string provider, string providerId, CancellationToken ct = default);
    Task UpsertAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
    Task AddTreatmentLogAsync(DataTreatmentLog log, CancellationToken ct = default);
    Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default);
}
```

## Database Schema (PostgreSQL)

### Table: users

| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK |
| provider | VARCHAR(50) | NOT NULL |
| provider_id | VARCHAR(255) | NOT NULL |
| email | VARCHAR(255) | NOT NULL |
| name | VARCHAR(255) | NOT NULL |
| created_at | TIMESTAMP | NOT NULL |
| last_login_at | TIMESTAMP | NOT NULL |

**Indexes:** UNIQUE on (provider, provider_id)

### Table: consent_records

| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK |
| user_id | UUID | FK → users.id |
| purpose | VARCHAR(100) | NOT NULL |
| policy_version | INT | NOT NULL |
| consent_date | TIMESTAMP | NOT NULL |
| revoked_at | TIMESTAMP | NULLABLE |

**Indexes:** (user_id, purpose)

### Table: data_treatment_logs

| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK |
| user_id | UUID | FK → users.id |
| data_type | VARCHAR(50) | NOT NULL |
| action | VARCHAR(50) | NOT NULL |
| timestamp | TIMESTAMP | NOT NULL |
| reason | VARCHAR(500) | NOT NULL |

**Indexes:** (user_id)

### Table: refresh_tokens

| Column | Type | Constraints |
|--------|------|-------------|
| token | VARCHAR(500) | PK |
| user_id | UUID | FK → users.id |
| expires_at | TIMESTAMP | NOT NULL |
| revoked_at | TIMESTAMP | NULLABLE |
| created_at | TIMESTAMP | NOT NULL |

**Indexes:** (user_id)

## Relationships

```
users (1) ──── (N) consent_records
users (1) ──── (N) data_treatment_logs
users (1) ──── (N) refresh_tokens
```

All foreign keys cascade on delete.

## Storage Strategy

| Mode | Provider | Use Case |
|------|----------|----------|
| `InMemory` | EF Core InMemory | Unit tests, local dev without PostgreSQL |
| `Postgres` | Npgsql + PostgreSQL | Production, integration tests |

Feature flag: `Persistence:Provider` in appsettings.json
