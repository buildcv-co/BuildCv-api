# Tasks: 003-adapt-ia

**Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Strict TDD**: tests rojos PRIMERO. La regla "0 supresiones" (Constitution) prohíbe `[Skip]`, `#pragma warning disable`, `@ts-ignore`.

## Phase 0 — Setup (no TDD)

- [ ] **T0.1** Crear feature branch `003-adapt-ia` (cuando se inicialice git en el repo).
- [ ] **T0.2** Verificar ZDR de Anthropic contractualmente (gate bloqueante Art. IX). Si no se puede verificar, mantener `ZeroDataRetention: false` y copy honesto.
- [ ] **T0.3** Crear golden set de 10+ CVs tech colombianos con trampas intencionales en `tests/BuildCv.Domain.Tests/Adapt/TestData/`.

## Phase 1 — Domain Tests (rojo → verde → refactor)

### EntityExtractor

- [ ] **T1.1** [TEST RED] `EntityExtractorTests.Extracts_known_skills_from_cv` (usa gazetteer M0).
- [ ] **T1.2** [TEST RED] `EntityExtractorTests.Extracts_companies_with_known_prefixes`.
- [ ] **T1.3** [TEST RED] `EntityExtractorTests.Extracts_dates_in_dd_mm_yyyy_format`.
- [ ] **T1.4** [TEST RED] `EntityExtractorTests.Extracts_metrics_with_percent_sign`.
- [ ] **T1.5** [TEST RED] `EntityExtractorTests.Extracts_certifications_from_known_list`.
- [ ] **T1.6** [IMPL] Crear `BuildCv.Domain/Adapt/EntityExtractor.cs` con regex + gazetteer.
- [ ] **T1.7** [GREEN] Todos los tests T1.1-T1.5 pasan.

### CrossEntityValidator

- [ ] **T1.8** [TEST RED] `CrossEntityValidatorTests.Detects_skill_not_in_original`.
- [ ] **T1.9** [TEST RED] `CrossEntityValidatorTests.Detects_company_not_in_original`.
- [ ] **T1.10** [TEST RED] `CrossEntityValidatorTests.Detects_date_not_in_original`.
- [ ] **T1.11** [TEST RED] `CrossEntityValidatorTests.Detects_certification_not_in_original`.
- [ ] **T1.12** [TEST RED] `CrossEntityValidatorTests.Does_not_flag_legitimate_skill_match`.
- [ ] **T1.13** [TEST RED] `CrossEntityValidatorTests.Handles_empty_original_entities`.
- [ ] **T1.14** [IMPL] Crear `BuildCv.Domain/Adapt/CrossEntityValidator.cs`.
- [ ] **T1.15** [GREEN] Todos los tests T1.8-T1.13 pasan.

### CrossEntityValidatorGoldenTests (golden set colombiano)

- [ ] **T1.16** [TEST RED] `CrossEntityValidatorGoldenTests.Cv_says_2_years_job_asks_5_plus_no_inflation`.
- [ ] **T1.17** [TEST RED] `CrossEntityValidatorGoldenTests.Cv_without_aws_job_with_aws_no_cert_added`.
- [ ] **T1.18** [TEST RED] `CrossEntityValidatorGoldenTests.Cv_mentions_company_x_job_mentions_y_no_company_swap`.
- [ ] **T1.19** [TEST RED] `CrossEntityValidatorGoldenTests.Prompt_injection_attempt_blocked`.
- [ ] **T1.20** [TEST RED] `CrossEntityValidatorGoldenTests.Legitimate_cv_no_false_positives` (curado con CV sin trampa).
- [ ] **T1.21** [GREEN] Todos los golden tests pasan tras T1.14.

### SeverityPolicy

- [ ] **T1.22** [TEST RED] `SeverityPolicyTests.No_inventions_returns_None`.
- [ ] **T1.23** [TEST RED] `SeverityPolicyTests.One_soft_invention_returns_Warning`.
- [ ] **T1.24** [TEST RED] `SeverityPolicyTests.Three_soft_inventions_returns_Critical`.
- [ ] **T1.25** [TEST RED] `SeverityPolicyTests.One_hard_invention_returns_Critical`.
- [ ] **T1.26** [IMPL] Crear `BuildCv.Domain/Adapt/SeverityPolicy.cs`.
- [ ] **T1.27** [GREEN] Todos los tests T1.22-T1.25 pasan.

### AdaptationResult + ValidationReport + EntityInvention (records)

- [ ] **T1.28** [IMPL] Crear los records en `BuildCv.Domain/Adapt/` (sin tests — son DTOs inmutables).

## Phase 2 — Application Tests (rojo → verde → refactor)

### IAiClient (puerto)

- [ ] **T2.1** [IMPL] Crear interfaz `IAiClient` en `BuildCv.Application/Features/Adapt/`.
- [ ] **T2.2** [IMPL] Mock en tests (`FakeAiClient` que retorna texto fijo).

### PromptBuilder

