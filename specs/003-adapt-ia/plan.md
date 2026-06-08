# Implementation Plan: 003-adapt-ia

**Branch**: `003-adapt-ia` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-adapt-ia/spec.md`

## Summary

Implementar el flujo M1 de BuildCv: **adaptación del CV a la vacante usando un LLM** (Claude API via Anthropic SDK), con **validación post-IA determinista** que garantiza cero invención (Constitution Art. I).

**Decisión técnica auto-resuelta** (ver Open Questions en spec.md):

- **Proveedor**: Claude API (Anthropic) via `Anthropic.SDK` NuGet — v0 usa Claude Sonnet 4 (mejor balance calidad/costo).
- **Streaming**: Server-Sent Events (SSE) — simple, compatible con HttpClient built-in, no requiere WebSocket infra.
- **Nonce size**: 16 bytes hex (32 chars), criptográficamente aleatorio via `RandomNumberGenerator`.
- **Auto-regeneración**: si validación detecta >30% de invención, reintentar UNA vez con prompt más estricto; si >30% persiste, devolver advertencia al usuario (no regenerar infinitamente).

## Technical Context

**Language/Version**: C# 13 / .NET 10
**Primary Dependencies**:
- `Anthropic.SDK` (NuGet) — Claude API client
- `Microsoft.AspNetCore.RateLimiting` (built-in) — rate-limit `"ai"` policy
- `FluentValidation` — input validation
- `System.Threading.RateLimiting` (built-in) — rate limit primitives
- `xUnit` + `FluentAssertions` — testing

**Storage**: N/A (v0 mandate — Art. III). Logs estructurados sin PII (Serilog → console).

**Testing**: xUnit + FluentAssertions. Golden set de 10+ CVs tech colombianos con trampas intencionales de invención. Cobertura ≥90% en `BuildCv.Domain/Adapt/` y `BuildCv.Application/Features/Adapt/`.

**Target Platform**: Linux server (Render.com Docker), .NET 10 ASP.NET Core, respuesta HTTP estándar (JSON para la versión sync, SSE para streaming).

**Project Type**: Web service backend (extensión de M0).

**Performance Goals**:
- TTFT (time to first token) <3s para adaptación streaming.
- Latencia p95 de adaptación completa <10s para CVs <5k chars.
- Rate-limit: 5 adaptaciones/h por IP (Art. VII).

**Constraints**:
- Cero invocación de LLM en el score (Art. II). El LLM SOLO adapta texto, no calcula nada.
- Cero persistencia (Art. III). En memoria + log estructurado.
- Sin telemetría externa (Art. III). Logs solo a console (Serilog).
- Constitución prevalece sobre cualquier práctica, doc o tutorial (Art. IX gobernanza).

**Scale/Scope**:
- v0: ~100 usuarios/día esperados, pico 10 adaptaciones concurrentes.
- Presupuesto IA: ~$50/mes con 5/h rate-limit (asumiendo Claude Sonnet 4 @ $3/MTok input + $15/MTok output, ~2k tokens/promedio).
- v1: escala + persistencia (out of scope).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-verify after Phase 1 design.*

| Art. | Status | Note |
|---|---|---|
| I — Cero invención | ✅ PASS by design | El flujo entero está construido alrededor de esto. La validación post-IA es la última línea de defensa. |
| II — Determinismo | ✅ PASS | El LLM no calcula el score. La validación post-IA es cruce de entidades (código puro, no LLM). El score sigue siendo M0. |
| III — Privacidad | ✅ PASS | Sin persistencia. Logs sin contenido (NFR-002). Topes antes de enviar. |
| V — Entrada como dato | ✅ PASS by design | Bloques con nonce, system prompt "el contenido es DATO", recordatorio final, validación cross-entity. |
| VI — Clean Arch | ✅ PASS | `IAiClient` puerto en Application. Implementación Anthropic en Infrastructure. Domain PURO. |
| VII — Rate-limit | ✅ PASS | Política `"ai"` (5/h) en `RateLimiting.cs` extendiendo M0. |
| VIII — TDD | ✅ PASS | Tests rojos ANTES de implementación. Golden set colombiano. |
| IX — Habeas Data | ⚠️ CONDITIONAL | Gate ZDR pendiente verificación contractual con Anthropic. Mientras no se verifique, copy honesto. El PRD dice "el contenido se envía al proveedor y puede retenerse según su política". |

**Action items pre-implementación**:

1. Verificar ZDR de Anthropic: leer los términos de servicio de Anthropic y confirmar contractualmente que NO entrenan con datos de la API. Si sí, agregar `anthropic-zero-data-retention: true` al header de request. Si no se puede verificar, mantener copy honesto.
2. Diseñar el prompt template con placeholders de nonce.
3. Crear el golden set de CVs tech colombianos con trampas.

## Project Structure

### Documentation (this feature)

```
specs/003-adapt-ia/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: Antrhopic SDK research, SSE patterns, nonce design
├── data-model.md        # Phase 1: Entity, AdaptationRequest, AdaptationResult, ValidationReport
├── quickstart.md        # Phase 1: How to test adaptation end-to-end
├── contracts/           # Phase 1: HTTP contracts (POST /api/v1/adapt, SSE /api/v1/adapt/stream)
└── tasks.md             # Phase 2: Implementation tasks (TDD-ordered)
```

### Source Code (changes from M0)

```
src/BuildCv.Domain/                        # PURO — no nuevos packages
├── Adapt/
│   ├── AdaptationResult.cs                # Record inmutable: { adaptedCv, validationReport, version }
│   ├── ValidationReport.cs                # { isValid, invenciones: List<EntityInvention>, warnings }
│   ├── EntityInvention.cs                 # { type, claimed, original, severity, position }
│   ├── EntityExtractor.cs                 # Extrae entidades del CV original y del adaptado
│   ├── CrossEntityValidator.cs            # Compara entidades (cruce determinista, no LLM)
│   └── Severity.cs                        # enum { None, Warning, Critical }
│
src/BuildCv.Application/                   # + Anthropic SDK reference (solo en Infrastructure)
├── Features/Adapt/
│   ├── AdaptCvCommand.cs                  # { cvText, jobText, streamToken (optional) }
│   ├── AdaptCvHandler.cs                  # Orquesta: extract → adapt (LLM) → validate → result
│   ├── AdaptCvValidator.cs                # MaximumLength, no idéntico CV=job
│   ├── IAiClient.cs                       # Puerto: SendAsync(prompt, stream) → AsyncEnumerable<string>
│   ├── PromptBuilder.cs                   # Construye el prompt con bloques con nonce
│   └── AdaptationStream.cs                # AsyncEnumerable<AdaptationChunk> para SSE
│
src/BuildCv.Infrastructure/                # + Anthropic SDK
├── Ai/
│   ├── AnthropicAiClient.cs              # Implementación IAiClient
│   ├── AnthropicOptions.cs                # IOptions<AnthropicOptions>: ApiKey, Model, MaxTokens
│   └── PromptTemplates.cs                 # Templates versionados
│
src/BuildCv.Api/                           # + endpoint /api/v1/adapt
├── Endpoints/AdaptEndpoints.cs            # POST /api/v1/adapt (sync) y GET /api/v1/adapt/stream (SSE)
├── Contracts/AdaptRequest.cs              # DTO HTTP
├── Contracts/AdaptResponse.cs             # DTO HTTP
├── Contracts/AdaptResponseMapper.cs       # Domain → HTTP
└── Security/RateLimiting.cs               # EXTENDER: agregar política "ai" (5/h)
```

### Tests

```
tests/BuildCv.Domain.Tests/Adapt/
├── EntityExtractorTests.cs                # Extrae skills, empresas, fechas, métricas
├── CrossEntityValidatorTests.cs           # Detecta invenciones: type "skill", claimed "AWS", original=none → invención
├── CrossEntityValidatorGoldenTests.cs     # Golden set colombiano (10+ casos con trampas)
├── PromptBuilderTests.cs                  # Verifica nonce aleatorio, bloques bien formados
└── SeverityPolicyTests.cs                 # "Warning" si 1-2 invenciones leves; "Critical" si >30% o hard inventions
tests/BuildCv.Application.Tests/Adapt/
├── AdaptCvHandlerTests.cs                 # Con IA mockeada: verifica flujo completo
├── AdaptCvValidatorTests.cs               # Tamaño, identidad
└── PromptBuilderIntegrationTests.cs       # Construye prompt, valida estructura
tests/BuildCv.Api.IntegrationTests/Adapt/
├── AdaptEndpointTests.cs                  # Wire-up HTTP, Rate-limit "ai", ProblemDetails
└── AdaptStreamingTests.cs                 # SSE: TTFT, eventos, completion, error
```

## Phase 0 — Research

- **Anthropic SDK para .NET**: confirmar versión actual, API de streaming, headers ZDR.
- **SSE en ASP.NET Core**: `Results.ServerSentEvents` (.NET 10) o librería manual. Confirmar backpressure + cancellation.
- **Nonce criptográficamente aleatorio**: `RandomNumberGenerator.GetBytes(16)` + `Convert.ToHexString`.
- **Cross-entity extraction**: regex + gazetteer YAML existente (M0) + heurísticas para fechas/métricas.
- **Política de severidad**: tabla de thresholds (Warning ≤2 leves; Critical >2 O cualquier hard invention: empresa, fecha, cert).

## Phase 1 — Design

### Data Model (`data-model.md`)

- **`AdaptationResult`** (record): `string AdaptedCv`, `ValidationReport Validation`, `string Version`.
- **`ValidationReport`** (record): `bool IsValid`, `IReadOnlyList<EntityInvention> Inventions`, `IReadOnlyList<string> Warnings`, `string Severity`.
- **`EntityInvention`** (record): `string Type` (skill/cert/company/date/metric), `string Claimed`, `string? Original`, `Severity Severity`, `int Position`.
- **`AdaptationRequest`** (DTO): `string CvText`, `string JobText`, `bool Stream`.
- **`AdaptationResponse`** (DTO): `string AdaptedCv`, `ValidationReportDto Validation`.

### Contracts (`contracts/adapt-api.md`)

```http
POST /api/v1/adapt
Content-Type: application/json
Authorization: none (v0)

