# Tasks: 004-export-pdf

**Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Strict TDD**: tests rojos PRIMERO. Cero supresiones (Constitution Art. VIII).

## Phase 0 — Setup

- [ ] **T0.1** Agregar `QuestPDF` NuGet a `BuildCv.Infrastructure/BuildCv.Infrastructure.csproj`.
- [ ] **T0.2** Setear `QuestPDF.Settings.License = LicenseType.Community;` en `Program.cs` (startup).

## Phase 1 — Domain (TDD)

### ValidationGate

- [ ] **T1.1** [TEST RED] `ValidationGateTests.No_inventions_returns_true`.
- [ ] **T1.2** [TEST RED] `ValidationGateTests.Warning_with_only_soft_inventions_returns_true`.
- [ ] **T1.3** [TEST RED] `ValidationGateTests.Critical_with_hard_invention_returns_false`.
- [ ] **T1.4** [TEST RED] `ValidationGateTests.Critical_with_only_soft_inventions_returns_true` (≥3 soft = Critical pero no Hard → pasa).
- [ ] **T1.5** [TEST RED] `ValidationGateTests.Explain_why_blocked_lists_inventions`.
- [ ] **T1.6** [IMPL] Crear `BuildCv.Domain/Export/ValidationGate.cs`.
- [ ] **T1.7** [GREEN] Todos los tests T1.1-T1.5 pasan.

### ExportRequest + ExportResult + PdfMetadata records

- [ ] **T1.8** [IMPL] Crear records en `BuildCv.Domain/Export/`.

## Phase 2 — Application (TDD)

### IPdfGenerator port

- [ ] **T2.1** [IMPL] Crear interfaz `IPdfGenerator` en `BuildCv.Application/Features/Export/`.
- [ ] **T2.2** [IMPL] `FakePdfGenerator` para tests (retorna bytes `Encoding.UTF8.GetBytes("FAKE PDF")`).

### ExportPdfValidator

- [ ] **T2.3** [TEST RED] `ExportPdfValidatorTests.Rejects_empty_adapted_cv`.
- [ ] **T2.4** [TEST RED] `ExportPdfValidatorTests.Rejects_cv_over_50000_chars`.
- [ ] **T2.5** [TEST RED] `ExportPdfValidatorTests.Rejects_candidate_name_over_100_chars`.
- [ ] **T2.6** [TEST RED] `ExportPdfValidatorTests.Accepts_valid_input`.
- [ ] **T2.7** [IMPL] Crear `ExportPdfValidator.cs`.
- [ ] **T2.8** [GREEN] Todos los tests T2.3-T2.6 pasan.

### ExportPdfHandler

- [ ] **T2.9** [TEST RED] `ExportPdfHandlerTests.Calls_validator_first_returns_400_on_invalid`.
- [ ] **T2.10** [TEST RED] `ExportPdfHandlerTests.Uses_validation_gate_returns_422_on_hard_invention`.
- [ ] **T2.11** [TEST RED] `ExportPdfHandlerTests.Calls_pdf_generator_on_valid_input`.
- [ ] **T2.12** [TEST RED] `ExportPdfHandlerTests.Returns_pdf_bytes_with_metadata`.
- [ ] **T2.13** [TEST RED] `ExportPdfHandlerTests.Wraps_generator_exception_as_failure`.
- [ ] **T2.14** [IMPL] Crear `ExportPdfHandler.cs`.
- [ ] **T2.15** [GREEN] Todos los tests T2.9-T2.13 pasan.

## Phase 3 — Infrastructure

### QuestPDF setup

- [ ] **T3.1** Agregar `QuestPDF` package a `BuildCv.Infrastructure.csproj`.
- [ ] **T3.2** En `QuestPdfGenerator` static constructor: `QuestPDF.Settings.License = LicenseType.Community;`.

### QuestPdfGenerator implementation

