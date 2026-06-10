# API Contract: Persistence (Internal)

**Note:** 010-persistence does NOT add new public API endpoints. It replaces the storage backend behind existing 009-auth endpoints. The API contracts remain unchanged.

## Affected Endpoints (storage swap only)

| Endpoint | Before (InMemory) | After (PostgreSQL) |
|----------|-------------------|-------------------|
| `POST /user/consent` | InMemoryConsentStore | EfConsentStore |
| `POST /user/consent/revoke` | InMemoryConsentStore | EfConsentStore |
| `GET /user/data` | InMemoryUserDataStore | EfUserDataStore |
| `PUT /user/data` | InMemoryUserDataStore | EfUserDataStore |
| `DELETE /user/data` | InMemoryUserDataStore | EfUserDataStore |
| `POST /auth/refresh` | InMemoryRefreshTokenStore | EfRefreshTokenStore |
| `POST /auth/logout` | InMemoryRefreshTokenStore | EfRefreshTokenStore |

## Response Contracts (unchanged)

See `specs/009-auth/contracts/auth-api.md` and `specs/009-auth/contracts/user-data-api.md` for full response schemas.

## New Health Check

### GET /health/ready (enhanced)

Response now includes PostgreSQL connectivity check:

```json
{
  "status": "Healthy",
  "results": {
    "parser": { "status": "Healthy", "durationMs": 2 },
    "ai-client": { "status": "Healthy", "durationMs": 1 },
    "pdf-generator": { "status": "Healthy", "durationMs": 3 },
    "postgres": { "status": "Healthy", "durationMs": 5 }
  }
}
```

## Configuration Contracts

### appsettings.json

```json
{
  "Persistence": {
    "Provider": "InMemory"  // or "Postgres"
  },
  "Postgres": {
    "ConnectionString": "Host=localhost;Database=buildcv;Username=postgres;Password=...",
    "EnableAutoMigrate": true
  }
}
```

### Environment Variables (Production)

```
Persistence__Provider=Postgres
Postgres__ConnectionString=Host=...;Database=buildcv;Username=...;Password=...
Postgres__EnableAutoMigrate=false
```
