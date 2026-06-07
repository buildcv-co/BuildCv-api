# Contrato de API — BuildCv (REST `/api/v1`)

> **Artefacto SDD:** `specs/001-mvp-cv-ats/contracts/api-contract.md` — define el **contrato HTTP** que sirve los requisitos funcionales de `spec.md`. Es el "qué expone el backend" que consumen el frontend (Next.js BFF) y los tests de integración. Deriva de `spec.md` (QUÉ/POR QUÉ), `plan.md` (CÓMO), `research.md` (decisiones D04/D06/D08/D14/D15) y el insumo de arquitectura .NET.
>
> **Idioma:** documentación en español; **identificadores de código/JSON en inglés** (campos de DTO, valores de enum).
> **Estilo:** Minimal APIs (`MapGroup` + `TypedResults` + endpoint filters), versionado por segmento de URL `Asp.Versioning.Http`. Errores RFC 7807/9457 (`application/problem+json`).
> **Fecha base:** 2026-06-06 · **Versión del contrato:** `v1` (HTTP). *(v0/v1 del producto son hitos internos; el contrato HTTP nace en `v1`.)*
>
> **Leyenda de hito por endpoint:** **[v0]** = hito lanzable sin cuentas · **[v1]** = hito comercial (cuentas/créditos/pagos/legal/archivos).

---

## 0. Índice de endpoints

| Hito | Método | Ruta | Propósito | FR principales |
|---|---|---|---|---|
| v0 | `POST` | `/api/v1/score` | Análisis determinista (puntaje + keywords + recomendaciones) | FR-005..FR-019, FR-021, FR-022 |
| v0 | `POST` | `/api/v1/adapt/stream` | Adaptación con IA en streaming SSE + verificación de honestidad | FR-023..FR-030 |
| v0 | `POST` | `/api/v1/export/pdf` | Exportar CV adaptado a PDF | FR-033 |
| v0 | `GET`  | `/health/live` · `/health/ready` | Liveness / readiness | NFR-018 |
| v1 | `POST` | `/api/v1/auth/register` | Crear cuenta | FR-044, FR-051 |
| v1 | `POST` | `/api/v1/auth/login` | Iniciar sesión (JWT) | FR-044 |
| v1 | `POST` | `/api/v1/auth/refresh` | Renovar token de acceso | FR-044 |
| v1 | `GET`  | `/api/v1/credits` | Saldo y movimientos de créditos | FR-046 |
| v1 | `GET`  | `/api/v1/adaptations` | Historial de adaptaciones | FR-045 |
| v1 | `GET`  | `/api/v1/adaptations/{id}` | Detalle de una adaptación guardada | FR-045 |
| v1 | `POST` | `/api/v1/payments/wompi/checkout` | Iniciar compra de créditos (firma server-side) | FR-047, FR-049 |
| v1 | `POST` | `/api/v1/webhooks/wompi` | Confirmación de pago firmada e idempotente | FR-048 |
| v1 | `POST` | `/api/v1/consent` · `POST /api/v1/consent/revoke` | Consentimiento de datos y revocación | FR-051, FR-052 |
| v1 | `GET`  | `/api/v1/me/data` · `DELETE /api/v1/me` | Derechos ARCO (acceso / supresión) | FR-052 |
| v1 | `POST` | `/api/v1/cv/parse` | Subir y extraer CV (PDF/DOCX) | FR-054, FR-055 |

---

## 1. Convenciones generales

### 1.1 URL base y versionado
- **Base:** `https://api.buildcv.co` (prod) · `http://localhost:8080` (dev).
- **Versionado por segmento de URL:** `/api/v1/...` mediante `Asp.Versioning.Http`. Explícito, visible en logs/caché, trivial para el cliente. Las rutas `/health/*` y `/api/v1/webhooks/*` quedan **fuera del versionado de negocio** (infraestructura/integración).
- **Cambios compatibles** (añadir campos opcionales, nuevos valores de enum) NO suben de versión; **cambios incompatibles** abren `/api/v2`.

### 1.2 Content types
| Caso | `Content-Type` |
|---|---|
| Request/response JSON | `application/json; charset=utf-8` |
| Stream de adaptación | `text/event-stream; charset=utf-8` |
| Export PDF (respuesta) | `application/pdf` |
| Subida de CV (request) | `multipart/form-data` |
| Errores | `application/problem+json; charset=utf-8` |

### 1.3 Idioma y localización
- Los **mensajes orientados al usuario** (`title`, `detail`, `band`, `honestyNotice`) se devuelven en **español (es-CO)**.
- Los **identificadores y enums** son en inglés y estables (no se traducen): el frontend mapea enum → copy localizable.

### 1.4 Sellado de versión (reproducibilidad — FR-006, FR-013, FR-031)
Toda respuesta de análisis incluye:
- `engineVersion` (semver del `ScoringEngine`, p. ej. `"1.0.0"`),
- `lexiconVersion` (versión del Diccionario de Habilidades, p. ej. `"2026.06.0"`),
- `contextId` (identificador de la Sesión de Análisis efímera; **no** es PII, es un GUID en memoria).

El mismo `contextId` se reenvía al recalcular el puntaje del CV adaptado para que la comparación sea válida (FR-031). En v0 el `contextId` vive solo durante la interacción (Sesión de Análisis no persistida, FR-040); el backend lo trata como token opaco y reproducible a partir de `(cvText, jobText, engineVersion)`.