- [ ] **T2.3** [TEST RED] `PromptBuilderTests.Generates_nonce_of_32_hex_chars`.
- [ ] **T2.4** [TEST RED] `PromptBuilderTests.Wraps_cv_in_data_block_with_nonce`.
- [ ] **T2.5** [TEST RED] `PromptBuilderTests.Wraps_job_in_data_block_with_nonce`.
- [ ] **T2.6** [TEST RED] `PromptBuilderTests.Includes_system_prompt_about_data_not_instruction`.
- [ ] **T2.7** [TEST RED] `PromptBuilderTests.Includes_reminder_at_end_of_prompt`.
- [ ] **T2.8** [TEST RED] `PromptBuilderTests.Stripes_closing_data_block_from_user_input` (defensa contra prompt-injection).
- [ ] **T2.9** [IMPL] Crear `PromptBuilder.cs`.
- [ ] **T2.10** [GREEN] Todos los tests T2.3-T2.8 pasan.

### AdaptCvValidator

- [ ] **T2.11** [TEST RED] `AdaptCvValidatorTests.Rejects_empty_cv`.
- [ ] **T2.12** [TEST RED] `AdaptCvValidatorTests.Rejects_cv_over_50000_chars`.
- [ ] **T2.13** [TEST RED] `AdaptCvValidatorTests.Rejects_empty_job`.
- [ ] **T2.14** [TEST RED] `AdaptCvValidatorTests.Rejects_job_over_20000_chars`.
- [ ] **T2.15** [TEST RED] `AdaptCvValidatorTests.Rejects_identical_cv_and_job`.
- [ ] **T2.16** [IMPL] Crear `AdaptCvValidator.cs`.
- [ ] **T2.17** [GREEN] Todos los tests T2.11-T2.15 pasan.

### AdaptCvHandler

- [ ] **T2.18** [TEST RED] `AdaptCvHandlerTests.Calls_validator_first_returns_400_on_invalid`.
- [ ] **T2.19** [TEST RED] `AdaptCvHandlerTests.Extracts_original_entities_before_calling_ai`.
- [ ] **T2.20** [TEST RED] `AdaptCvHandlerTests.Calls_ai_with_prompt_built_by_prompt_builder`.
- [ ] **T2.21** [TEST RED] `AdaptCvHandlerTests.Extracts_adapted_entities_after_ai_call`.
- [ ] **T2.22** [TEST RED] `AdaptCvHandlerTests.Runs_cross_entity_validator`.
- [ ] **T2.23** [TEST RED] `AdaptCvHandlerTests.Returns_warning_when_severity_is_warning`.
- [ ] **T2.24** [TEST RED] `AdaptCvHandlerTests.Regenerates_when_critical_and_retry_available`.
- [ ] **T2.25** [TEST RED] `AdaptCvHandlerTests.Does_not_retry_more_than_once`.
- [ ] **T2.26** [TEST RED] `AdaptCvHandlerTests.Logs_metadata_no_pii`.
- [ ] **T2.27** [IMPL] Crear `AdaptCvHandler.cs`.
- [ ] **T2.28** [GREEN] Todos los tests T2.18-T2.26 pasan.

## Phase 3 — Infrastructure (Anthropic SDK)

- [ ] **T3.1** Agregar `Anthropic.SDK` NuGet a `BuildCv.Infrastructure/BuildCv.Infrastructure.csproj`.
- [ ] **T3.2** [IMPL] Crear `AnthropicOptions.cs` con `IOptions<>`.
- [ ] **T3.3** [IMPL] Crear `AnthropicAiClient.cs` que implementa `IAiClient`.
- [ ] **T3.4** [IMPL] `CompleteAsync` usa `client.Messages.CreateAsync(...)`.
- [ ] **T3.5** [IMPL] `StreamAsync` usa `client.Messages.CreateStreamingAsync(...)`.
- [ ] **T3.6** [IMPL] Wire-up en `Infrastructure/DependencyInjection.cs` con `IHttpClientFactory`.
- [ ] **T3.7** [TEST INTEGRATION] Test con API key real (mark `[Trait("Category", "Integration")]`).

## Phase 4 — Api Layer

- [ ] **T4.1** [TEST RED] `AdaptEndpointTests.Accepts_valid_request_returns_200`.
- [ ] **T4.2** [TEST RED] `AdaptEndpointTests.Rejects_invalid_request_returns_400`.
- [ ] **T4.3** [TEST RED] `AdaptEndpointTests.Applies_rate_limit_ai_policy`.
- [ ] **T4.4** [TEST RED] `AdaptEndpointTests.Returns_503_when_ai_client_throws`.
- [ ] **T4.5** [TEST RED] `AdaptResponseMapperTests.Maps_domain_to_dto_correctly`.
- [ ] **T4.6** [IMPL] Crear `AdaptEndpoints.cs` con `MapPost /api/v1/adapt` + `RequireRateLimiting("ai")`.
- [ ] **T4.7** [IMPL] Crear `AdaptRequestDto.cs`, `AdaptResponseDto.cs`, `ValidationReportDto.cs`, `EntityInventionDto.cs`.
- [ ] **T4.8** [IMPL] Crear `AdaptResponseMapper.cs`.
- [ ] **T4.9** [GREEN] Todos los tests T4.1-T4.5 pasan.

