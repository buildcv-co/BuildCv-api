# Implementation Plan: 003-adapt-ia

**Branch**: `003-adapt-ia` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Commit**: `68baaf2` "feat(003-adapt-ia): adaptación con LLM, cero invención (Constitution Art. I)"

**Input**: Feature specification from `specs/003-adapt-ia/spec.md`

> **⚠️ Reality check — divergencia con el spec original.** El plan original proponía Anthropic SDK + Claude Sonnet 4 + SSE streaming + auto-regen. La implementación shipped (commit `68baaf2`) rechazó esa arquitectura en favor de un `StubAiClient` determinista (sin LLM real). El flujo de validación post-IA (cruce de entidades, severidad, bloques con nonce en el prompt) sí se implementó completo y se mantiene como antes. El "stub" es intercambiable en el futuro por una implementación real detrás del mismo puerto `IAiClient`. Ver `research.md` para el banner histórico y `tasks.md` para las tasks marcadas como "implemented as StubAiClient".

## Summary

Implementar el flujo M1 de BuildCv: **adaptación del CV a la vacante** con **validación post-IA determinista** que garantiza cero invención (Constitution Art. I). En v0 el "proveedor de IA" es un `StubAiClient` que retorna una versión "marco" del CV original sin agregar contenido — el resto del flujo (validación, severidad, endpoint, rate-limit, bloques con nonce) opera normalmente.

**Decisión técnica efectiva** (ver `research.md` para el histórico):

- **Proveedor v0**: `StubAiClient` determinista (sin LLM real). Puerto `IAiClient` con un único método `CompleteAsync(prompt, ct)` — listo para reemplazar por Anthropic/OpenAI en v1 detrás del mismo puerto.
- **Sin streaming** en v0: el endpoint es sincrónico (`POST /api/v1/adapt`). NO existe `/api/v1/adapt/stream`.
- **Sin auto-regen**: el handler es lineal (extract → call LLM → validate → return). NO hay loop de reintento por severidad.
- **Nonce size**: 16 bytes hex (32 chars), criptográficamente aleatorio via `RandomNumberGenerator` (defensa contra prompt-injection, Constitution Art. V).
- **Sin telemetría externa** (Constitution Art. III, IX — gate ZDR pendiente verificación contractual).

## Technical Context

**Language/Version**: C# 13 / .NET 10
**Primary Dependencies**:
- `Microsoft.AspNetCore.RateLimiting` (built-in) — rate-limit `"ai"` policy
- `FluentValidation` — input validation
- `System.Threading.RateLimiting` (built-in) — rate limit primitives
- `xUnit` + `FluentAssertions` — testing
- (NO se usa `Anthropic.SDK` — la implementación shipped es `StubAiClient` en `src/BuildCv.Infrastructure/Ai/`)

**Storage**: N/A (v0 mandate — Art. III). Logs estructurados sin PII (Console.WriteLine con metadatos).

**Testing**: xUnit + FluentAssertions. Golden set de CVs tech colombianos con trampas intencionales de invención. Cobertura ≥90% en `BuildCv.Domain/Adapt/` y `BuildCv.Application/Features/Adapt/`.

**Target Platform**: Linux server (Render.com Docker), .NET 10 ASP.NET Core, respuesta HTTP JSON sincrónica.

**Project Type**: Web service backend (extensión de M0).

**Performance Goals**:
- Latencia p95 de adaptación completa <10s para CVs <5k chars (con el stub: <100ms, ya que no hay IO de red).
- Rate-limit: 5 adaptaciones/h por IP (Art. VII).

**Constraints**:
- Cero invocación de LLM en el score (Art. II). El "LLM" (stub) SOLO adapta texto, no calcula nada.
- Cero persistencia (Art. III). En memoria + log estructurado.
- Sin telemetría externa (Art. III). Logs solo a console.
- Constitución prevalece sobre cualquier práctica, doc o tutorial (Art. IX gobernanza).

**Scale/Scope**:
- v0: ~100 usuarios/día esperados, pico 10 adaptaciones concurrentes.
- Presupuesto IA: $0 en v0 (stub). En v1 con Anthropic real, ~$50/mes con 5/h rate-limit.
- v1: escala + persistencia (out of scope).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-verify after Phase 1 design.*

