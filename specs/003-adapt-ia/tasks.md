# Tasks: 003-adapt-ia

**Date**: 2026-06-08 (orig) / 2026-06-09 (reality check) | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Status: SHIPPED (commit `68baaf2`)

> **Strict TDD**: tests rojos PRIMERO. La regla "0 supresiones" (Constitution) prohíbe `[Skip]`, `#pragma warning disable`, `@ts-ignore`.
>
> **Reality check:** Las tasks que originalmente proponían `Anthropic.SDK` / SSE streaming / auto-regen están marcadas con `[x] — implemented as StubAiClient (deterministic, no LLM) for v0` para dejar explícito qué se shipped y qué queda diferido a v1.

## Phase 0 — Setup (no TDD)

- [x] **T0.1** Crear feature branch `003-adapt-ia` → merged en `main` como commit `68baaf2`.
- [x] **T0.2** Verificar ZDR de Anthropic contractualmente (gate bloqueante Art. IX). **Diferido a v1** — en v0 el proveedor es un stub sin red, así que ZDR no aplica. Copy honesto: "el contenido se envía al proveedor y puede retenerse según su política" se mantiene como regla para v1.
- [x] **T0.3** Crear golden set de CVs tech colombianos con trampas intencionales. Distribuido en `tests/BuildCv.Domain.Tests/Adapt/` (EntityExtractorTests, CrossEntityValidatorTests, SeverityPolicyTests).

## Phase 1 — Domain Tests (rojo → verde → refactor)

### EntityExtractor

- [x] **T1.1** [TEST RED → GREEN] `EntityExtractorTests.Extracts_known_skills_from_cv` (usa gazetteer M0).
- [x] **T1.2** [TEST RED → GREEN] `EntityExtractorTests.Extracts_companies_with_known_prefixes`.
- [x] **T1.3** [TEST RED → GREEN] `EntityExtractorTests.Extracts_dates_in_dd_mm_yyyy_format`.
- [x] **T1.4** [TEST RED → GREEN] `EntityExtractorTests.Extracts_metrics_with_percent_sign`.
- [x] **T1.5** [TEST RED → GREEN] `EntityExtractorTests.Extracts_certifications_from_known_list`.
- [x] **T1.6** [IMPL] `BuildCv.Domain/Adapt/EntityExtractor.cs` con regex + gazetteer.
- [x] **T1.7** [GREEN] Todos los tests T1.1-T1.5 pasan.

### CrossEntityValidator

- [x] **T1.8** [TEST RED → GREEN] `CrossEntityValidatorTests.Detects_skill_not_in_original`.
- [x] **T1.9** [TEST RED → GREEN] `CrossEntityValidatorTests.Detects_company_not_in_original`.
- [x] **T1.10** [TEST RED → GREEN] `CrossEntityValidatorTests.Detects_date_not_in_original`.
- [x] **T1.11** [TEST RED → GREEN] `CrossEntityValidatorTests.Detects_certification_not_in_original`.
- [x] **T1.12** [TEST RED → GREEN] `CrossEntityValidatorTests.Does_not_flag_legitimate_skill_match`.
- [x] **T1.13** [TEST RED → GREEN] `CrossEntityValidatorTests.Handles_empty_original_entities`.
- [x] **T1.14** [IMPL] `BuildCv.Domain/Adapt/CrossEntityValidator.cs`.
- [x] **T1.15** [GREEN] Todos los tests T1.8-T1.13 pasan.

### CrossEntityValidatorGoldenTests (golden set colombiano)

- [x] **T1.16** [TEST RED → GREEN] — `Cv_says_2_years_job_asks_5_plus_no_inflation` — implementado como caso en `CrossEntityValidatorTests.cs` (no archivo separado).
- [x] **T1.17** [TEST RED → GREEN] — `Cv_without_aws_job_with_aws_no_cert_added` — caso en `CrossEntityValidatorTests.cs`.
- [x] **T1.18** [TEST RED → GREEN] — `Cv_mentions_company_x_job_mentions_y_no_company_swap` — caso en `CrossEntityValidatorTests.cs`.
- [x] **T1.19** [TEST RED → GREEN] — `Prompt_injection_attempt_blocked` — caso en `CrossEntityValidatorTests.cs` (el texto se trata como dato; no se ejecuta nada).
- [x] **T1.20** [TEST RED → GREEN] — `Legitimate_cv_no_false_positives` — caso en `CrossEntityValidatorTests.cs`.
- [x] **T1.21** [GREEN] Todos los golden tests pasan tras T1.14.

> **Nota:** El plan original listaba `CrossEntityValidatorGoldenTests.cs` como archivo separado. En la implementación shipped, el golden set se distribuyó entre los tests de unidad de `CrossEntityValidatorTests.cs` para evitar proliferación de archivos. Los nombres de tests se mantienen como casos identificables.

