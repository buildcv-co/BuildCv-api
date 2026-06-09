# Data Model: 004-export-pdf

## Domain Types (inmutables, records)

```csharp
namespace BuildCv.Domain.Export;

public sealed record ExportRequest(
    string AdaptedCv,
    ValidationReport Validation,
    string CandidateName);

public sealed record ExportResult(
    byte[] Pdf,
    string Filename,
    int SizeBytes,
    PdfMetadata Metadata);

public sealed record PdfMetadata(
    DateTimeOffset GeneratedAt,
    string EngineVersion,
    string ModelVersion,
    Severity Severity,
    int InventionCount,
    TimeSpan GenerationTime);
```

## Domain Service: ValidationGate

```csharp
namespace BuildCv.Domain.Export;

/// <summary>
/// Decide si un ValidationReport permite la exportación del PDF. Hard invenciones
/// bloquean (Constitution Art. I — cero invención). Soft invenciones solo warning.
/// </summary>
public sealed class ValidationGate
{
    public bool CanExport(ValidationReport report)
    {
        return !report.Inventions.Any(i => i.InventionSeverity == InventionSeverity.Hard);
    }

    public string ExplainWhyBlocked(ValidationReport report)
    {
        if (CanExport(report))
        {
            return string.Empty;
        }

        var hardInventions = report.Inventions
            .Where(i => i.InventionSeverity == InventionSeverity.Hard)
            .Select(i => i.Claimed);

        return $"El CV adaptado tiene {hardInventions.Count()} invención(es) Hard: [{string.Join(", ", hardInventions)}]. " +
               "Regenera la adaptación con prompt más estricto antes de exportar.";
    }
}
```

## Application Layer Types

```csharp
namespace BuildCv.Application.Features.Export;

public sealed record ExportPdfCommand(
    [Required, MaxLength(50_000)] string AdaptedCv,
    [Required] ValidationReport Validation,
    [MaxLength(100)] string CandidateName) : IRequest<Result<ExportResult>>;

public interface IPdfGenerator
{
    byte[] GeneratePdf(ExportRequest request);
}
```

## Infrastructure Types

```csharp
namespace BuildCv.Infrastructure.Pdf;

public sealed class QuestPdfGenerator : IPdfGenerator
{
    /// <summary>
    /// Constructor estático: configura la licencia Community de QuestPDF antes
    /// de la primera generación. NO se hace en Program.cs (la lib exige setear
    /// la licencia antes de GeneratePdf, y el constructor estático se ejecuta
    /// al primer uso del tipo, garantizando que está lista).
    /// </summary>
    static QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(ExportRequest request)
    {
        var stream = new MemoryStream();
        Document.Create(container => ComposeDocument(container, request))
            .GeneratePdf(stream);
        return stream.ToArray();
    }
}
```

## Api Layer (DTOs HTTP)

```csharp
namespace BuildCv.Api.Contracts;

public sealed record ExportRequestDto(
    [Required, MaxLength(50_000)] string AdaptedCv,
    [Required] ValidationReportDto Validation,
    [MaxLength(100)] string CandidateName);
```

## Validation Pipeline

```
ExportPdfHandler.HandleAsync(cmd, ct)
├── 1. validator.ValidateAndThrowAsync(cmd, ct)        [400 if invalid]
├── 2. validationGate.CanExport(cmd.Validation)        [422 if Hard invención]
├── 3. generator.GeneratePdf(ExportRequest)            [503 if QuestPDF fails]
└── 4. Return Result.Ok(ExportResult) with bytes
```

## State Machine

```
[Start]
   ↓
[Validate input] ──invalid──→ [400 ProblemDetails]
   ↓ valid
[ValidationGate.CanExport]
   ↓ false                    ↓ true
[422 BlockedInvention]    [GeneratePdf]
                              ↓
                           [503 if generator fails]
                              ↓
                           [200 with PDF bytes]
```

## Persistence

**NONE** (v0 mandate, Art. III). El PDF se genera en `MemoryStream`, se retorna al cliente, y se descarta.
