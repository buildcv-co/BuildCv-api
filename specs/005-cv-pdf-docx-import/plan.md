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
- `UglyToad.PdfPig` **1.7.0-custom-5** (NuGet, Apache-2.0) — la versión shipped es un fork custom para soportar .NET 10; la versión `v0.1.x` que mencionaba el plan original NO es la que se usa.
- `DocumentFormat.OpenXml` **3.5.1** (NuGet, MIT)
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

### Source Code (shipped, commit `c61bdf4`)

```
src/BuildCv.Application/Features/Import/             # + Application services
├── ICvParser.cs                                     # Puerto: ImportResult Parse(ImportCvCommand)
├── ImportTypes.cs                                   # Combined: ImportCvCommand, ImportSection, ImportWarning, ImportResult (un único archivo)
├── ImportErrorCodes.cs                              # Catálogo cerrado de códigos IMPORT_*
├── ParserEngineException.cs                         # Excepción tipada con Code
├── SectionDetector.cs                               # Lógica de detección de secciones
├── ImportCvValidator.cs                             # FluentValidation: tamaño, mime
└── ImportCvHandler.cs                               # Orquesta: validator → ICvParser → return

src/BuildCv.Infrastructure/Parsing/                  # + PdfPig 1.7.0-custom-5 + OpenXml 3.5.1
├── PdfPigCvParser.cs                                # Implementación ICvParser para PDF (en memoria)
├── OpenXmlCvParser.cs                               # Implementación ICvParser para DOCX (en memoria)
└── ParserRouter.cs                                  # Compuesto que despacha al parser concreto según MIME + magic bytes
                                                      # (magic bytes checkeado inline; no hay archivos PdfMagicBytes.cs ni OpenXmlMagicBytes.cs separados)

src/BuildCv.Api/                                     # + endpoint /api/v1/import
├── Contracts/ImportContracts.cs                     # DTOs HTTP + ImportResponseMapper (28 líneas)
├── Endpoints/ImportEndpoints.cs                     # POST → ImportResult JSON
└── Security/RateLimiting.cs                         # Política "import" 30/h (constante ImportPolicy)
```

> **Diferencias con el plan original:**
> - El directorio `src/BuildCv.Domain/Import/` **NO existe** en la implementación shipped. Los tipos `ImportSection`, `ImportWarning`, `ImportResult` viven en `Application/Features/Import/ImportTypes.cs`. El Domain se mantiene PURO (cero referencias a Application/Infrastructure).
> - `ImportRequest.cs` (record del dominio) **NO existe** — el handler trabaja directamente con `ImportCvCommand` (de Application) en vez de un record separado de Dominio. La separación de capas es: handler usa Command (Application), parser retorna Result (Application), y el Domain permanece PURO.
> - `SectionHeuristics.cs` y `SectionRegexPatterns.cs` como archivos separados en Domain **NO existen**. La lógica equivalente vive en `Application/Features/Import/SectionDetector.cs`.
> - `CvParserDispatcher.cs` **NO existe** — la implementación shipped usa `ParserRouter.cs` (que es la única `ICvParser` registrada en DI y dispatcha a los adaptadores concretos).
> - `PdfMagicBytes.cs` y `OpenXmlMagicBytes.cs` como archivos separados **NO existen** — la lógica de magic bytes está inline en `ParserRouter.EnsureMagicBytes` (helper estático privado).
> - No hay jerarquía de excepciones `BuildCv.Domain/Import/Exceptions/` con 8 tipos — la implementación shipped usa una sola `ParserEngineException` (en `Application/Features/Import/`) con un `Code` string, mapeada a HTTP en el endpoint.

### Tests

```
tests/BuildCv.Application.Tests/Import/
├── ImportCvValidatorTests.cs                        # Tamaño, mime
├── ImportCvHandlerTests.cs                          # Llama ICvParser, mapea errores
└── SectionDetectorTests.cs                          # Detección de secciones ES + EN

tests/BuildCv.Infrastructure.Tests/Parsing/           # NO en Domain.Tests ni Integration.Tests
├── PdfTestFixtures.cs                               # Generador programático de PDFs in-memory
├── DocxTestFixtures.cs                              # Generador programático de DOCX in-memory
├── SectionDetectorIntegrationTests.cs               # Detección de secciones sobre texto extraído
├── PdfPigCvParserTests.cs                           # PDF: extrae texto, detecta cifrado/escaneado, preserva tildes
├── OpenXmlCvParserTests.cs                          # DOCX: extrae texto y tablas, omite imágenes
└── ParserRouterTests.cs                             # Despacha PDF vs DOCX, rechaza MIME no soportado
```

> **Diferencias con el plan original:**
> - Los tests viven en **`tests/BuildCv.Infrastructure.Tests/Parsing/`**, NO en `tests/BuildCv.Domain.Tests/Import/` (el directorio Domain no existe) ni en `tests/BuildCv.Api.IntegrationTests/Import/` (los tests de integración del endpoint HTTP no se automatizaron; la verificación e2e es manual con curl).
> - `SectionHeuristicsTests.cs` y `SectionRegexPatternsTests.cs` no existen como tales — la cobertura equivalente está en `SectionDetectorIntegrationTests.cs` y `SectionDetectorTests.cs`.
> - `ICvParserContractTests.cs` no existe — la cobertura del contrato está distribuida en los tests de cada adaptador (`PdfPigCvParserTests` + `OpenXmlCvParserTests`).
> - `PdfPigCvParserIntegrationTests.cs` y `OpenXmlCvParserIntegrationTests.cs` se unificaron en `PdfPigCvParserTests.cs` y `OpenXmlCvParserTests.cs` (sin prefijo "Integration" porque la suite ya está en `BuildCv.Infrastructure.Tests`).

### Golden samples (test fixtures)

> **NO existen archivos `.pdf` ni `.docx` en el repositorio.** La implementación shipped usa **fixtures programáticos in-memory** que construyen el binario del PDF/DOCX al vuelo dentro del test (vía `PdfTestFixtures` y `DocxTestFixtures` que envuelven PdfPig/OpenXml para generar archivos sintéticos). Esto evita versionar binarios en git y hace los tests deterministas sin depender de assets externos.

```
tests/BuildCv.Infrastructure.Tests/Parsing/
├── PdfTestFixtures.cs                               # Genera PDFs in-memory (texto, cifrado, escaneado, páginas>100)
└── DocxTestFixtures.cs                              # Genera DOCX in-memory (texto, tablas, imágenes, protegido)
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