### 1.5 Trazas
- Cada respuesta incluye `traceId` (en el cuerpo de errores y en el header `X-Trace-Id`) para correlación con logs estructurados. **Nunca** se loguea el contenido de CV/vacante, solo metadatos (FR-041, NFR-002).

### 1.6 Defensa de entrada (FR-026, NFR-005)
El contenido de `cvText`/`jobText` se trata **siempre como dato**, nunca como instrucciones. El backend no interpreta órdenes incrustadas; el guardarraíl anti prompt-injection vive en la capa de IA (system prompt + bloques delimitados con nonce), no en el contrato.

---

## 2. Modelo de error — ProblemDetails (RFC 7807 / 9457)

Todas las respuestas de error usan `application/problem+json`. Estructura base:

```json
{
  "type": "https://buildcv.co/errors/validation",
  "title": "La entrada no cumple las reglas de validación.",
  "status": 400,
  "detail": "El campo 'cvText' está por debajo del mínimo de 200 caracteres.",
  "instance": "/api/v1/score",
  "traceId": "00-3f9a1b...-7c2d-01",
  "errors": {
    "cvText": ["Debe tener al menos 200 caracteres."],
    "jobText": ["Es obligatorio."]
  }
}
```

- `type` es una URI estable por clase de error (no resoluble obligatoriamente; sirve de clave).
- `errors` (mapa campo→mensajes) aparece **solo** en errores de validación (`ValidationProblemDetails`, 400).
- `detail` nunca filtra trazas internas, SQL, ni contenido del CV.

### 2.1 Catálogo de errores y mapeo de estado

| `type` (sufijo) | HTTP | Cuándo | Notas |
|---|---|---|---|
| `validation` | `400` | Entrada vacía/corta/larga, JSON inválido | FR-002, FR-037; incluye `errors` |
| `payload-too-large` | `413` | Cuerpo excede el tope (anti-abuso) | FR-037; rechaza **antes** de costo de IA |
| `unauthorized` | `401` | Falta/expira JWT (v1) | `WWW-Authenticate: Bearer` |
| `forbidden` | `403` | Sin permiso / sin consentimiento vigente (v1) | FR-051 |
| `insufficient-credits` | `402` | Adaptación sin créditos (v1) | FR-046; `detail` indica saldo |
| `not-found` | `404` | Recurso inexistente (adaptación/transacción) | |
| `rate-limited` | `429` | Excede política de frecuencia | FR-036; headers §3 |
| `ai-unavailable` | `503` | Circuito abierto / proveedor IA caído | FR-030; `Retry-After`; degradación elegante |
| `ai-timeout` | `504` | La IA superó el timeout por intento | D09 (Polly) |
| `client-closed` | `499` | El cliente abortó (cancelación) | FR-028; no es error de servidor |
| `webhook-invalid-signature` | `401` | Firma de webhook inválida (v1) | FR-048; se descarta el evento |
| `unsupported-media` | `415` | Archivo no soportado/ilegible (v1) | FR-054 |
| `internal` | `500` | Excepción no controlada | `GlobalExceptionHandler`; sin detalles |

> **Degradación elegante (FR-030 / US-016):** un fallo de IA en `/adapt/stream` **no** afecta a `/score`; el cliente conserva puntaje, keywords y recomendaciones. Si la IA cae durante el stream ya iniciado, se emite un evento SSE `error` (§5.4), no un código HTTP de error (los headers ya se enviaron).

---

## 3. Rate limiting y cabeceras (FR-036, FR-037, FR-038)

Particionado **por IP** (con `ForwardedHeadersOptions` para IP real tras proxy). Políticas diferenciadas por costo:

| Política | Endpoints | Límite v0 | Ventana |
|---|---|---|---|
| `score` (barata, 0 tokens) | `/score`, `/export/pdf` | 20 solicitudes | 1 min |
| `adapt` (cara, consume IA) | `/adapt/stream` | 5 solicitudes | 1 hora |
| Global de concurrencia | todos | 50 concurrentes (sin cola) | — |

**Headers en toda respuesta sujeta a límite** (informa el uso restante — FR-038):

```
X-RateLimit-Limit: 5
X-RateLimit-Remaining: 3
X-RateLimit-Reset: 1749250800        # epoch (s) del reinicio de ventana
```

**En `429`** se añade:

```
Retry-After: 1820                    # segundos hasta poder reintentar
```

Respuesta `429` (cuerpo ProblemDetails):

```json
{
  "type": "https://buildcv.co/errors/rate-limited",
  "title": "Demasiadas solicitudes. Intenta de nuevo en un momento.",
  "status": 429,
  "detail": "Alcanzaste el límite de adaptaciones. Se reinicia en ~30 min.",
  "instance": "/api/v1/adapt/stream",
  "traceId": "00-a1b2..."
}
```

> Defensas de borde adicionales (Turnstile, honeypot, debounce, tope de longitud cliente) viven en el BFF de Next.js (FR-039, D10), no en este contrato.

---

## 4. CORS, autenticación y seguridad

### 4.1 CORS
- Política nombrada `frontend` que permite los orígenes: `https://buildcv.co`, `https://*.vercel.app` (previews) y `http://localhost:3000` (dev). Orígenes desde configuración por entorno.
- Métodos: `GET, POST, OPTIONS`. Headers permitidos: `Content-Type, Authorization, Accept`.
- **SSE:** se permite el origen del frontend y `Accept: text/event-stream`. En v0 **sin** `AllowCredentials` (no hay cookies/auth). En v1, si se usan cookies, se habilita `AllowCredentials` con orígenes explícitos (no comodín).
- **Recomendación de despliegue:** el frontend consume vía **BFF same-origin** (Route Handlers), por lo que el SSE evita preflight CORS; CORS directo es el camino de respaldo/integración.

