# Regla · Convenciones C# / .NET

> Esta regla opera **bajo** la Constitución (Art. VI). Cita el artículo cuando la justifiques.

## Configuración del compilador (no negociable, ya en `Directory.Build.props`)

```xml
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

→ Cualquier `.csproj` puede **omitir** estas props; se heredan. No las dupliques.

## File-scoped namespaces (warning si no se cumple)

```csharp
namespace BuildCv.Domain.Scoring;   // ✅

namespace BuildCv.Domain.Scoring   // ❌
{
    public sealed class ScoringEngine { }
}
```

## `using` directives al inicio del archivo, **fuera** del namespace

```csharp
using System.Collections.Immutable;
using BuildCv.Domain.Common;
using FluentValidation;

namespace BuildCv.Application.Features.Scoring;
```

## Records para DTOs, `sealed` por defecto en clases

```csharp
public sealed record ScoreResponse(
    int Score,
    string Band,
    IReadOnlyList<ComponentBreakdown> Components,
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing,
    string EngineVersion);

public sealed record ComponentBreakdown(string Code, double Weight, double Value, string Rationale);
```

Inmutabilidad por defecto. Listas inmutables (`IReadOnlyList<T>`, `ImmutableArray<T>`) en boundaries públicos.

## Primary constructors cuando aporten (Art. VI — "no sobre-ingeniería")

```csharp
public sealed class ScoreCvHandler(
    IScoringEngine engine,
    IValidator<ScoreCvCommand> validator)
{
    public async Task<Result<ScoreResponse>> HandleAsync(ScoreCvCommand cmd, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(cmd, ct);
        // ...
    }
}
```

`var` solo cuando el tipo es obvio a la derecha. Si el tipo **no** es obvio, escríbelo explícito:

```csharp
var sb = new StringBuilder();                          // ✅ tipo obvio
ImmutableArray<MatchResult> matches = matcher.Match(); // ✅ tipo NO obvio a la derecha
```

## `Result<T>` en dominio, **no** excepciones para fallos esperados

```csharp
public readonly record struct Result<T>(bool IsSuccess, T? Value, DomainError? Error)
{
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(DomainError error) => new(false, default, error);
}
```

`Exception` solo para **imposibles / bugs** (estado corrupto, contrato violado). Fallos de validación, normalización o parseo van por `Result<T>` o `OneOf<T, DomainError>`.

## FluentValidation en Application, no `[Attribute]` en DTOs

```csharp
public sealed class ScoreCvValidator : AbstractValidator<ScoreCvCommand>
{
    public ScoreCvValidator()
    {
        RuleFor(x => x.CvText).NotEmpty().MinimumLength(200).MaximumLength(20_000);
        RuleFor(x => x.JobText).NotEmpty().MinimumLength(100).MaximumLength(20_000);
        RuleFor(x => x).Must(NotBeIdentical).WithMessage("El CV y la vacante no pueden ser idénticos.");
    }

    private static bool NotBeIdentical(ScoreCvCommand c) =>
        !string.Equals(c.CvText.Trim(), c.JobText.Trim(), StringComparison.Ordinal);
}
```

El `ValidationFilter` en `Api/Filters/` traduce `ValidationException` a 400 ProblemDetails. **No** metas lógica de validación en el endpoint.

## Minimal APIs (no MVC controllers)

```csharp
public static class ScoringEndpoints
{
    public static IEndpointRouteBuilder MapScoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/score")
            .WithTags("Scoring")
            .RequireRateLimiting("deterministic");

        group.MapPost("/", async (ScoreCvCommand cmd, ScoreCvHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(cmd, ct);
                return result.IsSuccess
                    ? Results.Ok(ScoreResponseMapper.ToDto(result.Value!))
                    : Results.Problem(detail: result.Error!.Message, statusCode: 400);
            })
            .WithName("Score")
            .Produces<ScoreResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
```

## Endpoints en `Api/Endpoints/<Feature>Endpoints.cs`

Un archivo por feature. `Map<Feature>Endpoints` como extension method sobre `IEndpointRouteBuilder`. El handler se inyecta directamente; **no** `IServiceProvider.GetRequiredService` en el lambda.

## Versionado y OpenAPI

- `/api/v{version:apiVersion}/...` con `Asp.Versioning` (ya cableado en `Program.cs:32`).
- OpenAPI siempre registrado (`AddOpenApi`); `MapOpenApi` y `MapScalarApiReference` solo en `IsDevelopment()`.
- `WithName`, `Produces<T>`, `ProducesValidationProblem` en cada endpoint — Scalar lo necesita para renderizar bien.

## Logging estructurado sin contenido sensible

```csharp
using var _ = LogContext.PushProperty("TraceId", Activity.Current?.Id);
using var __ = LogContext.PushProperty("CvLength", cmd.CvText.Length);
using var ___ = LogContext.PushProperty("JobLength", cmd.JobText.Length);
_logger.LogInformation("Score request received");
```

Tres líneas para tres propiedades: explícito, trazable, sin PII. Nunca `LogInformation("CV: {Cv}", cmd.CvText)`.

## Health checks

- `/health/live` → "self" (proceso vivo, sin tocar IO).
- `/health/ready` → "self" + `ai-config` (verifica que la clave del proveedor de IA esté presente).
- `AiConfigHealthCheck` (en `Api/Health/`) implementa `IHealthCheck`; si la clave falta, devuelve `Unhealthy` con mensaje **sin** la clave.

## Versionado del motor de puntaje (Art. II, FR-013)

```csharp
public sealed record ScoreResult(
    int Score,
    string Band,
    IReadOnlyList<ComponentBreakdown> Components,
    // ...
    EngineMetadata Engine)
{
    public sealed record EngineMetadata(string Version, string GazetteerVersion, string StemmerVersion);
}
```

Cada `ScoreResponse` lleva la versión del motor y de los léxicos. Cambiar la lógica de cálculo ⇒ bumpear `EngineVersion` (SemVer) en `ScoringEngine`; los tests de reproducibilidad deben actualizarse con la nueva versión.

## Dependencias por capa (recordatorio)

| Capa | Puede referenciar | NO puede referenciar |
|---|---|---|
| `BuildCv.Domain` | `Microsoft.Extensions.DependencyInjection.Abstractions` (solo) | `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`, SDKs externos |
| `BuildCv.Application` | `Domain` + `FluentValidation` + `Microsoft.Extensions.*` | `Infrastructure`, `Api`, SDKs externos |
| `BuildCv.Infrastructure` | `Application` + SDKs externos | `Api` |
| `BuildCv.Api` | `Application` + `Infrastructure` + ASP.NET | — |

Verifica con `dotnet list <proyecto> reference` antes de cerrar tarea.
