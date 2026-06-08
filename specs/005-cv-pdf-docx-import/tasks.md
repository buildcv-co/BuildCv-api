# Tasks: 005-cv-pdf-docx-import

**Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Strict TDD**: tests rojos PRIMERO. Cero supresiones (Constitution Art. VIII). Hito **v0.5**.

## Phase 0 — Setup

- [ ] **T0.1** Agregar `UglyToad.PdfPig` y `DocumentFormat.OpenXml` NuGet a `BuildCv.Infrastructure/BuildCv.Infrastructure.csproj` (vía `dotnet add package`).
- [ ] **T0.2** Wire-up DI: en `BuildCv.Infrastructure/DependencyInjection.cs`, registrar `ICvParser` → `PdfPigCvParser` (con `IsPdf`) y `ICvParser` → `OpenXmlCvParser` (con `IsDocx`).
- [ ] **T0.3** Crear `ICvParserDispatcher` en `Application/Features/Import/` que selecciona el parser según MIME detectado (magic bytes).

## Phase 1 — Domain (TDD, sin nuevos packages)

### SectionRegexPatterns

- [ ] **T1.1** [TEST RED] `SectionRegexPatternsTests.Contains_Expected_Spanish_Headers`.
- [ ] **T1.2** [TEST RED] `SectionRegexPatternsTests.Contains_Expected_English_Headers`.
- [ ] **T1.3** [TEST RED] `SectionRegexPatternsTests.No_Duplicates_Between_Spanish_And_English`.
- [ ] **T1.4** [IMPL] Crear `BuildCv.Domain/Import/SectionRegexPatterns.cs` con arrays `Spanish`, `English`, `AllHeaders`.
- [ ] **T1.5** [GREEN] Tests T1.1–T1.3 pasan.

### SectionHeuristics (función pura)

- [ ] **T1.6** [TEST RED] `SectionHeuristicsTests.Detects_Spanish_Headers_As_High_Confidence`.
- [ ] **T1.7** [TEST RED] `SectionHeuristicsTests.Detects_English_Headers_As_High_Confidence`.
- [ ] **T1.8** [TEST RED] `SectionHeuristicsTests.Returns_Empty_When_No_Headers_Found`.
- [ ] **T1.9** [TEST RED] `SectionHeuristicsTests.Marks_Substring_Matches_As_Low_Confidence`.
- [ ] **T1.10** [TEST RED] `SectionHeuristicsTests.Returns_Correct_Start_And_End_Indices`.
- [ ] **T1.11** [TEST RED] `SectionHeuristicsTests.Handles_Empty_And_Whitespace_Input_Gracefully`.
- [ ] **T1.12** [IMPL] Crear `BuildCv.Domain/Import/SectionHeuristics.cs` con método estático `Detect(string)`.
- [ ] **T1.13** [GREEN] Tests T1.6–T1.11 pasan.

### Excepciones de dominio

- [ ] **T1.14** [TEST RED] `ImportExceptionTests.Each_Exception_Type_Has_Correct_Code_And_HttpStatus`.
- [ ] **T1.15** [IMPL] Crear `BuildCv.Domain/Import/Exceptions/` con las 8 excepciones (`PdfEncryptedException`, `ScannedPdfException`, `DocxProtectedException`, `DocxNoTextException`, `TooManyPagesException`, `EmptyFileException`, `UnsupportedMediaException`, `TooLargeException`).
- [ ] **T1.16** [GREEN] Test T1.14 pasa.

### Records del dominio

- [ ] **T1.17** [IMPL] Crear records `ImportRequest`, `ImportResult`, `DetectedSection`, `ImportWarning` en `BuildCv.Domain/Import/`.
- [ ] **T1.18** [TEST RED] `ImportResultTests.Seller_Version_Format_Is_Semver`.
- [ ] **T1.19** [GREEN] Test T1.18 pasa.

## Phase 2 — Application (TDD)

### ICvParser port

- [ ] **T2.1** [IMPL] Crear interfaz `ICvParser` en `BuildCv.Application/Features/Import/`.
- [ ] **T2.2** [IMPL] `FakeCvParser` para tests (acepta texto pre-cargado, retorna resultado fijo).

### ImportCvValidator (FluentValidation)