### 4.2 Autenticación
- **v0:** endpoints públicos, sin auth (FR-040, US-008).
- **v1:** `Authorization: Bearer <jwt>` (ASP.NET Core Identity + JWT). Access token corto (p. ej. 15 min) + refresh token. `401` con `WWW-Authenticate: Bearer` si falta/expira.

### 4.3 Endurecimiento
- HTTPS obligatorio (HSTS en prod), `X-Content-Type-Options: nosniff`, límite de tamaño de cuerpo, secretos nunca expuestos al cliente (FR-049, NFR-006, NFR-008).

---

# v0 — Endpoints lanzables (sin cuentas)

## 5.1 `POST /api/v1/score` — Análisis determinista

**Propósito.** Calcular el **Resultado de Análisis** para un par (CV, Vacante): puntaje global explicable, desglose por **Componentes de Puntaje**, **análisis de keywords** (presentes/faltantes/parciales) y **Recomendaciones** priorizadas. 100% determinista, sin LLM.

**Mapea:** FR-005, FR-006, FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-019, FR-021, FR-022 · US-001, US-002, US-003 · NFR-009, NFR-021.

**Rate limit:** política `score`. **Auth:** ninguna.

### Request — `ScoreCvCommand`

```json
{
  "cvText": "Desarrollador backend con 4 años en .NET. Construí APIs REST con ASP.NET Core y EF Core sobre PostgreSQL. Implementé CI/CD...",
  "jobText": "Buscamos Ingeniero Backend .NET. Requisitos: C#, ASP.NET Core, SQL, Docker. Deseable: Azure, Kubernetes."
}
```

| Campo | Tipo | Reglas (FluentValidation) |
|---|---|---|
| `cvText` | string | requerido; `200..20000` chars (FR-002, FR-037) |
| `jobText` | string | requerido; `100..20000` chars |

### Response `200 OK` — `ScoreResponse` (Resultado de Análisis)

```json
{
  "overallScore": 62,
  "band": "Coincidencia media",
  "honestyNotice": "Este puntaje mide coincidencia con esta vacante y legibilidad para sistemas automáticos. No es un \"puntaje ATS oficial\" ni garantiza empleo.",
  "engineVersion": "1.0.0",
  "lexiconVersion": "2026.06.0",
  "contextId": "9b2c1f44-0e3a-4d8b-9b6e-2a7c0d1e5f10",
  "components": [
    {
      "componentId": "match",
      "label": "Coincidencia de keywords/skills",
      "subScore": 58,
      "weight": 0.45,
      "measurementCoverage": 1.0,
      "confidence": "high",
      "explanation": "Cubres 7 de 12 requisitos. Faltan: Docker, Azure, Kubernetes."
    },
    {
      "componentId": "structure",
      "label": "Estructura parseable",
      "subScore": 80,
      "weight": 0.20,
      "measurementCoverage": 1.0,
      "confidence": "high",
      "explanation": "Se detectaron secciones de experiencia, educación y habilidades."
    },
    {
      "componentId": "achievements",
      "label": "Verbos de acción y logros cuantificados",
      "subScore": 55,
      "weight": 0.20,
      "measurementCoverage": 1.0,
      "confidence": "medium",
      "explanation": "Buen uso de verbos de acción; pocos logros con métricas."
    },
    {
      "componentId": "format",
      "label": "Formato seguro",
      "subScore": 70,
      "weight": 0.10,
      "measurementCoverage": 0.5,
      "confidence": "low",
      "explanation": "Evaluación parcial: con texto pegado no se observan columnas, tablas ni imágenes. Sube tu archivo para análisis completo."
    },
    {
      "componentId": "length",
      "label": "Longitud y densidad",
      "subScore": 90,
      "weight": 0.05,
      "measurementCoverage": 1.0,
      "confidence": "high",
      "explanation": "Longitud adecuada; densidad de keywords saludable (sin relleno abusivo)."
    }
  ],
  "keywordAnalysis": {
    "present": [
      {
        "canonicalTerm": "ASP.NET Core",
        "category": "tool",
        "sourceSection": "requisitos",
        "weight": 0.9,
        "matchLevel": "exact",
        "location": "prominent",
        "creditAwarded": 1.0,
        "evidence": "Construí APIs REST con ASP.NET Core"
      },
      {
        "canonicalTerm": "PostgreSQL",
        "category": "tool",
        "sourceSection": "requisitos",
        "weight": 0.7,
        "matchLevel": "alias",
        "location": "buried",
        "creditAwarded": 0.6,
        "evidence": "EF Core sobre PostgreSQL (mencionado en un solo proyecto)"
      }
    ],
    "missing": [
      {
        "canonicalTerm": "Docker",
        "category": "tool",
        "sourceSection": "requisitos",
        "weight": 0.8,
        "matchLevel": "none",
        "location": "absent",
        "creditAwarded": 0.0,
        "reason": "Aparece en la sección de requisitos obligatorios."
      },
      {
        "canonicalTerm": "Kubernetes",
        "category": "tool",
        "sourceSection": "deseables",
        "weight": 0.4,
        "matchLevel": "none",
        "location": "absent",
        "creditAwarded": 0.0,
        "reason": "Deseable; menor peso."
      }
    ],
    "partial": [
      {
        "canonicalTerm": "Azure",
        "category": "tool",
        "sourceSection": "deseables",
        "weight": 0.5,
        "matchLevel": "related",
        "location": "buried",
        "creditAwarded": 0.3,
        "evidence": "Mencionas \"nube\" sin especificar Azure; crédito parcial por relación."
      }
    ]
  },
  "recommendations": [
    {
      "action": "Sube \"PostgreSQL\" a tu sección de Habilidades; hoy está enterrada en un proyecto.",
      "type": "resurface",
      "targetComponent": "match",
      "estimatedImpact": 6,
      "requiresInvention": false,
      "honestyNote": "Solo reubica algo que ya está en tu CV."
    },
    {
      "action": "Añade métricas a tus logros (p. ej. \"reduje latencia 30%\").",
      "type": "addMetric",
      "targetComponent": "achievements",
      "estimatedImpact": 4,
      "requiresInvention": false,
      "honestyNote": "Usa cifras reales de tu experiencia; no inventes."
    },
    {
      "action": "Docker es un requisito ausente. Apréndelo/añádelo solo si lo cumples.",
      "type": "learnAdd",
      "targetComponent": "match",
      "estimatedImpact": 8,
      "requiresInvention": false,
      "honestyNote": "Brecha real: no se fabricará esta habilidad en la adaptación."
    }
  ],
  "formatIssues": [
    {
      "code": "few-quantified-achievements",
      "severity": "info",
      "message": "Pocos logros incluyen métricas cuantificadas."
    }
  ],
  "gatesApplied": [
    {
      "componentId": "format",
      "cap": 0.5,
      "reason": "partial-measurement",
      "message": "Formato evaluado parcialmente por entrada de solo texto (v0)."
    }
  ]
}
```

