# Implementation Plan: 005-cv-pdf-docx-import

**Branch**: `005-cv-pdf-docx-import` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Hito**: v0.5 (P0.5)

## Summary

Implementar el flujo de **carga de archivos PDF/DOCX** del CV con parseo **server-side**, exponiendo `POST /api/v1/import` que recibe `multipart/form-data`, valida MIME + magic bytes + tamaño, extrae texto + secciones heurísticas, y devuelve un `ImportResult` JSON. El parseo vive tras el puerto `ICvParser` (Constitution Art. VI v1.1.0) con dos adaptadores: `PdfPigCvParser` (PDF, Apache-2.0) y `OpenXmlCvParser` (DOCX, MIT). Política de rate-limit `"import"` 30/h por IP (Constitution Art. VII v1.1.0). El resultado alimenta el editor (006) y, vía ese editor, el score (002) y la adaptación (003) operan sobre el texto que el usuario ya tenía.

**Decisiones técnicas auto-resueltas** (justificación completa en `research.md`):
- **PDF lib**: `UglyToad.PdfPig` (Apache-2.0). Razón: C# puro, sin dependencias nativas, gratis, soporte amplio. iText descartado por AGPL.
- **DOCX lib**: `DocumentFormat.OpenXml` (MIT). Razón: SDK oficial Microsoft, robusto, sin dependencias raras.
- **Server-side parsing**: en el backend, no en el browser. Razones: Art. V (entrada es dato, no instrucción), Art. VI (Clean Arch, adaptador único), Edge runtime de Next.js no es ideal para 5 MB de PDF, permite reusar validadores/normalizadores en un solo lugar.
- **Output shape**: `{ text, sections[], warnings[], engineVersion, traceId }`. Secciones por regex sobre headers en MAYÚSCULAS con `confidence: High|Low`.
- **Tamaños**: 5 MB max, ~100 páginas PDF max (defensa de CPU/memoria), truncado a 50k chars en `text` con warning.

## Technical Context

**Language/Version**: C# 13 / .NET 10
**Primary Dependencies** (NUEVOS, añadir a `BuildCv.Infrastructure.csproj`):
- `UglyToad.PdfPig` v0.1.x (NuGet, Apache-2.0)
- `DocumentFormat.OpenXml` v3.x (NuGet, MIT)
- `FluentValidation` (ya instalado en M1)
- `Microsoft.AspNetCore.RateLimiting` (built-in, política `"import"`)
- `xUnit` + `FluentAssertions` (testing, ya instalados)

**Storage**: NINGUNO. El archivo se procesa en RAM (byte[]) y se descarta tras extraer el texto (Art. III).

**Testing**: xUnit + FluentAssertions. Tests del puerto `ICvParser` con fake + tests de cada adaptador con golden samples sintéticos + tests de integración del endpoint con multipart.

**Target Platform**: Linux server (Render.com Docker), .NET 10 ASP.NET Core, retorno `application/json`. Multipart parser: `Microsoft.AspNetCore.Http.Features` built-in.

**Project Type**: Web service backend (extensión de M0/M1/M2, abre la puerta a v0.5).

**Performance Goals**:
- P95 parseo PDF 2 páginas <2s.
- P95 parseo DOCX 1 página <1s.
- Pico de memoria ≤ 4× tamaño del archivo (≤ 20 MB por request).
- 30 imports/h por IP; P95 latencia total <2.5s.

**Constraints**:
- Cero invocación de LLM en el parseo (Art. II — el import no usa IA).
- Cero persistencia (Art. III — todo en RAM, no se guarda en disco).
- Sin telemetría externa (Art. III).
- Constitución prevalece (Art. IX gobernanza).
- Cero supresiones (regla global del proyecto: `#pragma warning disable` prohibido).
- Domain PURO: 0 paquetes externos en `BuildCv.Domain`.

**Scale/Scope**:
- v0.5: ~50 imports/día esperados (10% de los usuarios sube archivo).
- 1 sola instancia del API, CPU-bound. Rate-limit 30/h protege.
- Pico de 30 imports concurrentes × 5 MB = 150 MB de RAM transitorio; cabe holgadamente en el tier actual.

## Constitution Check