- [ ] **T3.3** [IMPL] Crear `BuildCv.Infrastructure/Pdf/QuestPdfGenerator.cs`.
- [ ] **T3.4** [IMPL] `GeneratePdf` retorna `byte[]` con el PDF en memoria.
- [ ] **T3.5** [IMPL] Layout: header (nombre candidato + fecha), content (markdown parseado), footer (marca de agua).
- [ ] **T3.6** [IMPL] Markdown parser custom (regex para h1, h2, listas, párrafos).
- [ ] **T3.7** [IMPL] Watermark: "Generado por BuildCv · v0 · {fecha} · No es un puntaje ATS oficial · Powered by QuestPDF Community".
- [ ] **T3.8** [TEST INTEGRATION] `QuestPdfGeneratorTests.Generates_valid_pdf_with_magic_header` (verifica `%PDF-` en los primeros bytes).
- [ ] **T3.9** [TEST INTEGRATION] `QuestPdfGeneratorTests.Pdf_size_under_500kb_for_typical_cv`.
- [ ] **T3.10** [TEST INTEGRATION] `QuestPdfGeneratorTests.Generation_under_3s_for_typical_cv`.
- [ ] **T3.11** Wire-up en `Infrastructure/DependencyInjection.cs` con `IPdfGenerator → QuestPdfGenerator`.

## Phase 4 — Api

### ExportEndpoints

- [ ] **T4.1** [TEST RED] `ExportEndpointTests.Accepts_valid_request_returns_200_with_pdf`.
- [ ] **T4.2** [TEST RED] `ExportEndpointTests.Rejects_invalid_request_returns_400`.
- [ ] **T4.3** [TEST RED] `ExportEndpointTests.Hard_invention_returns_422_with_detail`.
- [ ] **T4.4** [TEST RED] `ExportEndpointTests.Applies_rate_limit_export_policy`.
- [ ] **T4.5** [TEST RED] `ExportEndpointTests.Pdf_response_has_correct_content_type_and_disposition`.
- [ ] **T4.6** [IMPL] Crear `ExportEndpoints.cs` con `MapPost /api/v1/export` retornando `Results.File(bytes, "application/pdf", filename)`.
- [ ] **T4.7** [IMPL] `AddEndpointFilter<ValidationFilter<ExportPdfCommand>>()`.
- [ ] **T4.8** [IMPL] `RequireRateLimiting("export")`.
- [ ] **T4.9** [GREEN] Todos los tests T4.1-T4.5 pasan.

## Phase 5 — Rate Limiting (Art. VII)

- [ ] **T5.1** [TEST RED] `RateLimitingTests.Export_policy_allows_20_requests_per_hour`.
- [ ] **T5.2** [TEST RED] `RateLimitingTests.Export_policy_rejects_21st_request_with_429`.
- [ ] **T5.3** [IMPL] Extender `RateLimiting.cs` con política `"export"` (20/h por IP).
- [ ] **T5.4** [GREEN] Tests T5.1-T5.2 pasan.

## Phase 6 — Web BFF

- [ ] **T6.1** Actualizar `BuildCv-web/app/api/export/route.ts` para proxyar al endpoint /api/v1/export con headers correctos.
- [ ] **T6.2** Verificar que la respuesta binaria (PDF) se transmite correctamente al browser sin buffering.

## Phase 7 — Pre-merge verification

- [ ] **T7.1** `./scripts/preflight.sh` → exit 0
- [ ] **T7.2** `./scripts/constitution-check.sh` → exit 0
- [ ] **T7.3** Test e2e con curl: PDF descargable, <500kB, marca de agua visible, 422 en Hard invención, 429 después de 20 requests.
- [ ] **T7.4** Code review adversarial (`judgment-day` skill).
- [ ] **T7.5** PR con cita explícita de Constitution Art. I, III, IV, VII.

## Critical Path (TDD ordering)

```
T0 (setup) → T1 (Domain) → T2 (Application) → T3 (Infrastructure) → T4 (Api) → T5 (Rate limit) → T6 (Web) → T7 (Verify)
```

## Risks Per Phase

| Phase | Risk | Mitigation |
|---|---|---|
| T1 | Hard invenciones definidas distinto en T1.3 vs T1.4 | Edge cases: ≥3 soft = Critical, pero no Hard → pasa |
| T3 | QuestPDF license expira | T0.2 setup + T3.2 static constructor |
| T3 | PDF malformado para CVs raros | T3.8 valida magic header `%PDF-` |
| T4 | 422 con cuerpo problem-details RFC 9457 | T4.3 valida estructura |
| T5 | Rate-limit consume cupo en 422 | T4.4 valida que solo exitosos consumen (cuenta en handler) |
| T6 | BFF no transmite binario correctamente | T6.2 streaming sin buffer |

## Auto-mode notes

Este `tasks.md` se ejecuta con `/speckit.implement` (auto mode, sin pausas). El orchestrator delega cada task al sub-agente `sdd-apply`. Si un test falla, el orchestrator hace retry una vez con prompt corregido. Si sigue fallando, STOP.