- [ ] **T2.3** [TEST RED] `ImportCvValidatorTests.Rejects_Empty_FileBytes`.
- [ ] **T2.4** [TEST RED] `ImportCvValidatorTests.Rejects_File_Over_5MB`.
- [ ] **T2.5** [TEST RED] `ImportCvValidatorTests.Rejects_Unsupported_Mime`.
- [ ] **T2.6** [TEST RED] `ImportCvValidatorTests.Accepts_Valid_Pdf_Mime`.
- [ ] **T2.7** [TEST RED] `ImportCvValidatorTests.Accepts_Valid_Docx_Mime`.
- [ ] **T2.8** [IMPL] Crear `ImportCvValidator.cs` con reglas de tamaño y MIME.
- [ ] **T2.9** [GREEN] Tests T2.3–T2.7 pasan.

### ImportCvHandler

- [ ] **T2.10** [TEST RED] `ImportCvHandlerTests.Calls_Validator_First_Returns_400_On_Invalid`.
- [ ] **T2.11** [TEST RED] `ImportCvHandlerTests.Dispatches_To_Correct_Parser_Based_On_Mime`.
- [ ] **T2.12** [TEST RED] `ImportCvHandlerTests.Catches_ImportException_And_Returns_Failure_With_Code`.
- [ ] **T2.13** [TEST RED] `ImportCvHandlerTests.Returns_ImportResult_On_Success`.
- [ ] **T2.14** [TEST RED] `ImportCvHandlerTests.Wraps_Unexpected_Exception_As_503_Failure`.
- [ ] **T2.15** [IMPL] Crear `ImportCvHandler.cs` orquestando: validator → dispatcher → ICvParser.
- [ ] **T2.16** [GREEN] Tests T2.10–T2.14 pasan.

### ICvParserContractTests

- [ ] **T2.17** [TEST RED] `ICvParserContractTests.Fake_Implementation_Follows_Contract` (todas las implementaciones deben manejar: archivo vacío, secciones detectadas, sin secciones, encoding raro, caracteres Unicode).

## Phase 3 — Infrastructure

### PdfPigCvParser

- [ ] **T3.1** [TEST RED] `PdfPigCvParserTests.Parses_2page_Pdf_Extracts_Text_And_Sections` (usa `tests/.../Fixtures/sample-cv-2pages.pdf`).
- [ ] **T3.2** [TEST RED] `PdfPigCvParserTests.Throws_PdfEncryptedException_On_Encrypted_Pdf`.
- [ ] **T3.3** [TEST RED] `PdfPigCvParserTests.Throws_ScannedPdfException_On_Image_Only_Pdf`.
- [ ] **T3.4** [TEST RED] `PdfPigCvParserTests.Throws_TooManyPagesException_On_Over_100_Pages`.
- [ ] **T3.5** [TEST RED] `PdfPigCvParserTests.Preserves_Spanish_Accents_And_Tildes`.
- [ ] **T3.6** [TEST RED] `PdfPigCvParserTests.Truncates_Text_Over_50k_Chars_With_Warning`.
- [ ] **T3.7** [TEST RED] `PdfPigCvParserTests.Emits_NoSectionsDetected_Warning_When_Heuristic_Fails`.
- [ ] **T3.8** [TEST RED] `PdfPigCvParserTests.Seller_EngineVersion_As_1_0_0`.
- [ ] **T3.9** [IMPL] Crear `BuildCv.Infrastructure/Parsing/PdfPigCvParser.cs`.
- [ ] **T3.10** [GREEN] Tests T3.1–T3.8 pasan.

### OpenXmlCvParser

- [ ] **T3.11** [TEST RED] `OpenXmlCvParserTests.Parses_1page_Docx_Extracts_Text_And_Sections` (usa `tests/.../Fixtures/sample-cv-1page.docx`).
- [ ] **T3.12** [TEST RED] `OpenXmlCvParserTests.Throws_DocxProtectedException_On_Password_Protected_Docx`.
- [ ] **T3.13** [TEST RED] `OpenXmlCvParserTests.Throws_DocxNoTextException_On_Empty_Docx`.
- [ ] **T3.14** [TEST RED] `OpenXmlCvParserTests.Extracts_Tables_With_Tab_Separator`.
- [ ] **T3.15** [TEST RED] `OpenXmlCvParserTests.Emits_ImageOmitted_Warning_With_Count`.
- [ ] **T3.16** [TEST RED] `OpenXmlCvParserTests.Preserves_Spanish_Accents_And_Tildes`.
- [ ] **T3.17** [TEST RED] `OpenXmlCvParserTests.Truncates_Text_Over_50k_Chars_With_Warning`.
- [ ] **T3.18** [IMPL] Crear `BuildCv.Infrastructure/Parsing/OpenXmlCvParser.cs`.
- [ ] **T3.19** [GREEN] Tests T3.11–T3.17 pasan.

