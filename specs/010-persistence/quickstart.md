# Quickstart: 010-persistence

## Local Setup (InMemory — no database required)

```bash
cd BuildCv-api

# 1. Build
dotnet build BuildCv.slnx -c Release

# 2. Run tests (uses InMemory provider by default)
dotnet test

# 3. Start the API (InMemory mode)
dotnet run --project src/BuildCv.Api

# 4. Verify auth endpoints work (same as 009-auth)
curl http://localhost:5080/api/v1/privacy-policy
```

## Local Setup (PostgreSQL)

### Prerequisites
- PostgreSQL running locally or via Docker
- Connection string

### Configuration

```bash
# 1. Set connection string (user secrets)
dotnet user-secrets set "Postgres:ConnectionString" "Host=localhost;Database=buildcv;Username=postgres;Password=yourpassword" --project src/BuildCv.Api
dotnet user-secrets set "Postgres:EnableAutoMigrate" "true" --project src/BuildCv.Api

# 2. Or via appsettings.Development.json
{
  "Postgres": {
    "ConnectionString": "Host=localhost;Database=buildcv;Username=postgres;Password=yourpassword",
    "EnableAutoMigrate": true
  },
  "Persistence": {
    "Provider": "Postgres"
  }
}

# 3. Build and run
dotnet build BuildCv.slnx -c Release
dotnet run --project src/BuildCv.Api

# 4. Auto-migration runs on startup (Development environment only)
```

### Docker PostgreSQL (quick start)

```bash
docker run -d \
  --name buildcv-postgres \
  -e POSTGRES_DB=buildcv \
  -e POSTGRES_PASSWORD=yourpassword \
  -p 5432:5432 \
  postgres:16-alpine
```

## Verification Commands

### Check Database Connection

```bash
# Health check endpoint
curl http://localhost:5080/health/ready

# Expected response (with PostgreSQL):
# {
#   "status": "Healthy",
#   "results": {
#     "parser": { "status": "Healthy" },
#     "ai-client": { "status": "Healthy" },
#     "pdf-generator": { "status": "Healthy" },
#     "postgres": { "status": "Healthy" }
#   }
# }
```

### Test Persistence Flow

```bash
# 1. Login (creates user in database)
curl -X POST http://localhost:5080/api/v1/auth/google \
  -H "Content-Type: application/json" \
  -d '{"code": "test_code"}'

# 2. Grant consent (persists to consent_records)
curl -X POST http://localhost:5080/api/v1/user/consent \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"purpose": "scoring"}'

# 3. Verify data persisted
curl http://localhost:5080/api/v1/user/data \
  -H "Authorization: Bearer YOUR_TOKEN"

# 4. Check PostgreSQL directly
psql -d buildcv -c "SELECT * FROM users;"
psql -d buildcv -c "SELECT * FROM consent_records;"
```

## Switching Between Providers

| Setting | InMemory | PostgreSQL |
|---------|----------|------------|
| `Persistence:Provider` | `InMemory` | `Postgres` |
| `Postgres:ConnectionString` | (ignored) | Required |
| `Postgres:EnableAutoMigrate` | (ignored) | `true` for Dev |

## Production Deployment

```bash
# 1. Set environment variables
export Postgres__ConnectionString="Host=your-render-postgres;Database=buildcv;Username=...;Password=..."
export Postgres__EnableAutoMigrate="false"
export Persistence__Provider="Postgres"

# 2. Run migrations manually (first deploy)
dotnet ef database update --project src/BuildCv.Infrastructure --startup-project src/BuildCv.Api

# 3. Start API
dotnet run --project src/BuildCv.Api
```

## Rollback

If PostgreSQL causes issues, set `Persistence:Provider=InMemory` in configuration. The system falls back to in-memory stores with no data persistence. No code changes needed.
