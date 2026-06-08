# Data Model: 005-cv-pdf-docx-import

## Tipos del dominio (inmutables, records, en `BuildCv.Domain/Import/`)

> **Cero paquetes externos en Domain** (Constitution Art. VI). Los records siguientes son puros: sin dependencias de PdfPig, OpenXml, ni del SDK de ASP.NET.

```csharp
namespace BuildCv.Domain.Import;

/// <summary>
/// Petición de import tal como la entiende el dominio. Se construye en el handler
/// a partir del IFormFile recibido por el endpoint, pero el dominio no conoce
/// IFormFile (separación de capas, Constitution Art. VI).
/// </summary>
public sealed record ImportRequest(
    byte[] FileBytes,
    string MimeDeclared,
    string FileName,
    string TraceId);

/// <summary>
/// Resultado del parseo. Es la "semilla" que el editor (006) consume para
/// mostrar el CV al usuario antes de pegarlo al score (002) o adapt (003).
/// </summary>
public sealed record ImportResult(
    string Text,
    IReadOnlyList<DetectedSection> Sections,
    IReadOnlyList<ImportWarning> Warnings,
    string EngineVersion,
    string TraceId);

/// <summary>
/// Sección detectada por heurística de regex. confidence: High si la línea
/// contiene solo el header; Low si hay puntuación, palabras adicionales o
/// subcadena dentro de un párrafo.
/// </summary>
public sealed record DetectedSection(
    string Heading,
    int Start,
    int End,
    string Confidence);   // "High" | "Low"

/// <summary>
/// Aviso no bloqueante sobre el resultado del parseo. Severity: Info (imágenes
/// omitidas), Warning (encoding normalizado, sección ambigua), Error (escaneado,
/// cifrado — pero los Error bloqueantes se mapean a excepción de dominio y
/// 422, no se mezclan aquí).
/// </summary>
public sealed record ImportWarning(
    string Code,
    string Message,
    string Severity);     // "Info" | "Warning" | "Error"
```

## Excepciones de dominio (`BuildCv.Domain/Import/Exceptions/`)

El parser lanza estas excepciones tipadas; el handler las mapea a códigos de error HTTP.

```csharp
namespace BuildCv.Domain.Import.Exceptions;

public abstract class ImportException : Exception
{
    public abstract string Code { get; }
    public abstract int HttpStatus { get; }
    protected ImportException(string message) : base(message) { }
    protected ImportException(string message, Exception inner) : base(message, inner) { }
}

public sealed class PdfEncryptedException : ImportException
{
    public override string Code => "IMPORT_PDF_ENCRYPTED";
    public override int HttpStatus => 422;
    public PdfEncryptedException() : base("PDF cifrado.") { }
}

public sealed class ScannedPdfException : ImportException
{
    public override string Code => "IMPORT_SCANNED_PDF";
    public override int HttpStatus => 422;
    public ScannedPdfException() : base("PDF basado en imágenes, sin texto extraíble.") { }
}

public sealed class DocxProtectedException : ImportException
{
    public override string Code => "IMPORT_DOCX_PROTECTED";
    public override int HttpStatus => 422;
    public DocxProtectedException() : base("DOCX protegido con contraseña.") { }
}

public sealed class DocxNoTextException : ImportException
{
    public override string Code => "IMPORT_DOCX_NO_TEXT";
    public override int HttpStatus => 422;
    public DocxNoTextException() : base("DOCX sin texto extraíble.") { }
}

public sealed class TooManyPagesException : ImportException
{
    public override string Code => "IMPORT_TOO_MANY_PAGES";
    public override int HttpStatus => 422;
    public int PageCount { get; }
    public TooManyPagesException(int pageCount)
        : base($"Documento con {pageCount} páginas (máx. 100).")
    {
        PageCount = pageCount;
    }
}

public sealed class EmptyFileException : ImportException
{
    public override string Code => "IMPORT_EMPTY_FILE";
    public override int HttpStatus => 422;
    public EmptyFileException() : base("Archivo vacío.") { }
}

public sealed class UnsupportedMediaException : ImportException
{
    public override string Code => "IMPORT_UNSUPPORTED_MEDIA";
    public override int HttpStatus => 415;
    public UnsupportedMediaException(string mime) : base($"MIME no soportado: {mime}.") { }
}

public sealed class TooLargeException : ImportException
{
    public override string Code => "IMPORT_TOO_LARGE";
    public override int HttpStatus => 413;
    public long SizeBytes { get; }
    public TooLargeException(long sizeBytes)
        : base($"Archivo de {sizeBytes} bytes (máx. 5 MB).")
    {
        SizeBytes = sizeBytes;
    }
}
```

## Servicio de dominio: heurística de secciones