### Magic bytes helpers

- [ ] **T3.20** [TEST RED] `PdfMagicBytesTests.IsPdf_True_For_PercentPdf_Header`.
- [ ] **T3.21** [TEST RED] `PdfMagicBytesTests.IsPdf_False_For_Other_Headers`.
- [ ] **T3.22** [TEST RED] `OpenXmlMagicBytesTests.IsDocx_True_For_PK_With_WordDocument_Entry`.
- [ ] **T3.23** [TEST RED] `OpenXmlMagicBytesTests.IsDocx_False_For_Other_Zips`.
- [ ] **T3.24** [IMPL] Crear `BuildCv.Infrastructure/Parsing/PdfMagicBytes.cs` y `OpenXmlMagicBytes.cs`.
- [ ] **T3.25** [GREEN] Tests T3.20–T3.23 pasan.

### CvParserDispatcher

- [ ] **T3.26** [TEST RED] `CvParserDispatcherTests.Dispatches_To_PdfPig_For_Pdf_Magic_Bytes`.
- [ ] **T3.27** [TEST RED] `CvParserDispatcherTests.Dispatches_To_OpenXml_For_Docx_Magic_Bytes`.
- [ ] **T3.28** [TEST RED] `CvParserDispatcherTests.Throws_UnsupportedMedia_For_Other_Magic_Bytes`.
- [ ] **T3.29** [IMPL] Crear `BuildCv.Infrastructure/Parsing/CvParserDispatcher.cs`.
- [ ] **T3.30** [GREEN] Tests T3.26–T3.28 pasan.

## Phase 4 — Api

### ImportEndpoints

- [ ] **T4.1** [TEST RED] `ImportEndpointTests.Accepts_Pdf_Returns_200_With_ImportResult`.
- [ ] **T4.2** [TEST RED] `ImportEndpointTests.Accepts_Docx_Returns_200_With_ImportResult`.
- [ ] **T4.3** [TEST RED] `ImportEndpointTests.Rejects_Txt_With_415`.
- [ ] **T4.4** [TEST RED] `ImportEndpointTests.Rejects_File_Over_5MB_With_413`.
- [ ] **T4.5** [TEST RED] `ImportEndpointTests.Rejects_Mismatched_Mime_With_415`.
- [ ] **T4.6** [TEST RED] `ImportEndpointTests.Rejects_Empty_File_With_422`.
- [ ] **T4.7** [TEST RED] `ImportEndpointTests.Encrypted_Pdf_Returns_422_With_Detail`.
- [ ] **T4.8** [TEST RED] `ImportEndpointTests.Scanned_Pdf_Returns_422_With_Detail`.
- [ ] **T4.9** [TEST RED] `ImportEndpointTests.Returns_ProblemDetails_Rfc9457_On_Errors`.
- [ ] **T4.10** [TEST RED] `ImportEndpointTests.Unexpected_Exception_Returns_503`.
- [ ] **T4.11** [IMPL] Crear `Endpoints/ImportEndpoints.cs` con `MapPost /api/v1/import` que:
  - Lee `IFormFile file` del form.
  - Construye `ImportCvCommand`.
  - Llama `ImportCvHandler`.
  - Mapea resultados a `ImportResponseDto` (200) o `ProblemDetails` (4xx/5xx).
- [ ] **T4.12** [IMPL] `AddEndpointFilter<ValidationFilter<ImportCvCommand>>()`.
- [ ] **T4.13** [IMPL] `RequireRateLimiting("import")`.
- [ ] **T4.14** [GREEN] Tests T4.1–T4.10 pasan.

### Kestrel max request body size

- [ ] **T4.15** [IMPL] En `Program.cs` o `appsettings.json`, configurar `Kestrel.Limits.MaxRequestBodySize = 6_000_000` (5 MB + overhead multipart).
- [ ] **T4.16** [TEST RED] `ImportEndpointTests.Oversized_Request_Rejected_By_Kestrel_With_413`.

### Contracts

- [ ] **T4.17** [IMPL] Crear `BuildCv.Api/Contracts/ImportContracts.cs` con `ImportResponseDto`, `SectionDto`, `WarningDto`, `ImportResponseMapper`.

### Program.cs wire-up