#### Diccionarios de enums (estables)

| Campo | Valores |
|---|---|
| `band` | `"Coincidencia baja"` · `"Coincidencia media"` · `"Coincidencia alta"` · `"Coincidencia muy alta"` (texto es-CO; el número es el valor rector — FR-010) |
| `componentId` | `match` · `structure` · `achievements` · `format` · `length` |
| `confidence` | `low` · `medium` · `high` |
| `category` (Requisito de Vacante) | `hardSkill` · `tool` · `softSkill` · `generic` |
| `matchLevel` (Coincidencia de Keyword) | `exact` · `alias` · `stem` · `related` · `fuzzy` · `none` |
| `location` | `prominent` · `buried` · `absent` |
| `type` (Recomendación) | `resurface` · `rewrite` · `addMetric` · `fixFormat` · `learnAdd` |
| `gatesApplied[].reason` | `partial-measurement` · `no-contact` · `no-experience` · `keyword-stuffing` (FR-012) |

#### Notas
- `measurementCoverage` (= `m_c` en research D01) declara la **medibilidad parcial** (FR-011): en v0 `format` viaja con `0.5`; el `overallScore` ya está **renormalizado** sobre el peso efectivamente medible, así que el cliente solo lo muestra, no recalcula.
- `recommendations` viene **ordenada por `estimatedImpact` descendente** (FR-021). `requiresInvention` es siempre `false` para acciones ejecutables; las brechas reales son de tipo `learnAdd` y nunca ofrecen fabricar (FR-022).
- Determinismo (FR-006): misma entrada + mismo `engineVersion`/`lexiconVersion` ⇒ respuesta byte-equivalente (salvo `contextId`, que es estable para la misma entrada).

#### Códigos de estado
| Código | Caso |
|---|---|
| `200` | Análisis calculado |
| `400` | Validación (vacío/corto) — `errors` |
| `413` | Cuerpo excede tope |
| `429` | Rate limit `score` |
| `500` | Error inesperado |

---

## 5.2 `POST /api/v1/adapt/stream` — Adaptación con IA (SSE)

**Propósito.** Generar la **Adaptación** del CV a la vacante (reordena/reescribe/prioriza lo existente, **cero invención**) y entregarla **incremental** por Server-Sent Events; al cierre, emite la **Verificación de Honestidad**.

**Mapea:** FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-029, FR-030, FR-043 · US-004, US-005, US-016 · NFR-005, NFR-010, NFR-011, NFR-021.

**Rate limit:** política `adapt`. **Auth:** ninguna en v0 (en v1 requiere JWT + consume 1 crédito, ver §6.4).

### Request — `AdaptCvCommand`

```json
{
  "cvText": "Desarrollador backend con 4 años en .NET...",
  "jobText": "Buscamos Ingeniero Backend .NET...",
  "contextId": "9b2c1f44-0e3a-4d8b-9b6e-2a7c0d1e5f10"
}
```

| Campo | Tipo | Reglas |
|---|---|---|
| `cvText` | string | requerido; `200..20000` (FR-037) |
| `jobText` | string | requerido; `100..20000` |
| `contextId` | string (GUID) \| null | opcional; si viene, reutiliza los requisitos extraídos del análisis previo para coherencia del delta (FR-031) |

### Cabeceras de petición
```
Accept: text/event-stream
Content-Type: application/json
```

### Response `200 OK` — `text/event-stream`

Cabeceras de respuesta:
```
Content-Type: text/event-stream; charset=utf-8
Cache-Control: no-cache, no-transform
X-Accel-Buffering: no
Connection: keep-alive
```

