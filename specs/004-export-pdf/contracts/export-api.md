# Contracts: 004-export-pdf

## HTTP Contracts

### POST /api/v1/export (sincrónico, retorna PDF binario)

```http
POST /api/v1/export HTTP/1.1
Host: api.buildcv.app
Content-Type: application/json
```

**Request Body**:
```json
{
  "adaptedCv": "string (max 50000, markdown simple)",
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
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="cv-adapted-2026-06-08.pdf"
Content-Length: 123456
<binary PDF data — %PDF-1.x header>
```

**Response 400 Bad Request** (validation):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "AdaptedCv": ["The field AdaptedCv must be a string with a maximum length of 50000."],
    "CandidateName": ["The field CandidateName must be a string with a maximum length of 100."]
  }
}
```

**Response 422 Unprocessable Entity** (Hard invención):
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Export bloqueado por invención",
  "status": 422,
  "detail": "El CV adaptado tiene 1 invención(es) Hard: [FakeCorp]. Regenera la adaptación con prompt más estricto antes de exportar.",
  "instance": "/api/v1/export",
  "inventions": [
    {
      "type": "Company",
      "claimed": "FakeCorp",
      "original": null,
      "severity": "Hard",
      "position": 0
    }
  ]
}
```

**Response 429 Too Many Requests** (rate-limit "export" 20/h):
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Has alcanzado el tope de exportaciones (20/hora). El análisis determinista y la adaptación siguen disponibles.",
  "instance": "/api/v1/export",
  "retryAfter": "2026-06-08T16:30:00Z"
}
```

**Response 503 Service Unavailable** (QuestPDF falló):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "La generación de PDF no está disponible temporalmente. Intenta de nuevo en unos minutos.",
  "instance": "/api/v1/export"
}
```

## Domain Contracts (C# interfaces)

### IPdfGenerator

```csharp
namespace BuildCv.Application.Features.Export;

public interface IPdfGenerator
{
    byte[] GeneratePdf(ExportRequest request);
}
```

### ValidationGate

```csharp
namespace BuildCv.Domain.Export;

public sealed class ValidationGate
{
    public bool CanExport(ValidationReport report);
    public string ExplainWhyBlocked(ValidationReport report);
}
```

## Configuration Contract

```json
{
  "RateLimit": {
    "Export": {
      "PermitLimit": 20,
      "Window": "01:00:00"
    }
  },
  "QuestPDF": {
    "LicenseType": "Community"
  }
}
```

## Logging Contract (Serilog structured)

```csharp
// ✓ Allowed
Log.Information("PDF generated (cvLength={CvLen}, fileSize={FileSize}, generationTimeMs={TimeMs}, severity={Severity}, traceId={TraceId})",
    cv.Length, pdfBytes.Length, stopwatch.ElapsedMilliseconds, severity, traceId);

// ✗ Prohibited (Constitution Art. III)
Log.Information("PDF: {Pdf}", pdfBytes);          // NUNCA contenido binario
Log.Information("CV: {Cv}", cv);                  // NUNCA contenido
```
