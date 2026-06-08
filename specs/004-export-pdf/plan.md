# Implementation Plan: 004-export-pdf

**Branch**: `004-export-pdf` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)

## Summary

Implementar el flujo de **export del CV adaptado a PDF** usando `QuestPDF` (NuGet, open source, API fluida en C#).

**Decisiones técnicas auto-resueltas**:

- **Librería PDF**: `QuestPDF` v2024 (NuGet, MIT-style community license).
- **Layout**: custom, simple, profesional. Header con nombre del candidato, experiencia cronológica, skills en grid 2 columnas, footer con marca de agua.
- **Streaming**: generar PDF en `MemoryStream`, retornar como `byte[]` desde el endpoint. NO persistir en disco (Art. III).
- **Filename**: `cv-adapted-{YYYY-MM-DD}.pdf` (encuadre honesto).

## Technical Context

**Language/Version**: C# 13 / .NET 10
**Primary Dependencies**:
- `QuestPDF` v2024.x (NuGet, open source)
- `FluentValidation` (input validation)
- `Microsoft.AspNetCore.RateLimiting` (built-in, política "export")
- `xUnit` + `FluentAssertions` (testing)

**Storage**: N/A (PDF en memoria, no se persiste).

**Testing**: xUnit + FluentAssertions. Tests del puerto `IPdfGenerator` con fake + tests del layout con verificación de bytes (magic number `%PDF-` + tamaño mínimo).

**Target Platform**: Linux server (Render.com Docker), .NET 10 ASP.NET Core, retorno `application/pdf`.

**Project Type**: Web service backend (extensión de M0/M1).

**Performance Goals**:
- P95 generación <3s para CVs <10k chars.
- Tamaño PDF <500kB para CVs típicos.
- Latencia primer byte: inmediata (todo en memoria).

**Constraints**:
- Cero invocación de LLM (Art. II — el PDF no usa IA).
- Cero persistencia (Art. III — MemoryStream, return, done).
- Sin telemetría externa (Art. III).
- Constitución prevalece (Art. IX gobernanza).

**Scale/Scope**:
- v0: ~100 exports/h esperados (10% de los scores generan export).
- 1 sola instancia del API, CPU-bound. Rate-limit 20/h protege.

## Constitution Check

*GATE: Must pass before Phase 0 research.*

| Art. | Status | Note |
|---|---|---|
| I — Cero invención | ✅ PASS | FR-034 bloquea Hard invenciones. |
| III — Privacidad | ✅ PASS | Sin persistencia, logs sin contenido. |
| IV — Encuadre honesto | ✅ PASS | "no es un puntaje ATS oficial" en footer. Filename "cv-adapted-". |
| VI — Clean Arch | ✅ PASS | `IPdfGenerator` puerto en Application, QuestPDF en Infrastructure. |
| VII — Rate-limit | ✅ PASS | Política "export" 20/h, diferenciada. |
| VIII — TDD | ✅ PASS | Tests rojos ANTES. |
| IX — Habeas Data | ✅ PASS | PDF en memoria, no se persiste. |

## Project Structure

### Documentation

```
specs/004-export-pdf/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: QuestPDF research
├── data-model.md        # Phase 1: ExportRequest, PdfResult records
├── quickstart.md        # Phase 1: How to test
├── contracts/           # Phase 1: HTTP contracts (POST /api/v1/export)
└── tasks.md             # Phase 2: Implementation tasks (TDD-ordered)
```

### Source Code

```
src/BuildCv.Domain/                                  # PURO — no nuevos packages
├── Export/
│   ├── ExportRequest.cs                             # Record: { AdaptedCv, Validation, CandidateName }
│   ├── ExportResult.cs                              # Record: { byte[] Pdf, string Filename, int SizeBytes }
│   ├── PdfMetadata.cs                               # { GeneratedAt, EngineVersion, Severity, Inventions }
│   └── ValidationGate.cs                            # Domain service: puede exportar dado ValidationReport

src/BuildCv.Application/                             # + Application services
├── Features/Export/
│   ├── ExportPdfCommand.cs                          # { AdaptedCv, Validation, CandidateName }
│   ├── ExportPdfHandler.cs                          # Orquesta: validate gate → call IPdfGenerator → return
│   ├── ExportPdfValidator.cs                        # FluentValidation: cv no vacío, ≤50k, candidate name
│   ├── IPdfGenerator.cs                             # Puerto: GeneratePdf(ExportRequest) → byte[]

src/BuildCv.Infrastructure/                          # + QuestPDF NuGet
├── Pdf/
│   ├── QuestPdfGenerator.cs                         # Implementación IPdfGenerator
│   ├── PdfLayout.cs                                 # Builder del layout con secciones
│   └── Watermark.cs                                 # Footer con "no es ATS oficial"

src/BuildCv.Api/                                     # + endpoint /api/v1/export
├── Endpoints/ExportEndpoints.cs                     # POST → retorna File(bytes, "application/pdf", filename)
└── Security/RateLimiting.cs                         # EXTENDER: agregar política "export" (20/h)
```

### Tests

```
tests/BuildCv.Domain.Tests/Export/
├── ValidationGateTests.cs                          # Decide si export pasa (None/Warning → OK, Critical → block)

tests/BuildCv.Application.Tests/Export/
├── ExportPdfValidatorTests.cs                       # Tamaño, nombre candidato
├── ExportPdfHandlerTests.cs                         # Gate + IPdfGenerator mock
└── IPdfGeneratorContractTests.cs                    # Fake implementation cumple contrato

tests/BuildCv.Api.IntegrationTests/Export/
├── ExportEndpointTests.cs                           # Wire-up HTTP, rate-limit "export", 422 invention
└── ExportPdfIntegrationTests.cs                     # Bytes válidos, filename, content-type
```

## Phase 0 — Research

- **QuestPDF NuGet package**: v2024.x, API fluida (`Document.Create(container => {...})`).
- **Community license**: requiere `LicenseType.Community` en `Program.cs`.
- **Layout sections**: header, content (skills, experiencia, educación), footer.
- **Styling**: fonts (`FontFamily.Calibri` default), colors (`Colors.Grey.Medium`), sizes (`TextStyle.Default.Size(10)`).
- **Memory rendering**: `Document.Generate(stream)` retorna `MemoryStream` con `byte[]`.
- **Unicode support**: QuestPDF soporta UTF-8 out of the box para español (á é í ó ú ñ).

## Phase 1 — Design

### Data Model (`data-model.md`)

- **`ExportRequest`** (record): `string AdaptedCv`, `ValidationReport Validation`, `string CandidateName`.
- **`ExportResult`** (record): `byte[] Pdf`, `string Filename`, `int SizeBytes`, `PdfMetadata Metadata`.
- **`PdfMetadata`** (record): `DateTimeOffset GeneratedAt`, `string EngineVersion`, `string ModelVersion`, `Severity Severity`, `int InventionCount`.
- **`ValidationGate`** (Domain service): `bool CanExport(ValidationReport report)` → true si `severity != Critical OR inventions.hard.Count == 0`.

### Contracts (`contracts/export-api.md`)

```http
POST /api/v1/export
Content-Type: application/json
```

**Request Body**:
```json
{
  "adaptedCv": "string (max 50000)",
  "validation": {
    "isValid": true,
    "severity": "None|Warning|Critical",
    "inventions": [...],
    "warnings": [...]
  },
  "candidateName": "string (max 100, default 'Candidato')"
}
```

**Response 200 OK** (binary PDF):
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="cv-adapted-2026-06-08.pdf"
Content-Length: 12345
<binary PDF data>
```

**Response 400 Bad Request**: validation (FluentValidation).
**Response 422 Unprocessable Entity** (invenciones Hard):
```json
{
  "type": "...",
  "title": "Export bloqueado",
  "status": 422,
  "detail": "El CV adaptado tiene 1 invención Hard: [\"Acme Corp\"]. Regenera la adaptación antes de exportar.",
  "instance": "/api/v1/export"
}
```

**Response 429 Too Many Requests**: rate-limit "export" (20/h).
**Response 503 Service Unavailable**: QuestPDF falló.

## Phase 2 — Tasks

Pendiente: ejecutar `/speckit.tasks` (auto mode) que genera `tasks.md` con TDD ordering.

## Risks

1. **QuestPDF community license en producción** — verificar que la atribución "QuestPDF Community" sea visible en el PDF (built-in en el footer de la lib, OK).
2. **PDF malformado para CVs con caracteres raros** — probar con CVs reales con emojis, tildes, ñ.
3. **Memory leak en MemoryStream** — usar `using` y dispose después de retornar bytes.
4. **Timeout en generación** — QuestPDF es sync, no async. Si tarda >10s, bloquearía el thread. Solución: `Task.Run` o generar async wrapper.

## Out of Scope

- Múltiples templates (1 diseño en v0).
- Watermark con logo personalizado.
- Persistir PDF (v1).
- Email del PDF (v1).

## Next Phase

→ Phase 1: Tasks (`/speckit.tasks` auto mode).