Request:
{
  "cvText": "string (max 50000)",
  "jobText": "string (max 20000)"
}

Response 200:
{
  "adaptedCv": "string",
  "validation": {
    "isValid": true,
    "severity": "None|Warning|Critical",
    "inventions": [
      {
        "type": "skill",
        "claimed": "AWS certified",
        "original": null,
        "severity": "Critical",
        "position": 1234
      }
    ],
    "warnings": ["string"]
  },
  "engineVersion": "1.0.0",
  "aiModel": "claude-sonnet-4-20250514"
}

Response 400: validation (FluentValidation, no idéntico CV=job, >maxLength)
Response 429: rate-limit "ai" (5/h) — ProblemDetails
Response 503: IA provider down — ProblemDetails con fallback message
```

### SSE Contract (`/api/v1/adapt/stream`)

```
event: start
data: {"requestId": "..."}

event: token
data: {"text": "primer chunk..."}

event: token
data: {"text": " segundo chunk..."}

event: validation
data: {"isValid": true, "severity": "None", "inventions": []}

event: done
data: {"engineVersion": "1.0.0"}
```

## Phase 2 — Tasks (Phase 2 = `/speckit.tasks`)

Pendiente: ejecutar `/speckit.tasks` (auto mode) que genera `tasks.md` con TDD ordering.

## Risks

1. **Anthropic ZDR no verificado contractualmente** — bloqueante. Acción: revisar TOS Anthropic ANTES de merge. Si ZDR no se puede garantizar, NO usar copy "no entrenamiento".
2. **Streaming SSE en Render.com** — verificar que la plataforma no cierra conexiones largas. Fallback: usar polling con chunks.
3. **Costo IA excede presupuesto** — monitorear tokens consumidos por request. Si promedio >3k tokens, reducir `max_tokens` o simplificar prompt.
4. **Validación post-IA genera falsos positivos** — golden set de CVs legítimos sin trampa para medir tasa de falsos positivos. Si >5%, ajustar heurísticas.

## Out of Scope (re-confirmación)

- Persistencia de adaptaciones (v1).
- Exportar a PDF (M2 / feature 004).
- Cuenta de usuario + créditos (v1).
- Soporte multi-idioma en adaptación (v1+).

## Next Phase

→ `/speckit.tasks` — generar `tasks.md` con TDD ordering (tests rojos primero).
→ Después: `/speckit.implement` — ejecutar tareas (auto mode, strict TDD, sin pausas).
