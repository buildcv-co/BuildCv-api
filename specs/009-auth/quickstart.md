# Quickstart: 009-auth

## Local Setup

```bash
cd BuildCv-api

# 1. Install NuGet packages
dotnet add src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj package System.IdentityModel.Tokens.Jwt

# 2. Configure OAuth credentials (local development)
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID" --project src/BuildCv.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET" --project src/BuildCv.Api
dotnet user-secrets set "Authentication:LinkedIn:ClientId" "YOUR_LINKEDIN_CLIENT_ID" --project src/BuildCv.Api
dotnet user-secrets set "Authentication:LinkedIn:ClientSecret" "YOUR_LINKEDIN_CLIENT_SECRET" --project src/BuildCv.Api
dotnet user-secrets set "Jwt:Secret" "YOUR_JWT_SECRET_MIN_32_CHARS_LONG" --project src/BuildCv.Api

# 3. Build
dotnet build BuildCv.slnx -c Release

# 4. Run tests
dotnet test

# 5. Start the API
dotnet run --project src/BuildCv.Api
```

## OAuth Provider Setup

### Google

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create OAuth 2.0 credentials
3. Set authorized redirect URI: `http://localhost:5080/api/v1/auth/google/callback`
4. Copy Client ID and Client Secret

### LinkedIn

1. Go to [LinkedIn Developer Portal](https://www.linkedin.com/developers/)
2. Create an app
3. Set authorized redirect URI: `http://localhost:5080/api/v1/auth/linkedin/callback`
4. Copy Client ID and Client Secret

## Verification Commands

### Login Flow

```bash
# 1. Initiate Google login (opens browser)
curl http://localhost:5080/api/v1/auth/google

# 2. After callback, you'll receive:
# {
#   "accessToken": "eyJhbGciOiJIUzI1NiIs...",
#   "refreshToken": "dGhpcyBpcyBhIHJlZnJl...",
#   "user": {
#     "userId": "123e4567-e89b-12d3-a456-426614174000",
#     "provider": "google",
#     "email": "user@example.com",
#     "name": "John Doe"
#   }
# }

# 3. Access protected endpoint
curl http://localhost:5080/api/v1/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# 4. Refresh token
curl -X POST http://localhost:5080/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "YOUR_REFRESH_TOKEN"}'

# 5. Logout
curl -X POST http://localhost:5080/api/v1/auth/logout \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### Consent Flow

```bash
# 1. Check privacy policy
curl http://localhost:5080/api/v1/privacy-policy

# 2. Grant consent
curl -X POST http://localhost:5080/api/v1/user/consent \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"purpose": "scoring"}'

# 3. Check consent status
curl http://localhost:5080/api/v1/user/consent \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# 4. Revoke consent
curl -X POST http://localhost:5080/api/v1/user/consent/revoke \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"purpose": "scoring"}'
```

### ARCO Rights

```bash
# 1. Access (get all user data)
curl http://localhost:5080/api/v1/user/data \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# 2. Rectify (update profile)
curl -X PUT http://localhost:5080/api/v1/user/data \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"email": "newemail@example.com", "name": "Jane Doe"}'

# 3. Cancel (delete all data)
curl -X DELETE http://localhost:5080/api/v1/user/data \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Rollback

If auth causes issues, remove the JWT Bearer authentication configuration from `Program.cs` and the auth endpoint registrations. The rest of the application continues working unauthenticated (v0/v0.5 behavior).
