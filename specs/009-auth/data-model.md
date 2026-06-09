# Data Model: 009-auth

## Domain Entities

### User

Represents an authenticated user. Minimal profile data only — no CV content, no scoring history.

```csharp
// src/BuildCv.Domain/Auth/User.cs
namespace BuildCv.Domain.Auth;

public sealed class User
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = "";      // "google" or "linkedin"
    public string ProviderId { get; init; } = "";    // Provider's unique user ID
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime LastLoginAt { get; init; }
}
```

**Constraints:**
- `Provider` + `ProviderId` is the natural key (composite unique)
- `Email` and `Name` are updated on each login if changed at provider
- No CV content, no scoring data (Art. III)

---

### ConsentRecord

Tracks user consent for data processing. Append-only audit trail.

```csharp
// src/BuildCv.Domain/Auth/ConsentRecord.cs
namespace BuildCv.Domain.Auth;

public sealed class ConsentRecord
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int PolicyVersion { get; init; }
    public DateTime ConsentDate { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string Purpose { get; init; } = "";       // "scoring", "adapt", "export", etc.
    public bool IsValid => RevokedAt is null;         // Computed: active if not revoked
}
```

**Constraints:**
- One active consent per user + purpose (no duplicates)
- `PolicyVersion` must match current policy version at time of consent
- `RevokedAt` is null while active; set on revocation
- Old records retained for audit trail (append-only)

---

### DataTreatmentLog

Audit log for all data treatment operations. Immutable, append-only.

```csharp
// src/BuildCv.Domain/Auth/DataTreatmentLog.cs
namespace BuildCv.Domain.Auth;

public sealed class DataTreatmentLog
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string DataType { get; init; } = "";      // "consent", "profile", "cv", "scoring"
    public string Action { get; init; } = "";        // "grant", "revoke", "access", "rectify", "delete"
    public DateTime Timestamp { get; init; }
    public string Reason { get; init; } = "";        // Metadata: purpose, old/new value hashes
}
```

**Constraints:**
- Append-only (never update or delete)
- No PII in `Reason` field — only metadata (hashes, timestamps)
- Required for SIC compliance audit

---

## Application Ports (Interfaces)

### IAuthenticationService

OAuth token exchange + user lookup.

```csharp
public interface IAuthenticationService
{
    Task<Result<OAuthUserInfo>> ExchangeCodeAsync(
        string provider, string code, string redirectUri, 
        CancellationToken ct = default);
}

public sealed record OAuthUserInfo(
    string Provider, string ProviderId, string Email, string Name);
```

---

### IConsentService

Consent lifecycle management.

```csharp
public interface IConsentService
{
    Task<Result<ConsentRecord>> GrantAsync(
        Guid userId, string purpose, CancellationToken ct = default);
    
    Task<Result> RevokeAsync(
        Guid userId, string purpose, CancellationToken ct = default);
    
    Task<bool> HasActiveConsentAsync(
        Guid userId, string purpose, CancellationToken ct = default);
    
    Task<IReadOnlyList<ConsentRecord>> GetConsentHistoryAsync(
        Guid userId, CancellationToken ct = default);
}
```

---

### IUserDataService

User CRUD for ARCO rights.

```csharp
public interface IUserDataService
{
    Task<Result<User>> GetOrCreateAsync(
        string provider, string providerId, string email, string name, 
        CancellationToken ct = default);
    
    Task<Result<User>> GetByIdAsync(
        Guid userId, CancellationToken ct = default);
    
    Task<Result<User>> UpdateAsync(
        Guid userId, string? email, string? name, 
        CancellationToken ct = default);
    
    Task<Result> DeleteAsync(
        Guid userId, CancellationToken ct = default);
    
    Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(
        Guid userId, CancellationToken ct = default);
}
```

---

### IRefreshTokenStore

Refresh token lifecycle.

```csharp
public interface IRefreshTokenStore
{
    Task<string> CreateAsync(Guid userId, CancellationToken ct = default);
    Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}
```

---

## API Contracts

```csharp
// Request/Response records
public sealed record OAuthCallbackRequest(string Code, string? State = null);
public sealed record TokenResponse(string AccessToken, string RefreshToken, UserProfileResponse User);
public sealed record UserProfileResponse(Guid UserId, string Provider, string Email, string Name);
public sealed record ConsentRequest(string Purpose);
public sealed record RectifyUserDataRequest(string? Email, string? Name);
public sealed record UserDataResponse(UserProfileResponse Profile, IReadOnlyList<ConsentRecord> Consents, IReadOnlyList<DataTreatmentLog> TreatmentLogs);
```

---

## Relationships

```
User (1) ──── (N) ConsentRecord
User (1) ──── (N) DataTreatmentLog
User (1) ──── (N) RefreshToken
```

- A user can have multiple consent records (one per purpose + version)
- A user can have multiple audit log entries
- A user can have multiple refresh tokens (rotated on use)

---

## Storage Strategy (v0.5)

All stores are in-memory (`ConcurrentDictionary`) — no database.

| Store | Implementation | Notes |
|-------|----------------|-------|
| Users | `InMemoryUserDataStore` | Keyed by (Provider, ProviderId) |
| Consent | `InMemoryConsentStore` | Append-only, keyed by UserId |
| Audit | `InMemoryUserDataStore` | Append-only, keyed by UserId |
| Refresh Tokens | `InMemoryRefreshTokenStore` | Keyed by token value, with expiration |

**Migration to 010-persistence:** Swap `InMemory*Store` for EF Core implementations behind the same ports. No code changes in Application or Api layers.
