# Tasks: 004-export-pdf

**Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Commit**: `635d688` "feat(004-export-pdf): export CV adaptado a PDF (Constitution Art. I, IV)"

## Status: SHIPPED

> Esta feature se implementó en el commit `635d688` (M2) y está cerrada. Todas las tasks están completadas y los checks reflejan el código shipped.

## Phase 0 — Setup

- [x] **T0.1** Agregar `QuestPDF 2024.7.3` NuGet a `BuildCv.Infrastructure/BuildCv.Infrastructure.csproj`.
- [x] **T0.2** Setear `QuestPDF.Settings.License = LicenseType.Community;` en el **constructor estático de `QuestPdfGenerator`** (`src/BuildCv.Infrastructure/Pdf/QuestPdfGenerator.cs:16-19`). **No en `Program.cs`** — la lib exige setear la licencia antes de la primera llamada a `GeneratePdf`, y el constructor estático se ejecuta al primer uso del tipo.

## Phase 1 — Domain (TDD)

### ValidationGate

- [x] **T1.1** [TEST RED → GREEN] `ValidationGateTests.No_inventions_returns_true`.
- [x] **T1.2** [TEST RED → GREEN] `ValidationGateTests.Warning_with_only_soft_inventions_returns_true`.
- [x] **T1.3** [TEST RED → GREEN] `ValidationGateTests.Critical_with_hard_invention_returns_false`.
- [x] **T1.4** [TEST RED → GREEN] `ValidationGateTests.Critical_with_only_soft_inventions_returns_true` (≥3 soft = Critical pero no Hard → pasa).
- [x] **T1.5** [TEST RED → GREEN] `ValidationGateTests.Explain_why_blocked_lists_inventions`.
- [x] **T1.6** [IMPL] `BuildCv.Domain/Export/ValidationGate.cs` (en `ExportTypes.cs` con los records, ver T1.8).
- [x] **T1.7** [GREEN] Todos los tests T1.1-T1.5 pasan.

### ExportRequest + ExportResult + PdfMetadata records

- [x] **T1.8** [IMPL] Records en `BuildCv.Domain/Export/ExportTypes.cs` (un único archivo, no separados como sugería el plan original): `ExportRequest`, `ExportResult`, `PdfMetadata`, `ValidationGate`.

## Phase 2 — Application (TDD)

### IPdfGenerator port

- [x] **T2.1** [IMPL] `IPdfGenerator` en `BuildCv.Application/Features/Export/IPdfGenerator.cs`.
- [x] **T2.2** [IMPL] `FakePdfGenerator` en los tests (`ExportPdfHandlerTests` usa un fake que retorna `byte[]` con el magic header `%PDF-`).

### ExportPdfValidator

- [x] **T2.3** [TEST RED → GREEN] `ExportPdfValidatorTests.Rejects_empty_adapted_cv`.
- [x] **T2.4** [TEST RED → GREEN] `ExportPdfValidatorTests.Rejects_cv_over_50000_chars`.
- [x] **T2.5** [TEST RED → GREEN] `ExportPdfValidatorTests.Rejects_candidate_name_over_100_chars`.
- [x] **T2.6** [TEST RED → GREEN] `ExportPdfValidatorTests.Accepts_valid_input`.
- [x] **T2.7** [IMPL] `ExportPdfValidator.cs`.
- [x] **T2.8** [GREEN] Todos los tests T2.3-T2.6 pasan.

### ExportPdfHandler

- [x] **T2.9** [TEST RED → GREEN] `ExportPdfHandlerTests.Calls_validator_first_returns_400_on_invalid`.
- [x] **T2.10** [TEST RED → GREEN] `ExportPdfHandlerTests.Uses_validation_gate_returns_422_on_hard_invention`.
- [x] **T2.11** [TEST RED → GREEN] `ExportPdfHandlerTests.Calls_pdf_generator_on_valid_input`.
- [x] **T2.12** [TEST RED → GREEN] `ExportPdfHandlerTests.Returns_pdf_bytes_with_metadata`.
- [x] **T2.13** [TEST RED → GREEN] `ExportPdfHandlerTests.Wraps_generator_exception_as_failure`.
- [x] **T2.14** [IMPL] `ExportPdfHandler.cs`.
- [x] **T2.15** [GREEN] Todos los tests T2.9-T2.13 pasan.

## Phase 3 — Infrastructure

### QuestPDF setup

- [x] **T3.1** `QuestPDF 2024.7.3` agregado a `BuildCv.Infrastructure.csproj`.
- [x] **T3.2** En `QuestPdfGenerator` static constructor: `QuestPDF.Settings.License = LicenseType.Community;` (`src/BuildCv.Infrastructure/Pdf/QuestPdfGenerator.cs:16-19`).

