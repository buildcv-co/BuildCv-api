# Data Model: 005-cv-pdf-docx-import

> **Source of truth:** `src/BuildCv.Application/Features/Import/ImportTypes.cs`, `ICvParser.cs`, `ParserEngineException.cs`, `ImportErrorCodes.cs`, `SectionDetector.cs` (commit `c61bdf4`).
>
> **Diferencia con el plan original:** los tipos viven en `Application/Features/Import/` (no en `Domain/Import/`). El directorio `src/BuildCv.Domain/Import/` **NO existe** — la implementación shipped mantiene el Domain PURO (cero packages externos, cero referencias a Application) y pone los records compartidos en Application. La separación de capas es: handler usa Command (Application), parser retorna Result (Application), y el Domain permanece PURO.

## Application Types (inmutables, records, en `BuildCv.Application/Features/Import/ImportTypes.cs`)

```csharp
namespace BuildCv.Application.Features.Import;

/// <summary>
/// Comando inmutable para importar un CV. La unidad de aplicación (handler)
/// orquesta validación, parseo y mapeo de errores. El endpoint construye
/// este record a partir del IFormFile multipart.
/// </summary>
public sealed record ImportCvCommand(
    byte[] FileBytes,
    string MimeType,
    string OriginalFileName,
    string TraceId);

/// <summary>Sección candidata detectada por la heurística de regex.</summary>
public sealed record ImportSection(
    string Heading,
    int Start,
    int End,
    string Confidence);  // "High" | "Low"

/// <summary>Aviso no bloqueante sobre el resultado del parseo.</summary>
public sealed record ImportWarning(
    string Code,
    string Message,
    string Severity);  // "Info" | "Warning" | "Error"

/// <summary>
/// Resultado del parseo. Es la semilla que consume el editor (006) y, vía
/// este editor, el score (002) y la adaptación (003).
/// </summary>
public sealed record ImportResult(
    string Text,
    IReadOnlyList<ImportSection> Sections,
    IReadOnlyList<ImportWarning> Warnings,
    string EngineVersion,
    string TraceId);
```

## Excepción de Application (`BuildCv.Application/Features/Import/ParserEngineException.cs`)

> **Diferencia con el plan original:** NO existe una jerarquía de 8 excepciones de dominio en `BuildCv.Domain/Import/Exceptions/`. La implementación shipped usa **una sola** excepción `ParserEngineException` (en `Application/`) con un campo `Code` string estable. Esto simplifica el mapeo a HTTP y elimina la proliferación de tipos.

```csharp
namespace BuildCv.Application.Features.Import;

/// <summary>
/// Excepción de motor: el parser encontró algo que sabe clasificar (PDF cifrado,
/// DOCX protegido, etc.) y lo traduce a un código estable mapeable a HTTP.
/// Vive en Application porque el handler (Application) la lanza tras mapear
/// el código que el adaptador (Infrastructure) reporta.
/// </summary>
public sealed class ParserEngineException : Exception
{
    public string Code { get; }

    public ParserEngineException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
```

## Catálogo de códigos de error (`BuildCv.Application/Features/Import/ImportErrorCodes.cs`)

```csharp
namespace BuildCv.Application.Features.Import;

public static class ImportErrorCodes
{
    public const string Validation = "IMPORT_VALIDATION";
    public const string TooLarge = "IMPORT_TOO_LARGE";
    public const string UnsupportedMedia = "IMPORT_UNSUPPORTED_MEDIA";

    public const string PdfEncrypted = "IMPORT_PDF_ENCRYPTED";
    public const string ScannedPdf = "IMPORT_SCANNED_PDF";
    public const string DocxProtected = "IMPORT_DOCX_PROTECTED";
    public const string DocxNoText = "IMPORT_DOCX_NO_TEXT";
    public const string TooManyPages = "IMPORT_TOO_MANY_PAGES";
    public const string EmptyFile = "IMPORT_EMPTY_FILE";
    public const string InvalidPdf = "IMPORT_INVALID_PDF";
    public const string InvalidDocx = "IMPORT_INVALID_DOCX";

    public const string EngineError = "IMPORT_ENGINE_ERROR";
}
```