- [ ] **T4.18** [IMPL] En `Program.cs`, agregar `app.MapImportEndpoints();` después de los otros endpoints.

## Phase 5 — Rate Limiting (Constitution Art. VII v1.1.0)

- [ ] **T5.1** [TEST RED] `RateLimitingTests.Import_Policy_Allows_30_Requests_Per_Hour`.
- [ ] **T5.2** [TEST RED] `RateLimitingTests.Import_Policy_Rejects_31st_Request_With_429`.
- [ ] **T5.3** [TEST RED] `RateLimitingTests.Import_Policy_Independent_From_Score_And_Ai_Policies`.
- [ ] **T5.4** [IMPL] Extender `BuildCv.Api/Security/RateLimiting.cs` con constante `ImportPolicy = "import"` y `AddPolicy(ImportPolicy, ...)` con `PermitLimit = 30, Window = TimeSpan.FromHours(1)`.
- [ ] **T5.5** [GREEN] Tests T5.1–T5.3 pasan.

## Phase 6 — Golden samples (test fixtures)

- [ ] **T6.1** [IMPL] Crear `tests/BuildCv.Api.IntegrationTests/Import/Fixtures/sample-cv-2pages.pdf` (sintético, 2 páginas con secciones EXPERIENCIA, EDUCACIÓN, HABILIDADES).
- [ ] **T6.2** [IMPL] Crear `tests/BuildCv.Api.IntegrationTests/Import/Fixtures/sample-cv-1page.docx` (sintético, 1 página con secciones).
- [ ] **T6.3** [IMPL] Crear `tests/BuildCv.Api.IntegrationTests/Import/Fixtures/sample-cv-encrypted.pdf` (PDF cifrado — usar qpdf o similar para generarlo).
- [ ] **T6.4** [IMPL] Crear `tests/BuildCv.Api.IntegrationTests/Import/Fixtures/sample-cv-scanned.pdf` (PDF solo con imágenes, sin texto).
- [ ] **T6.5** [IMPL] Crear `tests/BuildCv.Api.IntegrationTests/Import/Fixtures/sample-cv-protected.docx` (DOCX con `DocumentProtection`).
- [ ] **T6.6** [IMPL] Crear `tests/BuildCv.Api.IntegrationTests/Import/Fixtures/sample-not-a-pdf.txt` (texto plano con "PDF" en el nombre).

## Phase 7 — Integration & E2E tests

- [ ] **T7.1** [TEST RED] `ImportEndpointTests.Accepts_Realistic_Multipart_Request_With_Boundary`.
- [ ] **T7.2** [TEST RED] `ImportEndpointTests.Response_Has_Content_Type_Application_Json`.
- [ ] **T7.3** [TEST RED] `ImportEndpointTests.Sections_Are_Detected_For_2page_Pdf_Fixture`.
- [ ] **T7.4** [TEST RED] `ImportEndpointTests.Sections_Are_Detected_For_1page_Docx_Fixture`.
- [ ] **T7.5** [TEST RED] `ImportEndpointTests.Warnings_Are_Propagated_In_Response_Body`.
- [ ] **T7.6** [TEST RED] `ImportEndpointTests.EngineVersion_Is_1_0_0_In_Response`.
- [ ] **T7.7** [TEST RED] `ImportEndpointTests.TraceId_Is_Present_In_Response`.
- [ ] **T7.8** [TEST RED] `ImportEndpointTests.Rate_Limit_Returns_429_With_Retry_After_Header`.
- [ ] **T7.9** [GREEN] Todos los tests T7.1–T7.8 pasan.

## Phase 8 — Web BFF (en `BuildCv-web`, documentado en 005-web-cv-import-ui)

- [ ] **T8.1** [WEB] Crear `BuildCv-web/app/api/import/route.ts` (BFF que proxyea multipart a `BACKEND_URL/api/v1/import`).
- [ ] **T8.2** [WEB] Crear `BuildCv-web/lib/api/import.ts` con Zod schemas (`ImportResultSchema`, etc.) y función `requestImport(file)`.
- [ ] **T8.3** [WEB] Crear `BuildCv-web/components/import/file-upload.tsx` con drag/drop + click, validación 5 MB client-side.
- [ ] **T8.4** [WEB] Crear `BuildCv-web/components/import/import-button.tsx` con state machine (`idle|loading|success|error`).
- [ ] **T8.5** [WEB] Crear `BuildCv-web/components/import/import-result-panel.tsx` con preview del texto, secciones, warnings.
- [ ] **T8.6** [WEB] Crear página `BuildCv-web/app/importar/page.tsx` que orquesta los 3 componentes.
- [ ] **T8.7** [WEB] Copy en `BuildCv-web/lib/copy/es.ts`: `IMPORT_COPY` con strings en español (neutral, sin Rioplatense).
- [ ] **T8.8** [WEB] Test E2E manual: subir PDF, ver resultado, "Usar en editor" navega a `/editor` (006).
- [ ] **T8.9** [WEB] Accesibilidad WCAG 2.2 AA: `aria-label` en file input, `aria-live` para anunciar warnings, navegación por teclado (drag/drop + Enter para abrir file picker).
- [ ] **T8.10** [WEB] Validar que `pnpm lint` + `pnpm build` pasan en verde.

