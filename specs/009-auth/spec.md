# Feature Specification: 009-auth

**Feature Branch**: `009-auth`

**Created**: 2026-06-09

**Status**: Implemented (47 tasks, 290 tests passing)

**Input**: User description: "Authentication (OAuth Google/LinkedIn) + Habeas Data compliance (consent, ARCO rights, privacy policy)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - OAuth 2.0 Authentication (Priority: P1)

Como usuario, necesito autenticarme con Google o LinkedIn para acceder a funcionalidades protegidas del sistema (historial de scores, persistencia de CV, etc.).

**Why this priority**: La autenticación es el prerequisite para todo lo demás: sin identificar al usuario, no podemos recolectar consentimiento, guardar datos, ni ofrecer historial.

**Independent Test**: Puedo hacer login con Google/LinkedIn, recibir tokens JWT, acceder a endpoints protegidos, refrescar tokens, y hacer logout. Test: `AuthEndpointTests`.

**Acceptance Scenarios**:

1. **Given** el usuario no está autenticado, **When** inicia login con Google, **Then** el sistema redirige al endpoint de autorización de Google con client_id, redirect_uri, scope (openid email profile), y state parameter.
2. **Given** el callback de OAuth es exitoso, **When** el sistema intercambia el código por tokens, **Then** crea o actualiza el registro del usuario (provider, provider_id, email, name) y emite access token (15min) + refresh token (7d).
3. **Given** el usuario tiene un refresh token válido, **When** el access token expira, **Then** el sistema emite nuevos tokens y invalida el anterior (rotación).
4. **Given** el usuario está autenticado, **When** llama POST /api/v1/auth/logout, **Then** el sistema revoca el refresh token.

---

### User Story 2 - Habeas Data Consent (Priority: P1)

Como usuario, necesito dar mi consentimiento informado, previo y expreso antes de que el sistema guarde mis datos personales, conforme a la Ley 1581 de 2012.

**Why this priority**: El consentimiento es gate bloqueante para la persistencia (Art. IX). Sin consentimiento válido, no podemos guardar nada.

**Independent Test**: Puedo ver la política de privacidad, otorgar consentimiento, revocarlo, y verificar que sin consentimiento el sistema rechaza guardar datos. Test: `ConsentEndpointTests`.

**Acceptance Scenarios**:

1. **Given** el usuario no tiene consentimiento activo, **When** intenta persistir datos, **Then** el sistema rechaza la operación y devuelve la versión actual de la política de privacidad.
2. **Given** el usuario ha visto la política de privacidad (versión N), **When** llama POST /api/v1/user/consent con consentimiento expreso, **Then** el sistema crea un registro de consentimiento (userId, scope, policyVersion=N, timestamp, granted=true).
3. **Given** el usuario tiene consentimiento activo en versión N, **When** la política se actualiza a N+1, **Then** el consentimiento existente se marca como obsoleto y se requiere re-consentimiento.

---

### User Story 3 - ARCO Rights (Priority: P1)

Como usuario, necesito ejercer mis derechos ARCO (Acceso, Rectificación, Cancelación, Oposición) sobre mis datos personales.

**Why this priority**: Los derechos ARCO son obligación legal (Art. IX, FR-052). El usuario debe poder acceder, corregir, eliminar y revocar sus datos.

**Independent Test**: Puedo acceder a todos mis datos, rectificar información, eliminar mi cuenta y todos mis datos, y revocar consentimiento. Test: `ArcoEndpointTests`.

**Acceptance Scenarios**:

1. **Given** el usuario está autenticado con consentimiento activo, **When** llama GET /api/v1/user/data, **Then** el sistema retorna todos los datos almacenados (perfil, registros de consentimiento, datos de CV si existen).
2. **Given** el usuario está autenticado, **When** llama PUT /api/v1/user/data con campos corregidos, **Then** el sistema actualiza los campos y registra la rectificación en el log de tratamiento.
3. **Given** el usuario está autenticado, **When** llama DELETE /api/v1/user/data, **Then** el sistema elimina todos los datos, revoca consentimientos, y registra la eliminación.

---

### User Story 4 - Privacy Policy (Priority: P2)

