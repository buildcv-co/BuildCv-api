# Contracts: 003-adapt-ia

> **Reality check (post-shipped, commit `68baaf2`):** La implementación shipped NO incluye SSE ni endpoint `/api/v1/adapt/stream`. El endpoint es **únicamente** `POST /api/v1/adapt` sincrónico. La IAiClient implementada es `StubAiClient` (sin LLM real). El spec original proponía un endpoint SSE y `AnthropicAiClient` — ambos fueron rechazados para v0 y quedan como follow-up v1 detrás del mismo puerto `IAiClient`.

## HTTP Contracts

### POST /api/v1/adapt (síncrono, v0)

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

> **No** hay `bool stream` en el request. El endpoint es sincrónico en v0.

**Response 200 OK**:
```json
{
  "adaptedCv": "string",
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

> El campo `aiModel` reporta `"claude-sonnet-4-20250514"` por consistencia del contrato, pero la implementación actual es `StubAiClient` (sin LLM real). En v1, cuando se habilite `AnthropicAiClient`, este campo reflejará el modelo real.

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

**Response 503 Service Unavailable** (IA down / stub threw):
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

### ~~GET /api/v1/adapt/stream (SSE)~~ — NO IMPLEMENTADO EN v0

> El spec original proponía un endpoint SSE para streaming de la adaptación con eventos `start` / `token` / `:ping` / `validation` / `done`. **NO** está implementado en v0. La implementación shipped es sincrónica. Cuando se habilite un LLM real en v1, se reintroducirá con el patrón `Results.ServerSentEvents` de .NET 10.

## Domain Contracts (C# interfaces)

### IAiClient (puerto, shipped en commit `68baaf2`)

```csharp
namespace BuildCv.Application.Features.Adapt;

/// <summary>
/// Puerto de IO para el proveedor de IA. La capa Domain y Application NO saben
/// qué proveedor existe (Anthropic, OpenAI, etc.). La implementación vive en
/// Infrastructure (Constitution Art. VI — Clean Arch).
/// </summary>
public interface IAiClient
{
    /// <summary>Llamada sincrónica. Devuelve el texto adaptado completo.</summary>
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
}
```

> **NO** hay `IAsyncEnumerable<string> StreamAsync(...)` — el stub solo implementa `CompleteAsync` y el endpoint es sincrónico.

### SeverityPolicy (domain, shipped)

```csharp
namespace BuildCv.Domain.Adapt;

public sealed class SeverityPolicy
{
    public Severity Classify(IReadOnlyList<EntityInvention> inventions);
}
```

### CrossEntityValidator (domain, shipped)

```csharp
namespace BuildCv.Domain.Adapt;

public sealed class CrossEntityValidator
{
    public ValidationReport Validate(
        IReadOnlyList<string> originalEntities,
        IReadOnlyList<string> adaptedEntities,
        IReadOnlyDictionary<string, InventionType> entityTypes);
}
```

## Configuration Contract

> **Diferencias con el plan original:** en v0 NO se requiere `Ai:ApiKey` ni `Ai:Model` en configuración. El stub no hace IO ni llama a ningún proveedor. El bloque siguiente documenta la configuración que se requerirá en v1 cuando se habilite un LLM real (preservado del plan original como referencia).

```json
{
  "Ai": {
    "ApiKey": "env:Ai__ApiKey (REQUIRED for v1, from IConfiguration)",
    "Model": "claude-sonnet-4-20250514 (default for v1)",
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

## Logging Contract (Console.WriteLine, structured)

```csharp
// ✓ Allowed
Console.WriteLine("Adapt completed (cvLength={CvLen}, jobLength={JobLen}, severity={Severity}, inventions={InventionsCount}, traceId={TraceId})",
    cv.Length, job.Length, severity, inventionsCount, traceId);

// ✗ Prohibited (Constitution Art. III)
Console.WriteLine("CV: {Cv}", cv);  // NUNCA contenido
Console.WriteLine("Adapted: {Adapted}", adapted);  // NUNCA contenido
Console.WriteLine("Job: {Job}", job);  // NUNCA contenido
```