## Puerto `ICvParser` (`BuildCv.Application/Features/Import/ICvParser.cs`)

```csharp
namespace BuildCv.Application.Features.Import;

/// <summary>
/// Puerto de parseo de archivos (Constitution Art. VI v1.1.0 — ICvParser).
/// Los adaptadores concretos (PdfPig, OpenXml) viven en Infrastructure.
/// </summary>
public interface ICvParser
{
    ImportResult Parse(ImportCvCommand command);
}
```

## Servicio de aplicación: detector de secciones (`SectionDetector.cs`)

> **Diferencia con el plan original:** NO existen `SectionHeuristics.cs` ni `SectionRegexPatterns.cs` como archivos separados en Domain. La lógica equivalente vive en `Application/Features/Import/SectionDetector.cs` (instancia de clase, no static helper, para mantener testabilidad con DI). El comportamiento es el del plan: regex sobre headers en MAYÚSCULAS (ES + EN), `confidence: High` si la línea solo tiene el header, `Low` si hay puntuación o subcadena.

## Compuesto `ParserRouter` (Infrastructure, en lugar de un dispatcher separado)

> **Diferencia con el plan original:** NO existe `CvParserDispatcher.cs` separado. La única `ICvParser` registrada en DI es `ParserRouter`, que internamente despacha al parser concreto (`PdfPigCvParser` o `OpenXmlCvParser`) según MIME declarado y magic bytes. La validación de magic bytes está inline en `ParserRouter.EnsureMagicBytes` (helper estático privado); no hay archivos `PdfMagicBytes.cs` ni `OpenXmlMagicBytes.cs` separados.

```csharp
namespace BuildCv.Infrastructure.Parsing;

public sealed class ParserRouter : ICvParser
{
    private readonly PdfPigCvParser _pdfParser;
    private readonly OpenXmlCvParser _docxParser;

    public ParserRouter(PdfPigCvParser pdfParser, OpenXmlCvParser docxParser)
    {
        _pdfParser = pdfParser;
        _docxParser = docxParser;
    }

    public ImportResult Parse(ImportCvCommand command)
    {
        var mime = command.MimeType?.Trim() ?? string.Empty;

        if (mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            EnsureMagicBytes(command.FileBytes, expectedPdfMagic: true);
            return _pdfParser.Parse(command);
        }

        if (mime.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase))
        {
            EnsureMagicBytes(command.FileBytes, expectedPdfMagic: false);
            return _docxParser.Parse(command);
        }

        throw new ParserEngineException(
            "UNSUPPORTED_MIME",
            $"Tipo de archivo no soportado: {mime}. Sube un PDF o DOCX.");
    }

    private static void EnsureMagicBytes(byte[] bytes, bool expectedPdfMagic) { /* inline: %PDF- o PK\x03\x04 */ }
}
```

## Tipos de API (DTOs HTTP, en `BuildCv.Api/Contracts/ImportContracts.cs`)

```csharp
namespace BuildCv.Api.Contracts;

public sealed record ImportSectionDto(string Heading, int Start, int End, string Confidence);

public sealed record ImportWarningDto(string Code, string Message, string Severity);

public sealed record ImportResponseDto(
    string Text,
    IReadOnlyList<ImportSectionDto> Sections,
    IReadOnlyList<ImportWarningDto> Warnings,
    string EngineVersion,
    string TraceId);

public static class ImportResponseMapper
{
    public static ImportResponseDto Map(ImportResult result) => new(
        result.Text,
        result.Sections.Select(s => new ImportSectionDto(s.Heading, s.Start, s.End, s.Confidence)).ToList(),
        result.Warnings.Select(w => new ImportWarningDto(w.Code, w.Message, w.Severity)).ToList(),
        result.EngineVersion,
        result.TraceId);
}
```