### Streaming Endpoint

- [ ] **T4.10** [TEST RED] `AdaptStreamingTests.Sends_start_event_first`.
- [ ] **T4.11** [TEST RED] `AdaptStreamingTests.Sends_token_events_as_llm_streams`.
- [ ] **T4.12** [TEST RED] `AdaptStreamingTests.Sends_validation_event_after_completion`.
- [ ] **T4.13** [TEST RED] `AdaptStreamingTests.Sends_done_event_at_end`.
- [ ] **T4.14** [TEST RED] `AdaptStreamingTests.Sends_ping_comment_every_20s` (Render.com keep-alive).
- [ ] **T4.15** [TEST RED] `AdaptStreamingTests.Closes_stream_on_client_disconnect`.
- [ ] **T4.16** [IMPL] Crear endpoint SSE en `AdaptEndpoints.cs`.
- [ ] **T4.17** [GREEN] Todos los tests T4.10-T4.15 pasan.

## Phase 5 — Rate Limiting (Art. VII)

- [ ] **T5.1** [TEST RED] `RateLimitingTests.Ai_policy_allows_5_requests_per_hour`.
- [ ] **T5.2** [TEST RED] `RateLimitingTests.Ai_policy_rejects_6th_request_with_429`.
- [ ] **T5.3** [TEST RED] `RateLimitingTests.Deterministic_policy_still_allows_60_per_min`.
- [ ] **T5.4** [IMPL] Extender `RateLimiting.cs` con política `"ai"` (5/h por IP).
- [ ] **T5.5** [GREEN] Todos los tests T5.1-T5.3 pasan.

## Phase 6 — Web BFF (sub-proyecto paralelo)

- [ ] **T6.1** Crear `BuildCv-web/specs/003-web-adapt-ui/spec.md` con el flujo de UI.
- [ ] **T6.2** Crear `BuildCv-web/app/api/adapt/route.ts` que proxyea a `BACKEND_URL/api/v1/adapt`.
- [ ] **T6.3** Crear `BuildCv-web/app/api/adapt/stream/route.ts` para SSE.
- [ ] **T6.4** Crear componente `<AdaptPanel />` con streaming visual + delta de mejora.
- [ ] **T6.5** Indicador visual de severidad: verde (None), amarillo (Warning), rojo (Critical).
- [ ] **T6.6** UI muestra el delta de mejora con cada cambio trazado.

## Phase 7 — Documentation & PR

- [ ] **T7.1** Actualizar `BuildCv-api/AGENTS.md` con referencia a `IAiClient` y al puerto.
- [ ] **T7.2** Actualizar `BuildCv-api/specs/002-score-engine/spec.md` si hay cambios en contratos.
- [ ] **T7.3** Agregar entrada en CHANGELOG con citation of Constitution Art. I, V, IX.
- [ ] **T7.4** PR con descripción que cita los artículos relevantes.

## Phase 8 — Pre-merge verification

- [ ] **T8.1** `./scripts/preflight.sh` → exit 0
- [ ] **T8.2** `./scripts/constitution-check.sh` → exit 0
- [ ] **T8.3** Code review adversarial (`judgment-day` skill)
- [ ] **T8.4** PR review con cita explícita de Constitution
- [ ] **T8.5** Deploy a Render.com + smoke test

## Critical Path (TDD ordering)

```
T0 (setup) → T1 (Domain) → T2 (Application) → T3 (Infrastructure) → T4 (Api) → T5 (Rate limit) → T6 (Web) → T7 (Docs) → T8 (Verify)
```

## Risks Per Phase

| Phase | Risk | Mitigation |
|---|---|---|
| T1 | Golden set no detecta todos los casos | Iterar: 10 → 20 → 30 casos |
| T2 | Prompt-builder no defiende contra prompt-injection | T2.8 explícito + red-team tests |
| T3 | Anthropic SDK cambia API | Pin versión major; tests integration con [Trait] |
| T4 | SSE se rompe en Render.com | Keep-alive comment + fallback a polling |
| T5 | Rate-limit muy estricto bloquea usuarios legítimos | Monitorear 429s; ajustar a 10/h si muchos falsos bloqueos |

## Auto-mode notes

Este `tasks.md` se ejecuta con `/speckit.implement` (auto mode, sin pausas). El orchestrator:

1. Lee este archivo.
2. Para cada task `[ ]`: invoca el sub-agente `sdd-apply` con la descripción exacta.
3. El sub-agente crea los tests rojos PRIMERO, luego la implementación, luego verifica verde.
4. Si un test falla: STOP, reporta, espera intervención manual.

Cualquier task con error bloquea la cadena. No continuar hasta resolver.