### SeverityPolicy

- [x] **T1.22** [TEST RED → GREEN] `SeverityPolicyTests.No_inventions_returns_None`.
- [x] **T1.23** [TEST RED → GREEN] `SeverityPolicyTests.One_soft_invention_returns_Warning`.
- [x] **T1.24** [TEST RED → GREEN] `SeverityPolicyTests.Three_soft_inventions_returns_Critical`.
- [x] **T1.25** [TEST RED → GREEN] `SeverityPolicyTests.One_hard_invention_returns_Critical`.
- [x] **T1.26** [IMPL] `BuildCv.Domain/Adapt/SeverityPolicy.cs`.
- [x] **T1.27** [GREEN] Todos los tests T1.22-T1.25 pasan.

### AdaptationResult + ValidationReport + EntityInvention (records)

- [x] **T1.28** [IMPL] Records en `BuildCv.Domain/Adapt/AdaptationTypes.cs` (un único archivo, NO archivos separados como sugería el plan original).

## Phase 2 — Application Tests (rojo → verde → refactor)

### IAiClient (puerto)

- [x] **T2.1** [IMPL] Interfaz `IAiClient` en `BuildCv.Application/Features/Adapt/IAiClient.cs` con un único método `Task<string> CompleteAsync(string prompt, CancellationToken ct)` — **sin** `StreamAsync` (no hay SSE en v0).
- [x] **T2.2** [IMPL] `StubAiClient` en `BuildCv.Infrastructure/Ai/` actúa como "mock" para tests; `AdaptCvHandlerTests` usa un fake `IAiClient` para verificar el flujo.

### PromptBuilder

- [x] **T2.3** [TEST RED → GREEN] `PromptBuilderTests.Generates_nonce_of_32_hex_chars`.
- [x] **T2.4** [TEST RED → GREEN] `PromptBuilderTests.Wraps_cv_in_data_block_with_nonce`.
- [x] **T2.5** [TEST RED → GREEN] `PromptBuilderTests.Wraps_job_in_data_block_with_nonce`.
- [x] **T2.6** [TEST RED → GREEN] `PromptBuilderTests.Includes_system_prompt_about_data_not_instruction`.
- [x] **T2.7** [TEST RED → GREEN] `PromptBuilderTests.Includes_reminder_at_end_of_prompt`.
- [x] **T2.8** [TEST RED → GREEN] `PromptBuilderTests.Stripes_closing_data_block_from_user_input` (defensa contra prompt-injection).
- [x] **T2.9** [IMPL] `PromptBuilder.cs`.
- [x] **T2.10** [GREEN] Todos los tests T2.3-T2.8 pasan.

### AdaptCvValidator

- [x] **T2.11** [TEST RED → GREEN] `AdaptCvValidatorTests.Rejects_empty_cv`.
- [x] **T2.12** [TEST RED → GREEN] `AdaptCvValidatorTests.Rejects_cv_over_50000_chars`.
- [x] **T2.13** [TEST RED → GREEN] `AdaptCvValidatorTests.Rejects_empty_job`.
- [x] **T2.14** [TEST RED → GREEN] `AdaptCvValidatorTests.Rejects_job_over_20000_chars`.
- [x] **T2.15** ~~[TEST RED] `AdaptCvValidatorTests.Rejects_identical_cv_and_job`.~~ — **No implementado en v0.** El handler no valida identidad CV=job; queda como mejora v1 si la telemetría muestra fricción.
- [x] **T2.16** [IMPL] `AdaptCvValidator.cs`.
- [x] **T2.17** [GREEN] Tests T2.11-T2.14 pasan.

### AdaptCvHandler

- [x] **T2.18** [TEST RED → GREEN] `AdaptCvHandlerTests.Calls_validator_first_returns_400_on_invalid`.
- [x] **T2.19** [TEST RED → GREEN] `AdaptCvHandlerTests.Extracts_original_entities_before_calling_ai`.
- [x] **T2.20** [TEST RED → GREEN] `AdaptCvHandlerTests.Calls_ai_with_prompt_built_by_prompt_builder`.
- [x] **T2.21** [TEST RED → GREEN] `AdaptCvHandlerTests.Extracts_adapted_entities_after_ai_call`.
- [x] **T2.22** [TEST RED → GREEN] `AdaptCvHandlerTests.Runs_cross_entity_validator`.
- [x] **T2.23** [TEST RED → GREEN] `AdaptCvHandlerTests.Returns_warning_when_severity_is_warning`.
- [x] **T2.24** ~~[TEST RED] `AdaptCvHandlerTests.Regenerates_when_critical_and_retry_available`.~~ — **No implementado en v0.** El handler es lineal (sin auto-regen). Queda para v1 si se habilita un LLM real con presupuesto para reintentos.
- [x] **T2.25** ~~[TEST RED] `AdaptCvHandlerTests.Does_not_retry_more_than_once`.~~ — **No implementado en v0.** Ídem T2.24.
- [x] **T2.26** [TEST RED → GREEN] `AdaptCvHandlerTests.Logs_metadata_no_pii`.
- [x] **T2.27** [IMPL] `AdaptCvHandler.cs` (lineal, sin reintentos).
- [x] **T2.28** [GREEN] Todos los tests T2.18-T2.23, T2.26 pasan.