```csharp
namespace BuildCv.Domain.Import;

/// <summary>
/// Detecta secciones candidatas por regex sobre headers en MAYÚSCULAS.
/// Es una función pura: entra texto, sale lista de DetectedSection. Sin IO.
/// </summary>
public static class SectionHeuristics
{
    private static readonly System.Text.RegularExpressions.Regex HeaderPattern = new(
        @"^\s*(?<heading>" + string.Join("|", SectionRegexPatterns.AllHeaders) + @")\s*$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Multiline);

    public static IReadOnlyList<DetectedSection> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<DetectedSection>();
        }

        var matches = HeaderPattern.Matches(text);
        var sections = new List<DetectedSection>(matches.Count);

        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var heading = m.Groups["heading"].Value;
            var start = m.Index + m.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;

            // Confidence: High si la línea solo tiene el header (sin puntuación ni más palabras).
            var line = text.Substring(m.Index, m.Length).Trim();
            var confidence = line.Equals(heading, StringComparison.Ordinal) ? "High" : "Low";

            sections.Add(new DetectedSection(heading, start, end, confidence));
        }

        return sections;
    }
}

public static class SectionRegexPatterns
{
    public static readonly string[] Spanish = new[]
    {
        "EXPERIENCIA", "EDUCACION", "EDUCACIÓN", "HABILIDADES",
        "PROYECTOS", "CONTACTO", "PERFIL", "RESUMEN",
        "IDIOMAS", "CERTIFICACIONES", "REFERENCIAS", "PUBLICACIONES"
    };

    public static readonly string[] English = new[]
    {
        "EXPERIENCE", "EDUCATION", "SKILLS", "PROJECTS",
        "CONTACT", "PROFILE", "SUMMARY", "LANGUAGES",
        "CERTIFICATIONS", "REFERENCES", "PUBLICATIONS"
    };

    public static readonly string[] AllHeaders = Spanish.Concat(English).ToArray();
}
```

## Tipos de Application (puertos y handler)

```csharp
namespace BuildCv.Application.Features.Import;

using BuildCv.Domain.Import;

/// <summary>
/// Puerto de parseo (Constitution Art. VI v1.1.0: ICvParser es un puerto oficial).
/// </summary>
public interface ICvParser
{
    /// <summary>
    /// Parsea el archivo y devuelve un ImportResult. Lanza ImportException
    /// en errores bloqueantes (cifrado, escaneado, protegido, vacío, etc.).
    /// </summary>
    ImportResult Parse(ImportRequest request);
}

/// <summary>
/// Servicio de aplicación que orquesta: validator (FluentValidation) → parser.
/// </summary>
public sealed record ImportCvCommand(byte[] FileBytes, string MimeDeclared, string FileName) : IRequest<Result<ImportResult>>;
```

## Adaptadores de Infrastructure (en `BuildCv.Infrastructure/Parsing/`)

> Aquí sí se referencian PdfPig y OpenXml. Aislados en esta capa.

```csharp
namespace BuildCv.Infrastructure.Parsing;

using BuildCv.Application.Features.Import;
using BuildCv.Domain.Import;
using BuildCv.Domain.Import.Exceptions;
using UglyToad.PdfPig;

public sealed class PdfPigCvParser : ICvParser
{
    public ImportResult Parse(ImportRequest request)
    {
        if (request.FileBytes.Length == 0) throw new EmptyFileException();

        try
        {
            using var document = PdfDocument.Open(request.FileBytes);
            var pageCount = document.NumberOfPages;
            if (pageCount > 100) throw new TooManyPagesException(pageCount);

            var sb = new System.Text.StringBuilder();
            var warnings = new List<ImportWarning>();
            var textLengthAcrossPages = 0;

            foreach (var page in document.GetPages())
            {
                var pageText = page.Text ?? string.Empty;
                textLengthAcrossPages += pageText.Length;

                // Detección de PDF escaneado: si 0 chars de texto en todas las páginas
                // y el PDF tiene más de 0 páginas → no es un PDF de texto.
                // (Chequeo se hace al final.)

                sb.AppendLine(pageText);
            }

            var text = sb.ToString().Trim();

            if (textLengthAcrossPages == 0)
            {
                throw new ScannedPdfException();
            }

            // Truncar si excede 50k chars (coherente con FR-037)
            if (text.Length > 50_000)
            {
                warnings.Add(new ImportWarning(
                    "TEXT_TRUNCATED",
                    $"Texto truncado de {text.Length} a 50000 caracteres.",
                    "Warning"));
                text = text.Substring(0, 50_000);
            }

            var sections = SectionHeuristics.Detect(text);
            if (sections.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    "NO_SECTIONS_DETECTED",
                    "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                    "Info"));
            }

            return new ImportResult(
                text,
                sections,
                warnings,
                EngineVersion: "1.0.0",
                TraceId: request.TraceId);
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new PdfEncryptedException();
        }
    }
}
```