| Art. | Status | Note |
|---|---|---|
| I — Cero invención | ✅ PASS by design | El flujo entero está construido alrededor de esto. La validación post-IA (CrossEntityValidator + SeverityPolicy) es la última línea de defensa. El StubAiClient, al no agregar contenido, satisface Art. I por construcción. |
| II — Determinismo | ✅ PASS | El LLM (stub) no calcula el score. La validación post-IA es cruce de entidades (código puro, no LLM). El score sigue siendo M0. |
| III — Privacidad | ✅ PASS | Sin persistencia. Logs sin contenido (solo metadatos: longitudes, severidad, conteo de invenciones, traceId). Topes antes de enviar. |
| V — Entrada como dato | ✅ PASS by design | Bloques con nonce, system prompt "el contenido es DATO", recordatorio final, validación cross-entity. El PromptBuilder genera prompts defendidos aunque el receptor actual sea un stub. |
| VI — Clean Arch | ✅ PASS | `IAiClient` puerto en `Application/Features/Adapt/`. Implementación `StubAiClient` en `Infrastructure/Ai/`. Domain PURO. |
| VII — Rate-limit | ✅ PASS | Política `"ai"` (5/h) en `RateLimiting.cs:29-37`. |
| VIII — TDD | ✅ PASS | Tests rojos ANTES de implementación. Golden set colombiano. |
| IX — Habeas Data | ⚠️ CONDITIONAL | Gate ZDR pendiente verificación contractual con Anthropic. En v0, irrelevante (stub sin red). Copy honesto: "el contenido se envía al proveedor y puede retenerse según su política" sigue siendo la regla cuando se habilite el LLM real. |

## Project Structure

### Documentation (this feature)

```
specs/003-adapt-ia/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: research (incluye banner HISTÓRICO sobre decisiones rechazadas)
├── data-model.md        # Phase 1: tipos de dominio + Application
├── quickstart.md        # Phase 1: cómo probar la adaptación end-to-end
├── contracts/           # Phase 1: contratos HTTP (POST /api/v1/adapt sincrónico)
└── tasks.md             # Phase 2: tasks de implementación (TDD-ordered)
```

### Source Code (shipped, commit 68baaf2)

```
src/BuildCv.Domain/Adapt/                  # PURO — sin packages externos
├── AdaptationTypes.cs                     # Combined: InventionType, InventionSeverity, Severity, EntityInvention, ValidationReport, AdaptationResult
├── EntityExtractor.cs                     # Extrae entidades del CV (regex + gazetteer M0)
├── CrossEntityValidator.cs                # Compara entidades (cruce determinista, no LLM)
└── SeverityPolicy.cs                      # Clasifica severidad final (None/Warning/Critical)

src/BuildCv.Application/Features/Adapt/
├── AdaptCvCommand.cs                      # Record: { CvText, JobText } (sin Stream)
├── IAiClient.cs                           # Puerto: Task<string> CompleteAsync(prompt, ct) — sin streaming
├── PromptBuilder.cs                       # Construye prompt con bloques delimitados por nonce criptográfico
├── AdaptCvValidator.cs                    # FluentValidation: tamaño máximo
└── AdaptCvHandler.cs                      # Orquesta: extract → IAiClient → validate → result (lineal, sin reintentos)

src/BuildCv.Infrastructure/Ai/
└── StubAiClient.cs                        # Implementación IAiClient v0 — retorna "CV marco" sin agregar contenido (NO LLM real)

src/BuildCv.Api/Endpoints/
└── AdaptEndpoints.cs                      # POST /api/v1/adapt (sync); RequireRateLimiting("ai")

src/BuildCv.Api/Contracts/
└── AdaptContracts.cs                      # DTOs HTTP + AdaptResponseMapper
```

> **Diferencias con el plan original** (todas revertidas en favor de la simplicidad v0):
> - ~~`Anthropic.SDK`~~ → NO se usa. La implementación shipped es `StubAiClient`.
> - ~~`AnthropicAiClient.cs` + `AnthropicOptions.cs`~~ → NO existen. Se sustituirán en v1 cuando se habilite un LLM real.
> - ~~`AdaptationResult.cs`, `ValidationReport.cs`, `EntityInvention.cs`, `Severity.cs`~~ → combinados en `AdaptationTypes.cs`.
> - ~~SSE `/api/v1/adapt/stream`~~ → NO existe. Endpoint es sincrónico.
> - ~~Auto-regen con prompt estricto~~ → NO existe. El handler es lineal.

### Tests

```
tests/BuildCv.Domain.Tests/Adapt/
├── EntityExtractorTests.cs                # Extrae skills, empresas, fechas, métricas, certificaciones, títulos
├── CrossEntityValidatorTests.cs           # Detecta invenciones, valida cruces
└── SeverityPolicyTests.cs                 # Clasifica severidad: 0 → None, 1-2 soft → Warning, ≥3 soft o 1+ hard → Critical

tests/BuildCv.Application.Tests/Adapt/
├── PromptBuilderTests.cs                  # Verifica nonce aleatorio, bloques bien formados
├── AdaptCvValidatorTests.cs               # Tamaño máximo
└── AdaptCvHandlerTests.cs                 # Con StubAiClient mock: verifica flujo completo (extract → LLM → validate → result)
```

