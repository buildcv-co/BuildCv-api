# Contracts: 003-adapt-ia

## HTTP Contracts

### POST /api/v1/adapt (sincrónico)

```http
POST /api/v1/adapt HTTP/1.1
Host: api.buildcv.app
Content-Type: application/json
```

**Request Body**:
```json
{
  "cvText": "string (max 50000)",
  "jobText": "string (max 20000)"
}
```

**Response 200 OK**:
```json
{
  "adaptedCv": "string (max 50000)",
  "validation": {
    "isValid": true,
    "severity": "None|Warning|Critical",
    "inventions": [
      {
        "type": "Skill|Certification|Company|Date|Metric|Title|Other",
        "claimed": "string",
        "original": "string|null",
        "severity": "Soft|Hard",
        "position": 0
      }
    ],
    "warnings": ["string"]
  },
  "engineVersion": "1.0.0",
  "aiModel": "claude-sonnet-4-20250514"
}
```

**Response 400 Bad Request** (validation):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CvText": ["The field CvText must be a string with a maximum length of 50000."],
    "JobText": ["The field JobText must be a string with a maximum length of 20000."]
  },
  "traceId": "0HMU..."
}
```

**Response 429 Too Many Requests** (rate limit):
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Has alcanzado el tope de adaptaciones (5/hora). El análisis determinista sigue disponible.",
  "instance": "/api/v1/adapt",
  "retryAfter": "2026-06-08T16:30:00Z",
  "traceId": "0HMU..."
}
```

**Response 503 Service Unavailable** (IA down):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "La adaptación con IA no está disponible temporalmente. Usa el análisis determinista (/api/v1/score).",
  "instance": "/api/v1/adapt",
  "traceId": "0HMU..."
}
```

### GET /api/v1/adapt/stream (SSE)

```http
GET /api/v1/adapt/stream HTTP/1.1
Host: api.buildcv.app
Content-Type: application/json
Accept: text/event-stream

{
  "cvText": "string",
  "jobText": "string"
}
```

**Response 200 OK** (text/event-stream):

```
event: start
data: {"requestId": "abc-123", "timestamp": "2026-06-08T14:30:00Z"}

event: token
data: {"text": "Juan"}

event: token
data: {"text": " Pérez"}

event: token
data: {"text": ". Backend"}

: ping
: ping

event: validation
data: {"isValid": true, "severity": "None", "inventions": [], "warnings": []}

event: done
data: {"engineVersion": "1.0.0", "aiModel": "claude-sonnet-4-20250514"}
```

**Eventos**:
- `start`: conexión establecida, requestId asignado.
- `token`: chunk de texto del LLM.
- `: ping` (SSE comment): keep-alive cada 20s (Render.com no cierra conexión).
- `validation`: resultado de validación post-IA al finalizar.
- `done`: stream completado, metadata.
- `error` (si aplica): `{"code": "RATE_LIMIT", "message": "..."}`.

## Domain Contracts (C# interfaces)

### IAiClient

```csharp
namespace BuildCv.Application.Features.Adapt;

public interface IAiClient
{
    Task<string> CompleteAsync(string prompt, CancellationToken ct);

    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
```

### CrossEntityValidator

```csharp
namespace BuildCv.Domain.Adapt;

public interface ICrossEntityValidator
{
    ValidationReport Validate(
        IReadOnlySet<string> originalEntities,
        IReadOnlySet<string> adaptedEntities,
        IReadOnlyDictionary<string, InventionType> entityTypes);
}
```

### SeverityPolicy

```csharp
namespace BuildCv.Domain.Adapt;

public interface ISeverityPolicy
{
    Severity Classify(IReadOnlyList<EntityInvention> inventions);
}
```

## Configuration Contract

```json
{
  "Ai": {
    "ApiKey": "env:Ai__ApiKey (REQUIRED, from IConfiguration)",
    "Model": "claude-sonnet-4-20250514 (default)",
    "MaxTokens": 4096,
    "ZeroDataRetention": false
  },
  "RateLimit": {
    "Ai": {
      "PermitLimit": 5,
      "Window": "01:00:00"
    }
  }
}
```

## Logging Contract (Serilog structured)

```csharp
// ✓ Allowed
Log.Information("Adapt completed (cvLength={CvLen}, jobLength={JobLen}, model={Model}, severity={Severity}, retryCount={Retry}, traceId={TraceId})",
    cv.Length, job.Length, model, severity, retry, traceId);

// ✗ Prohibited
Log.Information("CV: {Cv}", cv);  // NUNCA contenido
Log.Information("Adapted: {Adapted}", adapted);  // NUNCA contenido
Log.Information("Job: {Job}", job);  // NUNCA contenido
```
