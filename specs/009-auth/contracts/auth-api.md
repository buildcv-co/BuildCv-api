# API Contract: Authentication

**Base URL**: `/api/v1/auth`

**Authentication**: JWT Bearer token required unless noted

---

## POST /auth/google

Initiate Google OAuth login. Returns redirect URL.

**Request**: None (redirects to Google)

**Response** (200 OK):
```json
{
  "redirectUrl": "https://accounts.google.com/o/oauth2/auth?client_id=..."
}
```

**Errors**:
- `503` Service Unavailable — Google OAuth unreachable

---

## POST /auth/google/callback

Google OAuth callback. Exchanges code for tokens.

**Request**:
```json
{
  "code": "4/0AX4XfWh...",
  "state": "random_state_value"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl...",
  "user": {
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "provider": "google",
    "email": "user@example.com",
    "name": "John Doe"
  }
}
```

**Errors**:
- `401` Unauthorized — OAuth code exchange failed
- `403` Forbidden — State parameter mismatch (CSRF)
- `429` Too Many Requests — Rate limit exceeded (30/min per IP)

---

## POST /auth/linkedin

Initiate LinkedIn OAuth login. Returns redirect URL.

**Request**: None (redirects to LinkedIn)

**Response** (200 OK):
```json
{
  "redirectUrl": "https://www.linkedin.com/oauth/v2/authorization?client_id=..."
}
```

**Errors**:
- `503` Service Unavailable — LinkedIn OAuth unreachable

---

## POST /auth/linkedin/callback

LinkedIn OAuth callback. Exchanges code for tokens.

**Request**:
```json
{
  "code": "AQXz7...",
  "state": "random_state_value"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl...",
  "user": {
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "provider": "linkedin",
    "email": "user@example.com",
    "name": "John Doe"
  }
}
```

**Errors**:
- `401` Unauthorized — OAuth code exchange failed
- `403` Forbidden — State parameter mismatch (CSRF)
- `429` Too Many Requests — Rate limit exceeded (30/min per IP)

---

## GET /auth/me

Get current user profile. Requires valid access token.

**Headers**: `Authorization: Bearer {accessToken}`

**Response** (200 OK):
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "provider": "google",
  "email": "user@example.com",
  "name": "John Doe"
}
```

**Errors**:
- `401` Unauthorized — Invalid or expired access token

---

## POST /auth/refresh

Refresh access token. Requires valid refresh token.

**Request**:
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl..."
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4...",
  "user": {
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "provider": "google",
    "email": "user@example.com",
    "name": "John Doe"
  }
}
```

**Errors**:
- `401` Unauthorized — Invalid or revoked refresh token

---

## POST /auth/logout

Logout user. Revokes refresh token. Requires valid access token.

**Headers**: `Authorization: Bearer {accessToken}`

**Response** (204 No Content)

**Errors**:
- `401` Unauthorized — Invalid access token