## Phase 3 — Infrastructure

- [x] **T3.1** ~~Agregar `Anthropic.SDK` NuGet a `BuildCv.Infrastructure`.~~ — **REJECTED.** La implementación shipped usa `StubAiClient` (sin LLM, sin red, sin paquete externo).
- [x] **T3.2** [IMPL] `StubAiClient` en `src/BuildCv.Infrastructure/Ai/StubAiClient.cs` implementa `IAiClient` con un único método `CompleteAsync` que retorna un "CV marco" determinista sin agregar contenido.
- [x] **T3.3** [IMPL] Wire-up en `Infrastructure/DependencyInjection.cs`: `IAiClient → StubAiClient`.
- [x] **T3.4** ~~[IMPL] `CompleteAsync` usa `client.Messages.CreateAsync(...)`.~~ — **No aplica (stub).**
- [x] **T3.5** ~~[IMPL] `StreamAsync` usa `client.Messages.CreateStreamingAsync(...)`.~~ — **No aplica (sin SSE en v0).**
- [x] **T3.6** [IMPL] Wire-up en `Infrastructure/DependencyInjection.cs` con el stub.
- [x] **T3.7** ~~[TEST INTEGRATION] Test con API key real.~~ — **No aplica en v0** (sin API key). En v1, se reintroducirá como test marcado con `[Trait("Category", "Integration")]` y key desde `dotnet user-secrets`.

## Phase 4 — Api Layer

- [x] **T4.1** [TEST RED → GREEN] `AdaptEndpointTests.Accepts_valid_request_returns_200`.
- [x] **T4.2** [TEST RED → GREEN] `AdaptEndpointTests.Rejects_invalid_request_returns_400`.
- [x] **T4.3** [TEST RED → GREEN] `AdaptEndpointTests.Applies_rate_limit_ai_policy`.
- [x] **T4.4** [TEST RED → GREEN] `AdaptEndpointTests.Returns_503_when_ai_client_throws`.
- [x] **T4.5** [TEST RED → GREEN] `AdaptResponseMapperTests.Maps_domain_to_dto_correctly`.
- [x] **T4.6** [IMPL] `AdaptEndpoints.cs` con `MapPost /api/v1/adapt` + `RequireRateLimiting("ai")`. **Sin SSE.**
- [x] **T4.7** [IMPL] `AdaptRequestDto`, `AdaptResponseDto`, `ValidationReportDto`, `EntityInventionDto` en `BuildCv.Api/Contracts/AdaptContracts.cs`.
- [x] **T4.8** [IMPL] `AdaptResponseMapper` en el mismo archivo de contratos.
- [x] **T4.9** [GREEN] Todos los tests T4.1-T4.5 pasan.

### Streaming Endpoint

- [x] **T4.10** ~~[TEST RED] `AdaptStreamingTests.Sends_start_event_first`.~~ — **No aplica en v0** (sin SSE).
- [x] **T4.11** ~~[TEST RED] `AdaptStreamingTests.Sends_token_events_as_llm_streams`.~~ — **No aplica en v0.**
- [x] **T4.12** ~~[TEST RED] `AdaptStreamingTests.Sends_validation_event_after_completion`.~~ — **No aplica en v0.**
- [x] **T4.13** ~~[TEST RED] `AdaptStreamingTests.Sends_done_event_at_end`.~~ — **No aplica en v0.**
- [x] **T4.14** ~~[TEST RED] `AdaptStreamingTests.Sends_ping_comment_every_20s`.~~ — **No aplica en v0.**
- [x] **T4.15** ~~[TEST RED] `AdaptStreamingTests.Closes_stream_on_client_disconnect`.~~ — **No aplica en v0.**
- [x] **T4.16** ~~[IMPL] Crear endpoint SSE en `AdaptEndpoints.cs`.~~ — **No aplica en v0.**
- [x] **T4.17** ~~[GREEN] Todos los tests T4.10-T4.15 pasan.~~ — **No aplica en v0.**

## Phase 5 — Rate Limiting (Art. VII)

- [x] **T5.1** [TEST RED → GREEN] `RateLimitingTests.Ai_policy_allows_5_requests_per_hour`.
- [x] **T5.2** [TEST RED → GREEN] `RateLimitingTests.Ai_policy_rejects_6th_request_with_429`.
- [x] **T5.3** [TEST RED → GREEN] `RateLimitingTests.Deterministic_policy_still_allows_60_per_min`.
- [x] **T5.4** [IMPL] `RateLimiting.cs` con política `"ai"` (5/h por IP). Constante `AiPolicy = "ai"`.
- [x] **T5.5** [GREEN] Todos los tests T5.1-T5.3 pasan.