**Secuencia de eventos** (nombrados): `meta` → (`token`)\* → `honesty` → `done`. Comentarios `:` como heartbeat (keep-alive). Cada frame termina en línea en blanco (`\n\n`); `FlushAsync` por frame para streaming real.

```text
event: meta
data: {"contextId":"9b2c1f44-0e3a-4d8b-9b6e-2a7c0d1e5f10","engineVersion":"1.0.0","model":"claude-sonnet-4-6","promptVersion":"adaptation.system.v1"}

event: token
data: Lideré

event: token
data:  la migración del monolito

: heartbeat

event: token
data:  a microservicios en .NET.

event: honesty
data: {"status":"clean","potentialNewTerms":[],"note":"No se detectaron elementos fuera del CV original."}

event: done
data: {"fullText":"Lideré la migración del monolito a microservicios en .NET. ...","tokensUsed":1180,"finishReason":"stop"}
```

#### Eventos SSE

| `event` | Carga (`data`) | Significado | FR |
|---|---|---|---|
| `meta` | JSON: `contextId`, `engineVersion`, `model`, `promptVersion` | Encabezado del stream (antes del primer token) | FR-013, FR-027 |
| `token` | **texto plano** (delta) | Fragmento de la Adaptación; los saltos de línea internos se escapan a varias líneas `data:` | FR-027, NFR-010 |
| `honesty` | JSON: Verificación de Honestidad | Dictamen anti-invención tras la generación | FR-025, FR-029 |
| `done` | JSON: `fullText`, `tokensUsed`, `finishReason` | Cierre exitoso; `fullText` es la fuente canónica para copiar/exportar | FR-034 |
| `error` | JSON: ProblemDetails compacto | Fallo a mitad de stream; cierra la conexión | FR-030 |

**Verificación de Honestidad** (`event: honesty`):
```json
{
  "status": "warning",
  "potentialNewTerms": ["Kubernetes", "AWS"],
  "note": "Estos términos no se encontraron en tu CV original. Revísalos: no se incluirán como experiencia tuya."
}
```
- `status`: `clean` (sin invención) | `warning` (términos potencialmente nuevos a revisar). Política de severidad: `warning` no bloquea, pero el cliente debe destacarlo (FR-025, FR-029).

#### Cancelación (FR-028, US-005, NFR-011)
- El cliente **aborta** la conexión (`AbortController`). El backend propaga `HttpContext.RequestAborted` hasta el SDK (`CreateStreaming(...).WithCancellation(ct)`) → **detiene el consumo de tokens**. No se emite `error`; el cliente conserva lo recibido. Internamente se mapea a `499 client-closed` solo para métricas.

#### Degradación elegante (FR-030, US-016)
- Si la IA está caída **antes** del primer token (circuito abierto / timeout): se responde **HTTP** `503`/`504` con ProblemDetails (los headers SSE aún no se enviaron). El cliente muestra mensaje honesto y **mantiene** el resultado de `/score`.
- Si falla **después** de iniciar el stream: se emite `event: error` y se cierra; no se reintenta a mitad de stream (evita contenido duplicado, D08/D09).

```text
event: error
data: {"type":"https://buildcv.co/errors/ai-unavailable","title":"El servicio de IA no está disponible.","status":503}
```

#### Códigos de estado (handshake)
| Código | Caso |
|---|---|
| `200` | Stream iniciado (`text/event-stream`) |
| `400` | Validación |
| `413` | Cuerpo excede tope (antes de costo de IA — FR-037) |
| `402` | Sin créditos (solo v1) |
| `429` | Rate limit `adapt` |
| `503` | IA no disponible / circuito abierto (`Retry-After`) |
| `504` | Timeout de IA |

> El frontend, tras `event: done`, vuelve a llamar a `POST /api/v1/score` con el `fullText` adaptado y el **mismo `contextId`** para obtener el nuevo Resultado de Análisis y calcular el **Delta de Mejora** (puntaje anterior/nuevo/diferencia, requisitos resueltos vs faltantes — FR-031, FR-032, US-006). El Delta se computa **en el cliente** a partir de los dos `ScoreResponse`; el contrato no añade endpoint dedicado en v0.

---

## 5.3 `POST /api/v1/export/pdf` — Exportar CV adaptado

**Propósito.** Renderizar el CV adaptado a un PDF descargable y "ATS-friendly" (QuestPDF).

**Mapea:** FR-033 · US-007. **Rate limit:** política `score`. **Auth:** ninguna.

### Request — `ExportPdfCommand`

```json
{
  "adaptedText": "Lideré la migración del monolito a microservicios en .NET. ...",
  "fileName": "cv-adaptado.pdf",
  "template": "ats-clean"
}
```

| Campo | Tipo | Reglas |
|---|---|---|
| `adaptedText` | string | requerido; `200..40000` chars |
| `fileName` | string \| null | opcional; saneado servidor; default `cv-buildcv.pdf` |
| `template` | string \| null | opcional; enum `ats-clean` (único en v0) |