*GATE: Must pass before Phase 0 research.*

| Art. | Status | Note |
|---|---|---|
| I — Cero invención | ✅ PASS | El import no toca IA; el texto extraído es lo que el usuario ya escribió. Cero invención se delega al editor (006) y al flujo 003. |
| II — Determinismo | ✅ PASS | `ImportResult.engineVersion` se sella; mismo archivo + misma versión = mismo resultado. El parser es CPU puro, sin red ni reloj en la ruta del texto. |
| III — Privacidad | ✅ PASS | Sin persistencia (NFR-001a); logs sin contenido (NFR-002a); sin envío a IA. |
| IV — Encuadre honesto | ✅ PASS | Copy frontend dice "extraer texto", NUNCA "optimizar para ATS" (NFR-022a). |
| V — Entrada como dato | ✅ PASS | El texto extraído se entrega al editor como contenido inerte. Defensa anti-prompt-injection del flujo 003 se aplica aguas abajo (NFR-005a). |
| VI — Clean Arch | ✅ PASS | Puerto `ICvParser` en `Application/Features/Import/`; adaptadores en `Infrastructure/Parsing/`. Domain PURO. |
| VII — Rate-limit | ✅ PASS | Política `"import"` 30/h por IP, **nueva en v1.1.0** (FR-039c). |
| VIII — TDD | ✅ PASS | Tests rojos ANTES. Cobertura ≥90% en adaptadores. |
| IX — Habeas Data | ✅ PASS | El contenido no sale del backend. ZDR no aplica a este flujo (sin IA). |

## Project Structure

### Documentation

```
specs/005-cv-pdf-docx-import/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: PdfPig + OpenXml research
├── data-model.md        # Phase 1: ImportRequest, ImportResult records
├── quickstart.md        # Phase 1: How to test
├── contracts/           # Phase 1: HTTP contracts (POST /api/v1/import)
│   └── import-api.md
└── tasks.md             # Phase 2: Implementation tasks (TDD-ordered)
```

### Source Code

```
src/BuildCv.Domain/                                  # PURO — no nuevos packages
├── Import/                                          # NUEVO namespace
│   ├── ImportRequest.cs                             # Record: { FileBytes, MimeDeclared, FileName, TraceId }
│   ├── ImportResult.cs                              # Record: { Text, Sections, Warnings, EngineVersion, TraceId }
│   ├── DetectedSection.cs                           # Record: { Heading, Start, End, Confidence }
│   ├── ImportWarning.cs                             # Record: { Code, Message, Severity }
│   ├── SectionHeuristics.cs                         # Static regex: detecta headers en MAYÚSCULAS
│   └── SectionRegexPatterns.cs                      # Constantes de patrones (ES + EN)

src/BuildCv.Application/                             # + Application services
├── Features/Import/
│   ├── ICvParser.cs                                 # Puerto: Parse(ImportRequest) → ImportResult
│   ├── ImportCvCommand.cs                           # Command para el handler
│   ├── ImportCvHandler.cs                           # Orquesta: validator → parser → return
│   ├── ImportCvValidator.cs                         # FluentValidation: tamaño, mime, magic bytes

src/BuildCv.Infrastructure/                          # + PdfPig + OpenXml NuGets
├── Parsing/
│   ├── PdfPigCvParser.cs                            # Implementación ICvParser para PDF
│   ├── OpenXmlCvParser.cs                           # Implementación ICvParser para DOCX
│   ├── CvParserDispatcher.cs                        # Selecciona el parser según MIME detectado
│   ├── PdfMagicBytes.cs                             # Helper: valida "%PDF-" en los primeros bytes
│   └── OpenXmlMagicBytes.cs                         # Helper: valida "PK\x03\x04" + entry word/document.xml

src/BuildCv.Api/                                     # + endpoint /api/v1/import
├── Endpoints/
│   └── ImportEndpoints.cs                           # POST → retorna ImportResult JSON
├── Security/
│   └── RateLimiting.cs                              # EXTENDER: agregar política "import" (30/h)
├── Contracts/
│   └── ImportContracts.cs                           # DTOs HTTP (request/response)
└── Program.cs                                       # Wire-up: MapImportEndpoints()
```

### Tests

