# Data Model: 003-adapt-ia

## Domain Types (inmutables, records)

```csharp
namespace BuildCv.Domain.Adapt;

/// Resultado de la adaptación con validación.
public sealed record AdaptationResult(
    string AdaptedCv,
    ValidationReport Validation,
    string EngineVersion,
    string AiModel);

/// Reporte de validación post-IA. SIEMPRE presente, incluso si IsValid=true.
public sealed record ValidationReport(
    bool IsValid,
    Severity Severity,
    IReadOnlyList<EntityInvention> Inventions,
    IReadOnlyList<string> Warnings);

/// Una entidad que aparece en el CV adaptado pero NO en el original.
public sealed record EntityInvention(
    InventionType Type,
    string Claimed,
    string? Original,
    InventionSeverity Severity,
    int Position);

public enum InventionType
{
    Skill,
    Certification,
    Company,
    Date,
    Metric,
    Title,    // ej. "Senior" auto-atribuido
    Other
}

public enum InventionSeverity
{
    Soft,    // ej. métrica redondeada, skill relacionada
    Hard     // ej. empresa inventada, cert inventada, fecha fabricada
}

public enum Severity
{
    None,        // 0 invenciones
    Warning,     // 1-2 soft
    Critical     // 1+ hard o ≥3 soft
}
```

## Application Layer Types

```csharp
namespace BuildCv.Application.Features.Adapt;

public sealed record AdaptCvCommand(
    string CvText,
    string JobText,
    bool Stream = false) : IRequest<Result<AdaptationResult>>;

/// Puerto: abstracción del cliente IA. Domain y Application NO saben que existe Anthropic.
public interface IAiClient
{
    /// Llamada sin streaming.
    Task<string> CompleteAsync(string prompt, CancellationToken ct);

    /// Llamada con streaming. Yield retorna chunks de texto.
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
}
```

## Infrastructure Types

```csharp
namespace BuildCv.Infrastructure.Ai;

public sealed class AnthropicOptions
{
    public string ApiKey { get; set; } = "";  // required, from configuration
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 4096;
    public bool ZeroDataRetention { get; set; } = false;  // requires Enterprise
}

/// Implementación de IAiClient. Único lugar donde aparece el SDK de Anthropic.
public sealed class AnthropicAiClient(
    AnthropicClient client,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicAiClient> logger) : IAiClient
{
    // ...
}
```

## Api Layer (DTOs HTTP)

```csharp
namespace BuildCv.Api.Contracts;

public sealed record AdaptRequestDto(
    [Required, MaxLength(50_000)] string CvText,
    [Required, MaxLength(20_000)] string JobText);

public sealed record AdaptResponseDto(
    string AdaptedCv,
    ValidationReportDto Validation,
    string EngineVersion,
    string AiModel);

public sealed record ValidationReportDto(
    bool IsValid,
    string Severity,
    IReadOnlyList<EntityInventionDto> Inventions,
    IReadOnlyList<string> Warnings);

public sealed record EntityInventionDto(
    string Type,
    string Claimed,
    string? Original,
    string Severity,
    int Position);
```

## Validation Pipeline

```
AdaptCvHandler.HandleAsync(cmd, ct)
├── 1. validator.ValidateAndThrowAsync(cmd, ct)
├── 2. Extract entities from CV original → OriginalEntities
├── 3. Call IAiClient.CompleteAsync(prompt) → AdaptedCv
├── 4. Extract entities from AdaptedCv → AdaptedEntities
├── 5. CrossEntityValidator.Validate(OriginalEntities, AdaptedEntities) → ValidationReport
│   ├── If Severity == Critical AND retry available → loop back to step 3 with stricter prompt (max 1 retry)
│   └── Return Result.Ok(AdaptationResult)
└── 6. Log structured (no PII): "Adapt completed (cvLen, jobLen, model, severity, retryCount, traceId)"
```

## State Machine: Adaptation Flow

```
[Start]
   ↓
[Validate input] ──invalid──→ [400 ProblemDetails]
   ↓ valid
[Extract original entities]
   ↓
[Call LLM] ──error──→ [503 ProblemDetails + fallback to deterministic]
   ↓ success
[Extract adapted entities]
   ↓
[Cross-validate] ──Critical + retry left──→ [Loop: stricter prompt]
   ↓                              ↓
   ↓                          retry==0
[Return AdaptationResult]    [Return with WARNING]
```

## Persistence

**NONE** (v0 mandate, Art. III). Todos los tipos viven solo en memoria durante la request.