### Response `200 OK`
- Cuerpo binario `application/pdf`.
- Cabeceras:
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="cv-adaptado.pdf"
```

| Código | Caso |
|---|---|
| `200` | PDF generado |
| `400` | `adaptedText` vacío/corto |
| `413` | Texto excede tope |
| `429` | Rate limit |
| `500` | Fallo de renderizado |

> El PDF **no se persiste** (v0): se genera en memoria y se transmite (FR-040). El contenido nunca se loguea (FR-041).

---

## 5.4 `GET /health/live` · `GET /health/ready` — Salud

**Propósito.** Liveness y readiness para el orquestador/hosting. Fuera del versionado de negocio. **Auth:** ninguna. **Rate limit:** exenta.

- `GET /health/live` → `200` si el proceso responde.
- `GET /health/ready` → `200` listo / `503` no listo. v0: trivial + presencia de `ANTHROPIC_API_KEY`. v1: añade chequeo de PostgreSQL (`AddNpgSql`). **No** hace ping de pago a la IA (costo, NFR de presupuesto).

```json
{ "status": "Healthy", "checks": [ { "name": "self", "status": "Healthy" }, { "name": "ai-key", "status": "Healthy" } ] }
```

---

# v1 — Endpoints comerciales (cuentas, créditos, pagos, legal, archivos)

> Todos requieren `Authorization: Bearer <jwt>` salvo `auth/register`, `auth/login` y `webhooks/wompi`.

## 6.1 `POST /api/v1/auth/register` — Crear cuenta

**Mapea:** FR-044, FR-051 · US-012, US-015. **Auth:** ninguna.

### Request
```json
{
  "email": "zaid@example.com",
  "password": "••••••••••",
  "consent": {
    "policyVersion": "2026-06-01",
    "purposes": ["adaptacion", "historial"],
    "internationalTransferAccepted": true
  }
}
```
- `consent` es **obligatorio** y previo: sin aceptar finalidad + transferencia internacional no se crea la cuenta (FR-051). Se registra un **Consentimiento de Datos** (versión de política, finalidades, aceptación de transferencia, fecha, estado `vigente`).

### Response `201 Created`
```json
{
  "userId": "c0ffee00-1234-...",
  "email": "zaid@example.com",
  "credits": 2,
  "createdAt": "2026-06-06T15:00:00Z",
  "tokens": {
    "accessToken": "eyJhbGciOi...",
    "expiresIn": 900,
    "refreshToken": "def502...",
    "tokenType": "Bearer"
  }
}
```
- `credits: 2` = regalo de bienvenida (Movimiento de Crédito motivo `gift`, FR-046, D20).

| Código | Caso |
|---|---|
| `201` | Cuenta creada |
| `400` | Email/clave inválidos o consentimiento ausente |
| `409` | Email ya registrado |

## 6.2 `POST /api/v1/auth/login` · `POST /api/v1/auth/refresh`

### `login` — Request
```json
{ "email": "zaid@example.com", "password": "••••••••••" }
```
### `login` / `refresh` — Response `200 OK`
```json
{ "accessToken": "eyJ...", "expiresIn": 900, "refreshToken": "def502...", "tokenType": "Bearer" }
```
`refresh` — Request: `{ "refreshToken": "def502..." }`.

| Código | Caso |
|---|---|
| `200` | Autenticado / renovado |
| `400` | Cuerpo inválido |
| `401` | Credenciales o refresh inválido/expirado |

## 6.3 `GET /api/v1/credits` — Saldo y movimientos

**Mapea:** FR-046 · US-013. **Auth:** JWT.

### Response `200 OK`
```json
{
  "balance": 17,
  "movements": [
    { "id": "mv_01", "delta": 20, "reason": "purchase", "reference": "txn_abc", "createdAt": "2026-06-05T10:00:00Z" },
    { "id": "mv_02", "delta": -1, "reason": "consumption", "reference": "adapt_77", "createdAt": "2026-06-05T10:05:00Z" },
    { "id": "mv_03", "delta": 2, "reason": "gift", "reference": "welcome", "createdAt": "2026-06-05T09:59:00Z" }
  ]
}
```
- `reason` (Movimiento de Crédito): `purchase` · `gift` · `consumption`. Regla: una Adaptación = 1 crédito; saldo sin caducidad por defecto.

## 6.4 Adaptación con créditos (v1)
El endpoint `POST /api/v1/adapt/stream` (§5.2) en v1 **requiere JWT** y **descuenta 1 crédito** al iniciar la generación (tras validar saldo). Cambios respecto a v0:
- `402 insufficient-credits` si `balance < 1` (antes de costo de IA).
- Si la adaptación se cancela/falla **antes** del primer token, el crédito se **reembolsa** (Movimiento de Crédito compensatorio); si ya hubo streaming, se considera consumida.
- La Adaptación resultante se persiste en el historial (FR-045) con su Verificación de Honestidad y metadatos.

## 6.5 `GET /api/v1/adaptations` · `GET /api/v1/adaptations/{id}` — Historial

**Mapea:** FR-045 · US-012. **Auth:** JWT.

### `GET /api/v1/adaptations?page=1&pageSize=20` — Response `200 OK`
```json
{
  "items": [
    {
      "id": "adapt_77",
      "jobTitle": "Ingeniero Backend .NET",
      "scoreBefore": 62,
      "scoreAfter": 89,
      "honestyStatus": "clean",
      "createdAt": "2026-06-05T10:05:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

### `GET /api/v1/adaptations/{id}` — Response `200 OK`
Detalle con `cvOriginalText`, `adaptedText`, `jobText`, el `Delta de Mejora` completo (puntaje origen/destino, diferencia por componente, requisitos resueltos vs faltantes) y la Verificación de Honestidad.

| Código | Caso |
|---|---|
| `200` | OK |
| `401` | Sin token |
| `403` | La adaptación no pertenece al usuario |
| `404` | No existe |

## 6.6 `POST /api/v1/payments/wompi/checkout` — Iniciar compra

**Propósito.** Crear los datos de un **Web Checkout** de Wompi para comprar un paquete de créditos, generando la **firma de integridad SHA256 en el servidor** (nunca expone el *integrity secret*).

**Mapea:** FR-047, FR-049 · US-013 · NFR-008. **Auth:** JWT.

### Request
```json
{ "packageId": "busqueda_20" }
```
| `packageId` | Créditos | Monto (COP) |
|---|---|---|
| `starter_5` | 5 | 9900 |
| `busqueda_20` | 20 | 27900 |
| `semana_pase` | acceso 7 días | 12900 |

### Response `200 OK`
```json
{
  "reference": "BUILDCV-7f3a9c2e",
  "amountInCents": 2790000,
  "currency": "COP",
  "publicKey": "pub_prod_xxx",
  "signatureIntegrity": "37c8407747e595535433ef8f6a811d853cd943046624a0ec04662b17bbf33bf5",
  "redirectUrl": "https://buildcv.co/pagos/retorno",
  "checkoutUrl": "https://checkout.wompi.co/p/?public-key=pub_prod_xxx&currency=COP&amount-in-cents=2790000&reference=BUILDCV-7f3a9c2e&signature%3Aintegrity=37c8..."
}
```
- `signatureIntegrity` = `SHA256("<reference><amountInCents><currency><integritySecret>")`, hex minúsculas, calculado **server-side** (FR-049). El `integritySecret` jamás viaja al cliente.
- `amountInCents` en **centavos** (COP $27.900 → `2790000`).
- Se crea una **Transacción de Pago** local en estado `pending` (referencia `BUILDCV-...`). **No** se acreditan créditos aquí.

| Código | Caso |
|---|---|
| `200` | Checkout preparado |
| `400` | `packageId` inválido |
| `401` | Sin token |

## 6.7 `POST /api/v1/webhooks/wompi` — Confirmación de pago

**Propósito.** Recibir el evento firmado de Wompi (`transaction.updated`), **verificar la firma**, y acreditar créditos de forma **idempotente**. Única fuente de verdad (no el `redirect-url`).

**Mapea:** FR-048 · US-013 · NFR-007. **Auth:** ninguna (autenticidad por **firma del evento**). **Rate limit:** exenta (fuera del versionado de negocio). **Idempotencia:** por `transaction.id` + estado.

### Request (lo envía Wompi)
```json
{
  "event": "transaction.updated",
  "data": { "transaction": { "id": "1234-1610...", "status": "APPROVED", "amount_in_cents": 2790000, "reference": "BUILDCV-7f3a9c2e", "currency": "COP" } },
  "sent_at": "2026-06-06T16:45:05.000Z",
  "timestamp": 1749250800,
  "signature": {
    "properties": ["transaction.id", "transaction.status", "transaction.amount_in_cents"],
    "checksum": "3476DDA50F64CD7CBD160689640506FEBEA93239BC524FC0469B2C68A3CC8BD0"
  }
}
```

**Algoritmo de verificación (regla dura):**
1. Leer `signature.properties` **del payload** (NO hardcodear; puede variar por evento), en ese orden, concatenar los valores tomados de `data`.
2. Concatenar el `timestamp` del evento.
3. Concatenar el **Secreto de Eventos** (`prod_events_...`, distinto de la llave y del integrity secret).
4. `SHA256(concatenación)` y comparar (case-insensitive) contra `signature.checksum`.
5. Si **no** coincide → `401 webhook-invalid-signature` y se **descarta** el evento sin tocar la BD.
6. Si coincide y `status == APPROVED`: confirmar contra `GET /v1/transactions/{id}` de Wompi, marcar la Transacción de Pago `approved`, **acreditar créditos** (Movimiento `purchase`) **una sola vez** (idempotente por `transaction.id`). `DECLINED`/`VOIDED`/`ERROR` → registrar estado, **no** acreditar.

### Response
```json
{ "received": true }
```
- Responder **`200` rápido e idempotente** aun ante reintentos/duplicados de Wompi (un mismo `transaction.id` no acredita dos veces).

| Código | Caso |
|---|---|
| `200` | Evento recibido (firma válida; o duplicado ya procesado) |
| `400` | Payload malformado |
| `401` | Firma inválida → evento descartado |

## 6.8 Consentimiento y derechos ARCO

**Mapea:** FR-051, FR-052, FR-053 · US-015 · NFR-023, NFR-024. **Auth:** JWT.

| Método | Ruta | Propósito |
|---|---|---|
| `POST` | `/api/v1/consent` | Otorgar/actualizar Consentimiento de Datos (versión de política, finalidades, transferencia internacional) |
| `POST` | `/api/v1/consent/revoke` | Revocar consentimiento (estado → `revocado`; detiene tratamiento) |
| `GET`  | `/api/v1/me/data` | Acceso: exporta los datos personales del titular (CVs, vacantes, adaptaciones) |
| `DELETE` | `/api/v1/me` | Supresión: elimina la cuenta y sus datos, dejando constancia |

### `POST /api/v1/consent` — Request
```json
{ "policyVersion": "2026-06-01", "purposes": ["adaptacion", "historial"], "internationalTransferAccepted": true }
```
### `GET /api/v1/me/data` — Response `200 OK`
```json
{
  "user": { "userId": "c0ffee00-...", "email": "zaid@example.com", "createdAt": "2026-06-01T00:00:00Z" },
  "consent": { "policyVersion": "2026-06-01", "status": "vigente", "internationalTransferAccepted": true, "grantedAt": "2026-06-01T00:00:00Z" },
  "adaptations": [ { "id": "adapt_77", "createdAt": "2026-06-05T10:05:00Z" } ]
}
```
- Toda acción de derechos deja **constancia auditable** (fecha, tipo). La revocación/supresión detiene el tratamiento conforme a la solicitud (FR-052).

| Código | Caso |
|---|---|
| `200`/`202` | Solicitud atendida (supresión puede ser asíncrona → `202`) |
| `401` | Sin token |

## 6.9 `POST /api/v1/cv/parse` — Subir y extraer CV (PDF/DOCX)

**Propósito.** Extraer el texto de un archivo subido para alimentar `/score` y `/adapt`, y habilitar evaluación de formato **completa** (`measurementCoverage = 1.0` para `format`).

**Mapea:** FR-054, FR-055 · US-014. **Auth:** JWT. **Content-Type:** `multipart/form-data`.

### Request (`multipart/form-data`)
| Parte | Tipo | Reglas |
|---|---|---|
| `file` | binario | requerido; `application/pdf` o `.docx`; tamaño máx. (p. ej. 5 MB) |

### Response `200 OK`
```json
{
  "cvText": "Texto extraído del documento...",
  "inputMode": "fileUpload",
  "detectedSections": ["experiencia", "educacion", "habilidades"],
  "formatObservations": [
    { "code": "multi-column", "severity": "warning", "message": "Se detectaron 2 columnas; algunos sistemas leen el orden incorrectamente." },
    { "code": "image-text", "severity": "warning", "message": "Hay texto dentro de imágenes que un sistema automático no puede leer." }
  ]
}
```
- `inputMode`: `fileUpload` (vs `pastedText` en v0) — atributo del CV (Hoja de Vida).
- El `cvText` extraído se reenvía a `/score` y `/adapt` igual que el texto pegado; las `formatObservations` se incorporan al componente `format` con cobertura plena (FR-055).

| Código | Caso |
|---|---|
| `200` | Extraído |
| `400` | Sin archivo |
| `413` | Archivo excede tamaño |
| `415` | Formato no soportado / archivo corrupto/protegido (`unsupported-media`) — el cliente ofrece pegar texto |

---

## 7. Matriz de trazabilidad endpoint → requisitos

| Endpoint | FR cubiertos | US | NFR |
|---|---|---|---|
| `POST /api/v1/score` | FR-005..FR-019, FR-021, FR-022 | US-001, US-002, US-003 | NFR-009, NFR-021 |
| `POST /api/v1/adapt/stream` | FR-023..FR-030, FR-043 | US-004, US-005, US-016 | NFR-005, NFR-010, NFR-011, NFR-021 |
| (cliente) Delta de Mejora vía 2× `/score` | FR-031, FR-032 | US-006 | — |
| `POST /api/v1/export/pdf` | FR-033 | US-007 | NFR-001 |
| `GET /health/*` | — | — | NFR-018 |
| Rate limit + headers (todos) | FR-036, FR-037, FR-038, FR-039 | US-011 | NFR-006 |
| Privacidad transversal (sin logs de contenido, sin persistencia v0) | FR-040, FR-041, FR-042 | US-008 | NFR-001, NFR-002, NFR-022 |
| `POST /api/v1/auth/register` · `login` · `refresh` | FR-044 | US-012 | — |
| `GET /api/v1/credits` · adaptación con créditos | FR-046 | US-013 | — |
| `GET /api/v1/adaptations[/{id}]` | FR-045 | US-012 | NFR-004 |
| `POST /api/v1/payments/wompi/checkout` | FR-047, FR-049 | US-013 | NFR-008 |
| `POST /api/v1/webhooks/wompi` | FR-048 | US-013 | NFR-007 |
| consentimiento + ARCO | FR-051, FR-052, FR-053 | US-015 | NFR-023, NFR-024 |
| `POST /api/v1/cv/parse` | FR-054, FR-055 | US-014 | — |

> **FR-020 (enriquecimiento de keywords con IA, P1):** cuando se implemente, se expondrá como campo **separado** en `ScoreResponse` (p. ej. `aiSuggestedKeywords[]`) marcado `"source": "ai"`, sin alterar `overallScore` (determinista). **FR-050 (comprobante tributario):** el flujo de factura electrónica DIAN se integra fuera de este contrato HTTP (proveedor de facturación), referenciando la Transacción de Pago.

---

## 8. Notas de implementación (puente a `plan.md` / `tasks.md`)
- **Determinismo y sellado:** `engineVersion` + `lexiconVersion` viajan en cada `ScoreResponse`; congelar su formato antes de publicar el cliente TS (FR-006, FR-013).
- **Contrato SSE congelado:** eventos `meta` / `token` / `honesty` / `done` / `error`; `data` de `token` es **texto plano**, el resto **JSON**. El BFF de Next.js hace passthrough del `ReadableStream` sin bufferizar (D14).
- **Headers de rate limit** (`X-RateLimit-*`, `Retry-After`) y errores `application/problem+json` deben quedar idénticos entre backend y frontend (D13 riesgo de sincronización).
- **Secretos:** integrity/events secrets de Wompi y `ANTHROPIC_API_KEY` solo en servidor; el checkout entrega `publicKey` + `signatureIntegrity`, nunca secretos (FR-049, NFR-008).
- El documento OpenAPI generado por `AddOpenApi()` es la fuente del cliente TypeScript; este contrato y ese documento deben mantenerse alineados.
