# Local Setup — BuildCv

Run the entire system locally for personal use. No production OAuth, no real
Wompi keys, no production database.

## Prerequisites

- **.NET 10 SDK** (pinned via `global.json`)
- **Node.js 22+**
- **pnpm 11**

## Quick Start

### 1. Start the backend (port 5080)

```bash
cd BuildCv-api
dotnet run --project src/BuildCv.Api
```

The backend starts with:

- **Persistence**: `InMemory` (no DB setup needed)
- **All feature flags ON** in `FeatureFlags:Defaults`
- **Local auth bypass** (`LocalAuth:Enabled=true`) — auto-creates `local@buildcv.dev`
  with `00000000-0000-0000-0000-000000000001` on first request
- **1000 credits pre-loaded** (refilled on startup if below)
- **Wompi keys mocked** — subscription flow runs but charges always succeed
- **Factus disabled** — no real DIAN invoicing

The fixed local user:

| Field | Value |
|-------|-------|
| ID | `00000000-0000-0000-0000-000000000001` |
| Email | `local@buildcv.dev` |
| Name | `Local User` |
| Initial credits | 1000 (refilled on startup if below) |

### 2. Start the frontend (port 3000)

```bash
cd BuildCv-web
pnpm install
echo "NEXT_PUBLIC_LOCAL_MODE=true" >> .env.local
pnpm dev
```

`NEXT_PUBLIC_LOCAL_MODE=true` makes the BFF skip NextAuth and sign a
local-mode HS256 JWT to exchange with the backend `/api/v1/auth/session`.

The frontend opens at `http://localhost:3000` and auto-authenticates you as
`local@buildcv.dev`.

## How local auth works

```
Browser /signin page
   └─> redirect("/analizar/iterate")            # IS_LOCAL check in page.tsx
                                                #
BFF /api/adapt/iterate                          # same-origin Route Handler
   └─> getJwtFromSession()                      # lib/api/jwt.ts
        └─> signLocalHs256Jwt(...)              # signs HS256 with NEXTAUTH_SECRET
        └─> fetch BACKEND/api/v1/auth/session   # sends Bearer token
                                                #
Backend pipeline                                #
   └─> UseAuthentication()                      # JwtBearer (rejects BFF JWT, fine)
   └─> UseAuthorization()                       #
   └─> LocalAuthMiddleware                      # ensures local user exists, sets claims if no auth
   └─> /api/v1/auth/session                     # extracts Bearer, validates via NextAuthJwtValidator
        └─> userStore.GetByIdAsync(...)         # finds local user (pre-created)
        └─> JwtTokenAdapter.GenerateAccessToken # issues backend JWT
        └─> returns backend JWT                 #
                                                #
BFF caches backend JWT, calls                  #
BACKEND/api/v1/adapt/iterate with backend JWT  #
                                                #
Backend: JwtBearer validates, [Authorize] passes, handler runs
```

### Key files

| File | Purpose |
|------|---------|
| `src/BuildCv.Api/appsettings.Development.json` | Dev config: LocalAuth, all flags ON, mock Wompi |
| `src/BuildCv.Application/Common/LocalAuthOptions.cs` | `LocalAuth` config binder |
| `src/BuildCv.Api/Auth/LocalAuthMiddleware.cs` | Pre-creates local user + sets claims |
| `src/BuildCv.Api/Program.cs` | Registers middleware + options |

## What works locally

- All scoring (002-score-engine)
- All adaptation (003-adapt-ia)
- CV import (005)
- CV iteration loop (018-cv-iteration-loop) — run 5 iterations, see best score + probability warning
- Credit consumption (013)
- Subscriptions (016) — Wompi mock, no real charges
- Admin endpoints (015-feature-flags) — flip flags at runtime
- ARCO anonymize (013 + 017 followups)
- Privacy policy v3 (017)

## What's stubbed

- Wompi real charges (uses mock keys, charges always succeed)
- Anthropic API (uses StubAiClient by default — deterministic outputs)
- OAuth providers (auto-login bypasses Google/LinkedIn)
- Database (in-memory, resets on restart)

## Switching to production mode

Set in `BuildCv-web/.env.local`:

```
NEXT_PUBLIC_LOCAL_MODE=false
```

Set real credentials in
`BuildCv-api/src/BuildCv.Api/appsettings.Development.json` (or use
`dotnet user-secrets`):

- `LocalAuth:Enabled=false`
- `Wompi:Enabled=true` with real sandbox keys
- `Ai:ApiKey` from `dotnet user-secrets set Ai:ApiKey <key>`

## Constitution compliance

Local mode is **DEVELOPMENT ONLY**. Constitution Art. III/IV/IX still apply
for any data sent to external APIs:

- No CV or job text is logged (Art. III).
- No CV or job text leaves the backend (Art. V) — `StubAiClient` produces
  deterministic outputs without external calls.
- Copy never promises ATS scoring or employment (Art. IV).
- Wompi mock returns success without real charges (Art. IX).