## Phase 6 — Web BFF (sub-proyecto paralelo)

> **Documentado en `BuildCv-web/specs/003-web-adapt-ui/`.** El BFF de frontend es responsabilidad del sub-proyecto `BuildCv-web` (repositorio independiente) y NO se ejecuta desde este monorepo backend.

- [x] **T6.1** [WEB] `BuildCv-web/specs/003-web-adapt-ui/spec.md` creado en el sub-proyecto.
- [x] **T6.2** [WEB] `BuildCv-web/app/api/adapt/route.ts` proxyea a `BACKEND_URL/api/v1/adapt`.
- [x] **T6.3** ~~[WEB] `BuildCv-web/app/api/adapt/stream/route.ts` para SSE.~~ — **No aplica en v0** (sin SSE en backend).
- [x] **T6.4** [WEB] Componente `<AdaptPanel />` con spinner + resultado.
- [x] **T6.5** [WEB] Indicador visual de severidad: verde (None), amarillo (Warning), rojo (Critical).
- [x] **T6.6** ~~[WEB] UI muestra el delta de mejora con cada cambio trazado.~~ — **Diferido a v1** (requiere tracking por chunk que el stub no genera).

## Phase 7 — Documentation & PR

- [x] **T7.1** `BuildCv-api/AGENTS.md` actualizado con referencia a `IAiClient` y al puerto.
- [x] **T7.2** `BuildCv-api/specs/002-score-engine/spec.md` sin cambios (no aplica a M1).
- [x] **T7.3** Entrada en CHANGELOG con citation of Constitution Art. I, V, IX.
- [x] **T7.4** Commit `68baaf2` con cita explícita de Constitution Art. I.

## Phase 8 — Pre-merge verification

- [x] **T8.1** `./scripts/preflight.sh` → exit 0
- [x] **T8.2** `bash /home/mackroph/Dev/portfolio/buildCV/scripts/constitution-check.sh` → 20/20 passes, 0 critical
- [x] **T8.3** Code review adversarial (`judgment-day` skill)
- [x] **T8.4** PR review con cita explícita de Constitution
- [x] **T8.5** Deploy a Render.com + smoke test

## Critical Path (TDD ordering — tal como se ejecutó)

```
T0 (setup) → T1 (Domain) → T2 (Application) → T3 (Infrastructure con StubAiClient) → T4 (Api) → T5 (Rate limit) → T6 (Web) → T7 (Docs) → T8 (Verify)
```

## Risks Per Phase (con estado real)

| Phase | Risk | Status |
|---|---|---|
| T1 | Golden set no detecta todos los casos | ✅ Resuelto: cobertura distribuida en EntityExtractor + CrossEntityValidator + SeverityPolicy |
| T2 | Prompt-builder no defiende contra prompt-injection | ✅ Cubierto por T2.8 + T2.6 + T2.7 |
| T3 | StubAiClient podría pasar tests triviales | ✅ Cubierto: tests con `IAiClient` mockeado verifican contrato y flujo |
| T4 | Sin SSE, UX menos progresiva | ⚠️ Aceptado para v0 (<100ms con stub); v1 reintroduce SSE con LLM real |
| T5 | Rate-limit muy estricto | ✅ 5/h suficiente para v0 (sin costo de LLM); ajustable en v1 según telemetría |

## Auto-mode notes

Este `tasks.md` se ejecutó con `/speckit.implement` (auto mode, sin pausas). El orchestrator delegó cada task al sub-agente `sdd-apply` y todos los tests rojos pasaron a verde siguiendo TDD estricto.

Cualquier task con error bloquea la cadena. No continuar hasta resolver.

## Open Questions (preservados del spec original)

- ¿Qué proveedor de IA usar? (Claude, OpenAI, OpenRouter) — **RESUELTO en commit `68baaf2`:** ninguno en v0 (stub). v1 reabrirá la decisión.
- ¿Streaming vía SSE o WebSocket? — **RESUELTO en commit `68baaf2`:** sin streaming en v0. v1 reintroducirá SSE con `Results.ServerSentEvents` (.NET 10).
- ¿Qué tamaño de bloque con nonce? — **RESUELTO en `PromptBuilder.cs:30`:** 16 bytes hex (32 chars), criptográficamente aleatorio.
- ¿Qué hacer si la validación post-IA detecta >X% de invención? — **RESUELTO en `SeverityPolicy.cs`:** clasificación 0/1-2/≥3 → None/Warning/Critical. **No hay auto-regen en v0**; queda como follow-up v1.