```csharp
namespace BuildCv.Infrastructure.Parsing;

using BuildCv.Application.Features.Import;
using BuildCv.Domain.Import;
using BuildCv.Domain.Import.Exceptions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

public sealed class OpenXmlCvParser : ICvParser
{
    public ImportResult Parse(ImportRequest request)
    {
        if (request.FileBytes.Length == 0) throw new EmptyFileException();

        try
        {
            using var ms = new MemoryStream(request.FileBytes);
            using var doc = WordprocessingDocument.Open(ms, false);

            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) throw new DocxNoTextException();

            var sb = new System.Text.StringBuilder();
            var warnings = new List<ImportWarning>();
            var imageCount = 0;

            foreach (var element in body.Elements())
            {
                if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph p)
                {
                    var text = p.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
                else if (element is DocumentFormat.OpenXml.Wordprocessing.Table t)
                {
                    foreach (var row in t.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                    {
                        var cells = row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                            .Select(c => c.InnerText);
                        sb.AppendLine(string.Join('\t', cells));
                    }
                }
                else if (element is DocumentFormat.OpenXml.Wordprocessing.SdtBlock sdt)
                {
                    // Contenido estructurado (controles, contenido reutilizable): tratarlo como párrafo.
                    var text = sdt.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            // Contar imágenes referenciadas en el documento.
            imageCount = doc.MainDocumentPart?.ImageParts?.Count() ?? 0;
            if (imageCount > 0)
            {
                warnings.Add(new ImportWarning(
                    "IMAGE_OMITTED",
                    $"Se omitieron {imageCount} imagen(es).",
                    "Info"));
            }

            var text = sb.ToString().Trim();
            if (text.Length == 0) throw new DocxNoTextException();

            if (text.Length > 50_000)
            {
                warnings.Add(new ImportWarning(
                    "TEXT_TRUNCATED",
                    $"Texto truncado de {text.Length} a 50000 caracteres.",
                    "Warning"));
                text = text.Substring(0, 50_000);
            }

            var sections = SectionHeuristics.Detect(text);
            if (sections.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    "NO_SECTIONS_DETECTED",
                    "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                    "Info"));
            }

            return new ImportResult(
                text,
                sections,
                warnings,
                EngineVersion: "1.0.0",
                TraceId: request.TraceId);
        }
        catch (OpenXmlPackageException) when (IsPasswordProtection(/*...*/))
        {
            throw new DocxProtectedException();
        }
        catch (OpenXmlPackageException)
        {
            throw new UnsupportedMediaException(request.MimeDeclared);
        }
    }
}
```

## Tipos de API (DTOs HTTP)

```csharp
namespace BuildCv.Api.Contracts;

public sealed record ImportResponseDto(
    string Text,
    IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<WarningDto> Warnings,
    string EngineVersion,
    string TraceId);

public sealed record SectionDto(string Heading, int Start, int End, string Confidence);

public sealed record WarningDto(string Code, string Message, string Severity);

public static class ImportResponseMapper
{
    public static ImportResponseDto Map(ImportResult result) => new(
        result.Text,
        result.Sections.Select(s => new SectionDto(s.Heading, s.Start, s.End, s.Confidence)).ToList(),
        result.Warnings.Select(w => new WarningDto(w.Code, w.Message, w.Severity)).ToList(),
        result.EngineVersion,
        result.TraceId);
}
```

## Pipeline de validación

```
ImportEndpoints.POST /api/v1/import
├── 1. Kestrel MaxRequestBodySize = 6_000_000         [413 si > 6 MB]
├── 2. ImportCvValidator.ValidateAndThrow(cmd)        [400 si falla]
│      ├── FileBytes.Length > 0                       [IMPORT_EMPTY_FILE → 400]
│      ├── FileBytes.Length ≤ 5_000_000               [IMPORT_TOO_LARGE → 400]
│      └── MimeDeclared ∈ {pdf, docx}                 [400 con detalle]
├── 3. ICvParserDispatcher.Dispatch(cmd)              [selecciona PdfPig u OpenXml]
│      ├── Magic bytes check                          [415 si no coincide]
│      └── Parse(request)                             [puede lanzar ImportException → mapeo]
├── 4. RequireRateLimiting("import") 30/h             [429 si excede]
└── 5. Return ImportResponseDto (200)
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
   ├── ImportException (cifrado/escaneado/protegido) → [422 ProblemDetails]
   ├── Success                                          → [200 ImportResult JSON]
   └── Unexpected exception                             → [503 ProblemDetails: IMPORT_ENGINE_ERROR]
```

## Persistencia

**NINGUNA** (mandato v0.5 + Constitution Art. III). El `byte[]` se procesa en RAM, se descarta tras el response, y NO se escribe a disco ni se loguea (NFR-001a, NFR-002a).

## Schemas TypeScript (mirror para el frontend, en `BuildCv-web`)

> El frontend define y consume los mismos shapes vía Zod (defense in depth, Constitution Art. I FR-029a).

```typescript
// En BuildCv-web/lib/api/import.ts (Zod schema)
import { z } from "zod";

export const DetectedSectionSchema = z.object({
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
  sections: z.array(DetectedSectionSchema).max(50),
  warnings: z.array(ImportWarningSchema).max(20),
  engineVersion: z.string().regex(/^\d+\.\d+\.\d+$/),
  traceId: z.string().min(1).max(100),
});

export type ImportResult = z.infer<typeof ImportResultSchema>;
export type DetectedSection = z.infer<typeof DetectedSectionSchema>;
export type ImportWarning = z.infer<typeof ImportWarningSchema>;
```

## Out of Scope (persistente)

- Persistencia del archivo subido (v1 con consentimiento).
- Historial de imports (v1 con cuentas).
- Caché del texto extraído (v1, si hay métricas de re-imports).