```
tests/BuildCv.Domain.Tests/Import/
├── SectionHeuristicsTests.cs                        # Regex detecta headers ES + EN
├── SectionRegexPatternsTests.cs                     # Patrones individuales

tests/BuildCv.Application.Tests/Import/
├── ImportCvValidatorTests.cs                        # Tamaño, mime, magic bytes
├── ImportCvHandlerTests.cs                          # Llama ICvParser, mapea resultados
├── ICvParserContractTests.cs                        # Fake implementation cumple contrato
└── PdfPigCvParserIntegrationTests.cs                # Golden: PDF de 2 páginas (fixture)
    OpenXmlCvParserIntegrationTests.cs               # Golden: DOCX de 1 página (fixture)

tests/BuildCv.Api.IntegrationTests/Import/
├── ImportEndpointTests.cs                           # Wire-up HTTP, multipart, mime, size
├── ImportRateLimitTests.cs                          # Política "import" 30/h
└── ImportMspoofingTests.cs                          # 415 si magic bytes no coinciden
```

### Golden samples (test fixtures)

```
tests/BuildCv.Api.IntegrationTests/Import/Fixtures/
├── sample-cv-2pages.pdf                             # 2 páginas: nombre, contacto, experiencia, skills
├── sample-cv-1page.docx                             # 1 página: secciones EXPERIENCIA, EDUCACIÓN, HABILIDADES
├── sample-cv-encrypted.pdf                          # PDF cifrado → 422 IMPORT_PDF_ENCRYPTED
├── sample-cv-scanned.pdf                             # PDF solo con imágenes → 422 IMPORT_SCANNED_PDF
├── sample-cv-protected.docx                         # DOCX con contraseña → 422 IMPORT_DOCX_PROTECTED
└── sample-not-a-pdf.txt                             # .txt con bytes "%PDF-" en header → 415 (no cumple magic completo)
```

## Phase 0 — Research

Ver `research.md` para el detalle completo. Resumen ejecutivo:

- **PdfPig (UglyToad.PdfPig)**: API `PdfDocument.Open(byte[])` → itera páginas con `page.Text` → extrae texto + posición. Soporta UTF-8 nativo. PDFs cifrados lanzan `PdfDocumentEncryptedException`. PDFs escaneados devuelven texto vacío.
- **DocumentFormat.OpenXml**: `WordprocessingDocument.Open(stream, false)` → `MainDocumentPart.Document.Body` → itera `Paragraph` y `Table`. Texto en `paragraph.InnerText`. Imágenes referenciadas en `Blip` (no se extraen bytes).
- **Heurística de secciones**: regex `(?m)^(EXPERIENCIA|EXPERIENCE|EDUCACIÓN|EDUCATION|HABILIDADES|SKILLS|...)\s*$` sobre el texto extraído. `confidence: High` si es la única línea; `Low` si hay puntuación o es subcadena.
- **Rechazo temprano**: `Content-Length` HTTP header → si >5 MB, 413 inmediato. Si no hay header, leer `IFormFile.Length` antes de alocar el byte[]. Límite defensivo en Kestrel: `MaxRequestBodySize = 6_000_000` (5 MB + overhead multipart).
- **Validación MIME dual**: header `Content-Type` del form field + magic bytes. PdfPig y OpenXml también hacen su propia validación interna (lanza excepción si no son PDF/DOCX).

## Phase 1 — Design

### Data Model (`data-model.md`)

- **`ImportRequest`** (record, Application): `byte[] FileBytes`, `string MimeDeclared`, `string FileName`, `string TraceId`.
- **`ImportResult`** (record, Application): `string Text`, `IReadOnlyList<DetectedSection> Sections`, `IReadOnlyList<ImportWarning> Warnings`, `string EngineVersion`, `string TraceId`.
- **`DetectedSection`** (record, Application): `string Heading`, `int Start`, `int End`, `string Confidence` (High|Low).
- **`ImportWarning`** (record, Application): `string Code`, `string Message`, `string Severity` (Info|Warning|Error).
- **`SectionHeuristics`** (static, Domain): método `Detect(string text) → IReadOnlyList<DetectedSection>`. Sin IO, pura.
- **`SectionRegexPatterns`** (static const, Domain): `SpanishHeaders`, `EnglishHeaders` arrays. Lista cerrada (no se carga de archivo externo).

