# Data Model: 003-adapt-ia

> **Source of truth:** `src/BuildCv.Domain/Adapt/AdaptationTypes.cs` (commit `68baaf2`). Todos los tipos viven en un único archivo `AdaptationTypes.cs` (no en archivos separados como sugería el plan original).

## Domain Types (inmutables, records)

```csharp
namespace BuildCv.Domain.Adapt;

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

public sealed record EntityInvention(
    InventionType Type,
    string Claimed,
    string? Original,
    InventionSeverity InventionSeverity,
    int Position);

public sealed record ValidationReport(
    bool IsValid,
    Severity Severity,
    IReadOnlyList<EntityInvention> Inventions,
    IReadOnlyList<string> Warnings);

public sealed record AdaptationResult(
    string AdaptedCv,
    ValidationReport Validation,
    string EngineVersion,
    string AiModel);
```

## Application Layer Types

```csharp
namespace BuildCv.Application.Features.Adapt;

public sealed record AdaptCvCommand(
    string CvText,
    string JobText);
```

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

> **Diferencias con el plan original:**
> - `AdaptCvCommand` NO tiene `bool Stream` — el endpoint es sincrónico en v0.
> - `IAiClient` NO expone `IAsyncEnumerable<string> StreamAsync(...)` — el stub solo implementa `CompleteAsync`.

## Infrastructure Types

```csharp
namespace BuildCv.Infrastructure.Ai;

/// <summary>
/// Implementación v0 del IAiClient. NO usa un LLM real — retorna una versión
/// "marco" del CV original con la keyword de la vacante highlighted, sin
/// agregar contenido. Esto permite probar el flujo end-to-end en v0
/// (v0 no llama al proveedor real — M1 lo habilitará con clave Anthropic).
///
/// Constitution compliance: Art. I (no invención — solo reorganiza), Art. III
/// (sin persistencia, sin IO), Art. IX (sin ZDR claim — v0 no usa LLM).
/// </summary>
public sealed class StubAiClient : IAiClient
{
    public Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Retorna CV "marco" determinista, sin agregar contenido.
        return Task.FromResult(STUB_CV);
    }
}
```

> **Diferencias con el plan original:** NO existen `AnthropicAiClient.cs` ni `AnthropicOptions.cs`. La implementación shipped es **única y exclusivamente** `StubAiClient`. Cuando se habilite un proveedor real (v1), se agregará detrás del mismo puerto `IAiClient` sin tocar Application/Domain.

## Api Layer (DTOs HTTP)

```csharp
namespace BuildCv.Api.Contracts;

public sealed record AdaptRequestDto(
    [Required, MaxLength(50_000)] string CvText,
    [Required, MaxLength(20_000)] string JobText);

public sealed record EntityInventionDto(
    string Type,
    string Claimed,
    string? Original,
    string Severity,
    int Position);

public sealed record ValidationReportDto(
    bool IsValid,
    string Severity,
    IReadOnlyList<EntityInventionDto> Inventions,
    IReadOnlyList<string> Warnings);

public sealed record AdaptResponseDto(
    string AdaptedCv,
    ValidationReportDto Validation,
    string EngineVersion,
    string AiModel);
```

> **Diferencias con el plan original:** `AdaptRequestDto` NO tiene `bool Stream`. `AdaptResponseDto` no tiene `DeltaDeMejoraDto` ni `ChangesDto` — el delta de mejora (que el spec original prometía en US2) no se implementó en v0 porque requiere tracking por chunk, y el stub retorna el CV completo en una sola llamada.

## Validation Pipeline (AdaptCvHandler)

```
AdaptCvHandler.Handle(command, ct)
├── 1. Extract entities from CV original → OriginalEntities     [EntityExtractor]
├── 2. Build prompt (PromptBuilder.Build)                        [nonces + bloques delimitados]
├── 3. Call IAiClient.CompleteAsync(prompt, ct) → AdaptedCv      [StubAiClient en v0]
├── 4. Extract entities from AdaptedCv → AdaptedEntities         [EntityExtractor]
├── 5. CrossEntityValidator.Validate(original, adapted, types) → report
├── 6. SeverityPolicy.Classify(report.Inventions) → finalSeverity
├── 7. Build ValidationReport (IsValid = (severity != Critical))
├── 8. Return Result.Success(AdaptationResult)
└── 9. Log structured: "Adapt completed (cvLength, jobLength, severity, inventions, traceId)"
```

> **Diferencias con el plan original:** el handler es **lineal**. NO hay loop de reintento por severidad. NO hay "auto-regen con prompt más estricto". El `AdaptCvHandler` (`src/BuildCv.Application/Features/Adapt/AdaptCvHandler.cs:37-79`) es un flujo straight-through.

## State Machine: Adaptation Flow

```
[Start]
   ↓
[Extract original entities]
   ↓
[Call IAiClient] ──error──→ [503 ProblemDetails + Result.Failure("AI_UNAVAILABLE")]
   ↓ success
[Extract adapted entities]
   ↓
[Cross-validate] ──Critical──→ [AdaptationResult con Severity=Critical, IsValid=false]
   ↓                              ↓
[Return AdaptationResult]    [AdaptationResult con Warning/None]
```

## Persistence

**NONE** (v0 mandate, Art. III). Todos los tipos viven solo en memoria durante la request. El log estructurado (Console.WriteLine) solo emite metadatos: `cvLength`, `jobLength`, `severity`, `inventionsCount`, `traceId` (NFR-002).