## Pipeline de validación

```
ImportEndpoints.POST /api/v1/import
├── 1. Kestrel MaxRequestBodySize (5 MB + overhead multipart)  [413 si excede]
├── 2. ImportCvValidator.Validate(cmd)                        [400 si falla]
│      ├── FileBytes.Length > 0                                [IMPORT_EMPTY_FILE → 400]
│      ├── FileBytes.Length ≤ 5_000_000                        [IMPORT_TOO_LARGE → 400]
│      └── MimeType ∈ {application/pdf, ...wordprocessingml...document}
│                                                               [400 con detalle]
├── 3. ICvParser.Parse(cmd)                                     [ParserRouter despacha]
│      ├── Magic bytes check (inline en ParserRouter)           [415 si no coincide]
│      └── PdfPigCvParser.Parse / OpenXmlCvParser.Parse         [puede lanzar ParserEngineException → mapeo a 4xx/5xx]
├── 4. RequireRateLimiting("import") 30/h                       [429 si excede]
└── 5. Return ImportResponseDto (200) o ProblemDetails (4xx/5xx)
```

## Máquina de estados (errores)

```
[Request]
   ↓
[Validate] ──invalid──→ [400 ProblemDetails]
   ↓ valid
[Rate Limit] ──exceeded─→ [429 ProblemDetails + Retry-After]
   ↓ ok
[Dispatch Parser]
   ↓
[Magic bytes] ──mismatch──→ [415 ProblemDetails: IMPORT_UNSUPPORTED_MEDIA]
   ↓ match
[Parse]
   ├── ParserEngineException (UNSUPPORTED_MIME/INVALID_PDF/IMPORT_PDF_ENCRYPTED/IMPORT_SCANNED_PDF/IMPORT_DOCX_PROTECTED/IMPORT_DOCX_NO_TEXT/IMPORT_TOO_MANY_PAGES/IMPORT_EMPTY_FILE/IMPORT_INVALID_DOCX) → [4xx ProblemDetails con `code`]
   ├── Success                                                                              → [200 ImportResult JSON]
   └── Unexpected exception                                                                → [503 ProblemDetails: IMPORT_ENGINE_ERROR]
```

## Persistencia

**NINGUNA** (mandato v0.5 + Constitution Art. III). El `byte[]` se procesa en RAM, se descarta tras el response, y NO se escribe a disco ni se loguea (NFR-001a, NFR-002a).

## Schemas TypeScript (mirror para el frontend, en `BuildCv-web`)

> El frontend define y consume los mismos shapes vía Zod (defense in depth, Constitution Art. I FR-029a).

```typescript
// En BuildCv-web/lib/api/import.ts (Zod schema)
import { z } from "zod";

export const ImportSectionSchema = z.object({
  heading: z.string().min(1).max(100),
  start: z.number().int().min(0),
  end: z.number().int().min(0),
  confidence: z.enum(["High", "Low"]),
});

export const ImportWarningSchema = z.object({
  code: z.string().min(1).max(50),
  message: z.string().min(1).max(500),
  severity: z.enum(["Info", "Warning", "Error"]),
});

export const ImportResultSchema = z.object({
  text: z.string().max(50_000),
  sections: z.array(ImportSectionSchema).max(50),
  warnings: z.array(ImportWarningSchema).max(20),
  engineVersion: z.string().regex(/^\d+\.\d+\.\d+$/),
  traceId: z.string().min(1).max(100),
});

export type ImportResult = z.infer<typeof ImportResultSchema>;
export type ImportSection = z.infer<typeof ImportSectionSchema>;
export type ImportWarning = z.infer<typeof ImportWarningSchema>;
```

## Out of Scope (persistente)

- Persistencia del archivo subido (v1 con consentimiento).
- Historial de imports (v1 con cuentas).
- Caché del texto extraído (v1, si hay métricas de re-imports).
