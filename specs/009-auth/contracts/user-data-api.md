# API Contract: User Data & Consent

**Base URL**: `/api/v1`

**Authentication**: JWT Bearer token required for all endpoints

---

## Consent Endpoints

### POST /user/consent

Grant consent for data processing. Requires authenticated user.

**Headers**: `Authorization: Bearer {accessToken}`

**Request**:
```json
{
  "purpose": "scoring"
}
```

**Response** (201 Created):
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "policyVersion": 1,
  "consentDate": "2026-06-09T18:00:00Z",
  "revokedAt": null,
  "purpose": "scoring",
  "isValid": true
}
```

**Errors**:
- `401` Unauthorized — Invalid access token
- `403` Forbidden — Stale policy version, re-consent required
- `409` Conflict — Consent already granted for this purpose
- `429` Too Many Requests — Rate limit exceeded (10/min per IP)

---

### POST /user/consent/revoke

Revoke consent. Requires authenticated user with active consent.

**Headers**: `Authorization: Bearer {accessToken}`

**Request**:
```json
{
  "purpose": "scoring"
}
```

**Response** (200 OK):
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "policyVersion": 1,
  "consentDate": "2026-06-09T18:00:00Z",
  "revokedAt": "2026-06-09T19:00:00Z",
  "purpose": "scoring",
  "isValid": false
}
```

**Errors**:
- `401` Unauthorized — Invalid access token
- `404` Not Found — No active consent for this purpose
- `429` Too Many Requests — Rate limit exceeded (10/min per IP)

---

## ARCO Rights Endpoints

### GET /user/data

Access all user data (ARCO: Access). Requires authenticated user with active consent.

**Headers**: `Authorization: Bearer {accessToken}`

**Response** (200 OK):
```json
{
  "profile": {
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "provider": "google",
    "email": "user@example.com",
    "name": "John Doe"
  },
  "consents": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "policyVersion": 1,
      "consentDate": "2026-06-09T18:00:00Z",
      "revokedAt": null,
      "purpose": "scoring",
      "isValid": true
    }
  ],
  "treatmentLogs": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "dataType": "consent",
      "action": "grant",
      "timestamp": "2026-06-09T18:00:00Z",
      "reason": "purpose=scoring, policyVersion=1"
    }
  ]
}
```

**Errors**:
- `401` Unauthorized — Invalid access token
- `403` Forbidden — No active consent
- `404` Not Found — No user data found

---

### PUT /user/data

Rectify user data (ARCO: Rectification). Requires authenticated user with active consent.

**Headers**: `Authorization: Bearer {accessToken}`

**Request**:
```json
{
  "email": "newemail@example.com",
  "name": "Jane Doe"
}
```

**Response** (200 OK):
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "provider": "google",
  "email": "newemail@example.com",
  "name": "Jane Doe"
}
```

**Errors**:
- `401` Unauthorized — Invalid access token
- `403` Forbidden — No active consent
- `429` Too Many Requests — Rate limit exceeded (10/min per IP)

---

### DELETE /user/data

Cancel all user data (ARCO: Cancellation). Requires authenticated user with active consent.

**Headers**: `Authorization: Bearer {accessToken}`

**Response** (204 No Content)

**Side Effects**:
- All user data deleted (profile, CV data, scoring history)
- All consent records revoked
- Audit log entry created

**Errors**:
- `401` Unauthorized — Invalid access token
- `403` Forbidden — No active consent
- `404` Not Found — No data to delete

---

## Privacy Policy Endpoint (Public)

### GET /privacy-policy

Fetch current privacy policy. No authentication required.

**Response** (200 OK):
```json
{
  "version": 1,
  "effectiveDate": "2026-06-09",
  "lastUpdated": "2026-06-09",
  "content": "# Política de Privacidad de BuildCv\n\n## 1. Datos que recopilamos..."
}
```

**Query Parameters**:
- `version` (optional): Fetch specific policy version

**Errors**: None (always returns current version)

---

## Error Response Format

All errors follow RFC 9457 ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid or expired access token",
  "instance": "/api/v1/auth/me"
}
```