## Phase 9 — Pre-merge verification

- [ ] **T9.1** `./scripts/preflight.sh` → exit 0
- [ ] **T9.2** `./scripts/constitution-check.sh` → exit 0 (cite Art. III, V, VI, VII, VIII, IX)
- [ ] **T9.3** `dotnet list src/BuildCv.Domain package references` → 0 packages (Domain PURO)
- [ ] **T9.4** `dotnet list src/BuildCv.Domain reference` → 0 project refs (solo Microsoft.NETCore.App)
- [ ] **T9.5** Test e2e con curl: PDF happy path, DOCX happy path, 415, 413, 422 (cifrado/escaneado/protegido), 429.
- [ ] **T9.6** Test e2e web: drag PDF → ver resultado → click "Usar en editor" → editor carga texto.
- [ ] **T9.7** Code review adversarial (`judgment-day` skill).
- [ ] **T9.8** PR con cita explícita de Constitution Art. III, V, VI, VII, VIII, IX.

## Critical Path (TDD ordering)

```
T0 (setup)
  ↓
T1 (Domain: regex + heurística + records)
  ↓
T2 (Application: puerto + validator + handler)
  ↓
T3 (Infrastructure: PdfPig + OpenXml + dispatcher)
  ↓
T4 (Api: endpoint + ProblemDetails + Kestrel)
  ↓
T5 (Rate limit: política "import" 30/h)
  ↓
T6 (Golden samples: fixtures sintéticos)
  ↓
T7 (Integración E2E)
  ↓
T8 (Web BFF + UI components)
  ↓
T9 (Pre-merge verification)
```

## Risks Per Phase

| Phase | Risk | Mitigation |
|---|---|---|
| T1 | Heurística de secciones marca falsos positivos | `confidence: Low` + `SECTION_AMBIGUOUS` warning; v0.5.1 puede afinar |
| T3 | PdfPig lanza excepciones no esperadas con PDFs malformados | try/catch global + mapeo a `503 IMPORT_ENGINE_ERROR` |
| T3 | OpenXml con DOCX malformados o no ZIP | magic bytes check ANTES de pasar al SDK; `415` si no coincide |
| T4 | 413 de Kestrel tiene mensaje poco claro | custom `IExceptionHandler` que mapea a ProblemDetails con código `IMPORT_TOO_LARGE` |
| T4 | MIME spoofing con `Content-Type: application/pdf` pero bytes de .exe | validación de magic bytes + estructura interna (PdfPig.Open lanza si no es PDF válido) |
| T5 | Rate-limit consume cupo en 4xx | documentar como intencional (defensa de CPU); ajustable en v0.5.1 si hay fricción |
| T8 | Web UI no transmite archivos grandes (>4 GB) por el runtime de Vercel | limitar a 5 MB en cliente ANTES de subir; BFF usa `runtime = "nodejs"` (no edge) |
| T8 | Accesibilidad: drag/drop no es accesible para usuarios de teclado | ofrecer también `<input type="file">` con `<label>` cliqueable; `aria-describedby` con instrucciones |

## Auto-mode notes

Este `tasks.md` se ejecuta con `/speckit.implement` (auto mode, sin pausas). El orchestrator delega cada task al sub-agente `sdd-apply`. Si un test falla, el orchestrator hace retry una vez con prompt corregido. Si sigue fallando, STOP.

## Handoff a features downstream

- **006-cv-editor**: el editor consume `ImportResult` (Zod-validado en cliente) y lo usa como semilla del textarea.
- **002-score-engine**: sin cambios (opera sobre texto pegado/importado por el editor).
- **003-adapt-ia**: sin cambios (opera sobre texto del editor).