### Contracts (`contracts/import-api.md`)

```http
POST /api/v1/import
Content-Type: multipart/form-data; boundary=...
```

**Form fields**:
- `file` (required): el archivo PDF o DOCX, ≤ 5 MB.

**Response 200 OK**:
```json
{
  "text": "Juan Pérez\nBackend Developer con 5 años...",
  "sections": [
    { "heading": "Experiencia", "start": 245, "end": 612, "confidence": "High" },
    { "heading": "Educación", "start": 614, "end": 780, "confidence": "High" }
  ],
  "warnings": [
    { "code": "IMAGE_OMITTED", "message": "Se omitieron 2 imágenes", "severity": "Info" }
  ],
  "engineVersion": "1.0.0",
  "traceId": "0HMVD9F2E5Q2P:00000001"
}
```

**Response 400** (FluentValidation: falta `file`, mime inválido en header).
**Response 413** (`IMPORT_TOO_LARGE`): cuerpo >5 MB.
**Response 415** (`IMPORT_UNSUPPORTED_MEDIA`): mime declarado o magic bytes no coinciden.
**Response 422** (`IMPORT_PDF_ENCRYPTED` | `IMPORT_SCANNED_PDF` | `IMPORT_DOCX_PROTECTED` | `IMPORT_DOCX_NO_TEXT` | `IMPORT_TOO_MANY_PAGES` | `IMPORT_EMPTY_FILE`).
**Response 429** (rate-limit `"import"` 30/h).
**Response 503** (`IMPORT_ENGINE_ERROR`): PdfPig/OpenXml fallaron.

## Phase 2 — Tasks

Pendiente: ejecutar `/speckit.tasks` (auto mode) que genera `tasks.md` con TDD ordering. Ver `tasks.md` cuando esté generado.

## Risks

1. **PdfPig con PDFs malformados** — puede lanzar excepciones no esperadas. Mitigación: try/catch en `PdfPigCvParser.Parse`, mapear a `503 IMPORT_ENGINE_ERROR` o `422 IMPORT_PDF_INVALID` según la excepción.
2. **OpenXml con DOCX malformados o no ZIP** — `OpenXmlPackageException`. Mitigación: validación previa de magic bytes ZIP + entry `word/document.xml` antes de pasar al SDK.
3. **Consumo de CPU con PDFs de 100 páginas** — el parser es lineal en número de páginas. Mitigación: límite `IMPORT_TOO_MANY_PAGES` (>100 → 422).
4. **Memory leak en byte[] grandes** — usar `using` para `MemoryStream` y dispose explícito del `byte[]` (C# no lo hace automáticamente).
5. **Encoding del texto extraído** — PdfPig devuelve strings .NET (UTF-16), OpenXml también. Normalizar a UTF-8 al armar el JSON (NFR-019a).
6. **MIME spoofing en multipart** — un atacante puede declarar `Content-Type: application/pdf` y enviar un `.exe` con bytes `%PDF-` al inicio. Mitigación: validar estructura interna (PdfPig.Open lanza si no es PDF válido; OpenXml valida el ZIP + entry).
7. **Concurrencia: 30 imports/h × 5 MB simultáneos** — 150 MB de RAM pico. Aceptable en el tier actual; monitorear en producción.
8. **Heurística de secciones en falsos positivos** — la palabra "Skills" puede aparecer en un párrafo. Mitigación: `confidence: Low` en esos casos, y warning `SECTION_AMBIGUOUS` para revisión manual en el editor.

## Out of Scope

- OCR de PDFs escaneados (v1).
- Soporte de `.rtf`, `.odt`, `.pages`, `.txt` (v1, si hay demanda).
- Extracción de imágenes (v1).
- Múltiples CVs por usuario, historial (v1 con cuentas).
- Persistencia del archivo o texto server-side (Art. III).

## Next Phase

→ Phase 1: Tasks (`/speckit.tasks` auto mode) → `tasks.md`.
→ Handoff: el editor (006) consume `ImportResult`; el score (002) y adapt (003) operan sobre el texto en el editor.