> **Tests descartados** (vs. plan original): `CrossEntityValidatorGoldenTests.cs` (la cobertura del golden set está distribuida en `CrossEntityValidatorTests.cs` + `AdaptCvHandlerTests.cs`), `AdaptStreamingTests.cs` (no hay SSE), `AdaptStreamingTests.cs`, `PromptBuilderIntegrationTests.cs` (la cobertura está en `PromptBuilderTests.cs`).

## Phase 0 — Research

Ver `research.md` para el detalle completo. Resumen ejecutivo:

- **Proveedor IA v0**: `StubAiClient` determinista. Sin red, sin API key, sin ZDR pendiente. La arquitectura detrás del puerto `IAiClient` está lista para reemplazar el stub por una implementación real (Anthropic.SDK, OpenAI SDK, etc.) en v1 sin tocar Domain/Application.
- **Validación post-IA**: `EntityExtractor` + `CrossEntityValidator` + `SeverityPolicy`. Es la defensa duradera contra invenciones (independiente del proveedor de IA).
- **Nonces criptográficos**: `RandomNumberGenerator.GetBytes(16)` + `Convert.ToHexString`. Defendible aunque el receptor sea un stub.
- **Heurísticas de extracción**: regex + el `ISkillGazetteer` de M0 (skills); regex puro para empresas/fechas/métricas; lista hardcoded para certificaciones.
- **Severidad**: `0 inventions → None`, `1-2 soft → Warning`, `≥3 soft o 1+ hard → Critical`.

## Phase 1 — Design

### Data Model (`data-model.md`)

- **`AdaptationResult`** (record, en `AdaptationTypes.cs`): `string AdaptedCv`, `ValidationReport Validation`, `string EngineVersion`, `string AiModel`.
- **`ValidationReport`** (record): `bool IsValid`, `Severity Severity`, `IReadOnlyList<EntityInvention> Inventions`, `IReadOnlyList<string> Warnings`.
- **`EntityInvention`** (record): `InventionType Type`, `string Claimed`, `string? Original`, `InventionSeverity InventionSeverity`, `int Position`.
- **`AdaptCvCommand`** (DTO, Application): `string CvText`, `string JobText` (sin `Stream`).
- **`IAiClient`** (puerto, Application): `Task<string> CompleteAsync(string prompt, CancellationToken ct)`.

### Contracts (`contracts/adapt-api.md`)

```http
POST /api/v1/adapt
Content-Type: application/json

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
    "inventions": [...],
    "warnings": [...]
  },
  "engineVersion": "1.0.0",
  "aiModel": "claude-sonnet-4-20250514"  // string que StubAiClient reporta por consistencia del contrato
}

Response 400: validation (FluentValidation, maxLength)
Response 429: rate-limit "ai" (5/h) — ProblemDetails
Response 503: IA provider down — ProblemDetails con fallback message
```

> **No existe** `GET /api/v1/adapt/stream` ni endpoint SSE en v0. El spec original lo mencionaba pero la implementación shipped no lo incluye.

## Phase 2 — Tasks (Phase 2 = `/speckit.tasks`)

Ver `tasks.md`. Las tasks marcadas como completadas reflejan el código shipped en el commit `68baaf2`. Las tasks que originalmente proponían `Anthropic.SDK` / SSE / auto-regen están marcadas con nota "implemented as StubAiClient (deterministic, no LLM) for v0".

## Risks

1. **Stub no es un LLM real** — el "AI" en v0 es un retorno determinista. Riesgo: usuarios que esperan mejoras reales del CV pueden sorprenderse. Mitigación: copy honesto en frontend ("CV optimizado — vista previa") + log claro en metadata. La arquitectura detrás del puerto permite reemplazar el stub en v1.
2. **ZDR no verificado contractualmente** — bloqueante para v1. Acción: revisar TOS Anthropic ANTES de merge v1. Si ZDR no se puede garantizar, NO usar copy "no entrenamiento".
3. **Validación post-IA genera falsos positivos** — golden set de CVs legítimos sin trampa para medir tasa de falsos positivos. Si >5%, ajustar heurísticas.
4. **Sin SSE, el cliente espera respuesta completa** — UX menos progresiva que el spec original. Aceptable para v0 (respuesta típica <100ms con el stub); v1 con LLM real puede reintroducir SSE con el patrón `Results.ServerSentEvents` de .NET 10.

## Out of Scope (re-confirmación)

- Persistencia de adaptaciones (v1).
- Streaming SSE (v1, con LLM real).
- Cuenta de usuario + créditos (v1).
- Soporte multi-idioma en adaptación (v1+).
- Proveedor de IA real distinto al stub (v1).

## Next Phase

→ Las tasks TDD-ordered (con su estado real de shipped) están en `tasks.md`.
→ v1: reemplazar `StubAiClient` por `AnthropicAiClient` (detrás del mismo puerto `IAiClient`), re-habilitar SSE y auto-regen si el presupuesto lo permite.