Como operador del sistema, necesito exponer una política de privacidad versionada y accesible públicamente.

**Why this priority**: La política es prerequisite para el consentimiento informado. El usuario debe poder leerla antes de decidir.

**Independent Test**: Puedo acceder a GET /api/v1/privacy-policy sin autenticación y recibir la política completa con versión y fecha. Test: `PrivacyEndpoints`.

**Acceptance Scenarios**:

1. **Given** la política está en versión N, **When** se solicita GET /api/v1/privacy-policy, **Then** el sistema retorna el texto completo (markdown), número de versión, fecha efectiva y última actualización.
2. **Given** existen versiones N-1 y N, **When** se solicita GET /api/v1/privacy-policy?version=N-1, **Then** el sistema retorna la versión específica.

---

### User Story 5 - Data Treatment Registry (Priority: P2)

Como operador del sistema, necesito un registro de auditoría de todas las operaciones de tratamiento de datos para cumplimiento legal.

**Why this priority**: El registro de auditoría es obligatorio para demostrar cumplimiento ante la SIC.

**Independent Test**: Cada operación de consentimiento, acceso, rectificación y cancelación genera un registro inmutable con timestamp y metadatos. Test: `ArcoEndpointTests`.

**Acceptance Scenarios**:

1. **Given** se otorga, revoca o actualiza un consentimiento, **When** se completa la operación, **Then** se escribe un registro de auditoría (userId, operation, scope, policyVersion, timestamp, metadata).
2. **Given** se ejerce un derecho ARCO (acceso, rectificación, cancelación), **When** se completa la operación, **Then** se escribe un registro de auditoría.

---

## Edge Cases & Error States

### Edge Cases

1. **CSRF state mismatch**: El parámetro state del callback no coincide con el generado → rechazar login.
2. **Provider unavailable**: Google o LinkedIn no responde → error genérico al usuario, log con traceId.
3. **Expired refresh token**: Token de refresco expirado o revocado → 401, usuario debe re-autenticarse.
4. **Stale consent**: Consentimiento en versión obsoleta → rechazar persistencia, requerir re-consentimiento.
5. **Policy versioning**: Cambio en política requiere re-consentimiento para todos los usuarios afectados.
6. **Rate limiting**: Endpoints de auth (30/min) y consent (10/min) por IP.
7. **Malformed JWT**: Token JWT malformado o manipulado → 401 sin detalles del error.

### Error States

1. **OAuth provider error**: access_denied, invalid_request → mensaje de error amigable, log interno.
2. **Invalid refresh token**: Token no válido o no existe → 401 Unauthorized.
3. **Consent already exists**: Intento de otorgar consentimiento cuando ya existe activo → 409 Conflict.
4. **Rate limit exceeded**: 429 Too Many Requests con Retry-After header.
5. **JWT validation failure**: Firma inválida, token expirado, issuer/audience incorrecto → 401.

---

## Constitution Compliance

| Article | Relevance | Implementation |
|---------|-----------|----------------|
| **Art. III** | Privacidad primero | No CV content in logs; minimal PII stored (email, name); consent required before any data persistence |
| **Art. IV** | Encuadre honesto | Privacy policy must honestly describe data treatment including international transfer to AI provider (ZDR gate) |
| **Art. VI** | Clean Architecture | `IAuthenticationService`, `IConsentService`, `IUserDataService` ports in Application; adapters in Infrastructure |
| **Art. VII** | v0/v0.5 sin fricción | Auth is v1 scope — v0/v0.5 remain unauthenticated, no server-side data storage |
| **Art. IX** | Habeas Data | Direct implementation: consent (FR-051), ARCO rights (FR-052), privacy policy (FR-053), audit trail |

---

## Non-Functional Requirements

- **Rate Limiting**: `auth` (30/min per IP), `consent` (10/min per IP)
- **Token Lifetime**: Access token 15min, Refresh token 7d
- **Storage**: In-memory for v0.5 (no database); EF Core deferred to 010-persistence
- **Privacy**: No PII in logs (Art. III); audit records store metadata only
- **Security**: JWT via IOptions (no secret exposure); OAuth via HTTPS only