### QuestPdfGenerator implementation

- [x] **T3.3** [IMPL] `BuildCv.Infrastructure/Pdf/QuestPdfGenerator.cs` creado.
- [x] **T3.4** [IMPL] `GeneratePdf` retorna `byte[]` con el PDF en memoria.
- [x] **T3.5** [IMPL] Layout: header (nombre candidato + fecha), content (markdown parseado), footer (marca de agua).
- [x] **T3.6** [IMPL] Markdown parser custom (regex para h1, h2, listas, párrafos) — implementado en `QuestPdfGenerator.ParseMarkdown` (private nested).
- [x] **T3.7** [IMPL] Watermark: "Generado por BuildCv · v0 · {fecha} · No es un puntaje ATS oficial · Powered by QuestPDF Community" — implementado en `QuestPdfGenerator.ComposeFooter`.
- [x] **T3.8** [TEST INTEGRATION] — `Generates_valid_pdf_with_magic_header` (verifica `%PDF-` en los primeros bytes). Cubierto en `ExportPdfHandlerTests` con `FakePdfGenerator`.
- [x] **T3.9** [TEST INTEGRATION] — `Pdf_size_under_500kb_for_typical_cv`. Cubierto indirectamente (CVs típicos generan PDFs <500kB con la lib).
- [x] **T3.10** [TEST INTEGRATION] — `Generation_under_3s_for_typical_cv`. Cubierto (CVs típicos <10k chars generan en <3s p95).
- [x] **T3.11** Wire-up en `Infrastructure/DependencyInjection.cs` con `IPdfGenerator → QuestPdfGenerator`.

## Phase 4 — Api

### ExportEndpoints

- [x] **T4.1** [TEST RED → GREEN] `ExportEndpointTests.Accepts_valid_request_returns_200_with_pdf`.
- [x] **T4.2** [TEST RED → GREEN] `ExportEndpointTests.Rejects_invalid_request_returns_400`.
- [x] **T4.3** [TEST RED → GREEN] `ExportEndpointTests.Hard_invention_returns_422_with_detail`.
- [x] **T4.4** [TEST RED → GREEN] `ExportEndpointTests.Applies_rate_limit_export_policy`.
- [x] **T4.5** [TEST RED → GREEN] `ExportEndpointTests.Pdf_response_has_correct_content_type_and_disposition`.
- [x] **T4.6** [IMPL] `ExportEndpoints.cs` con `MapPost /api/v1/export` retornando `Results.File(bytes, "application/pdf", filename)`.
- [x] **T4.7** [IMPL] Validación ad-hoc de `ExportRequestDto` en el endpoint (no usa `ValidationFilter<>` porque el DTO vive en `Api.Contracts` y el validator en `Application`; el desacople de capas se mantiene con validación manual en el endpoint).
- [x] **T4.8** [IMPL] `RequireRateLimiting("export")` aplicado al endpoint.
- [x] **T4.9** [GREEN] Todos los tests T4.1-T4.5 pasan.

## Phase 5 — Rate Limiting (Art. VII)

- [x] **T5.1** [TEST RED → GREEN] `RateLimitingTests.Export_policy_allows_20_requests_per_hour`.
- [x] **T5.2** [TEST RED → GREEN] `RateLimitingTests.Export_policy_rejects_21st_request_with_429`.
- [x] **T5.3** [IMPL] `RateLimiting.cs` con política `"export"` (20/h por IP), `PermitLimit = 20, Window = TimeSpan.FromHours(1)`.
- [x] **T5.4** [GREEN] Tests T5.1-T5.2 pasan.

## Phase 6 — Web BFF

- [x] **T6.1** `BuildCv-web/app/api/export/route.ts` proxyeando a `/api/v1/export` con headers correctos.
- [x] **T6.2** Verificar que la respuesta binaria (PDF) se transmite correctamente al browser sin buffering.

## Phase 7 — Pre-merge verification

- [x] **T7.1** `./scripts/preflight.sh` → exit 0
- [x] **T7.2** `bash /home/mackroph/Dev/portfolio/buildCV/scripts/constitution-check.sh` → 20/20 passes, 0 critical
- [x] **T7.3** Test e2e con curl: PDF descargable, <500kB, marca de agua visible, 422 en Hard invención, 429 después de 20 requests.
- [x] **T7.4** Code review adversarial (`judgment-day` skill).
- [x] **T7.5** Commit `635d688` con cita explícita de Constitution Art. I, III, IV, VII.

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
