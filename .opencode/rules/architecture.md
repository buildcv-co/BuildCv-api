# Regla · Arquitectura

> Esta regla opera **bajo** la Constitución (Art. VI — "el backend demuestra .NET profesional"). Cita el artículo cuando la justifiques.

## Clean Architecture — 4 capas, una dirección

```
Domain  ←  Application  ←  Infrastructure
                       ↑
                      Api  (compone)
```

- **Domain**: PURO. Sin `Microsoft.AspNetCore.*`, sin `Microsoft.Extensions.*` (salvo `DependencyInjection.Abstractions` cuando se compongan servicios), sin SDKs externos, sin IO, sin reloj, sin red, sin aleatoriedad (Art. VI).
- **Application**: casos de uso + puertos de IO (`IAiClient`, `ICvParser`, `IPdfExporter`, `IPaymentProvider`). Compone los servicios de dominio como Singletons inmutables (ver `Application/DependencyInjection.cs`).
- **Infrastructure**: implementaciones de los puertos. Aquí viven los SDKs externos, `HttpClient`, filesystem, YAML embebido, drivers.
- **Api**: Minimal APIs, versionado `/api/v1`, OpenAPI, ProblemDetails, rate limiting, health checks. Composición final en `Program.cs`.

## Reglas de dependencia (verificables)

```bash
# Debe devolver 0 paquetes externos en Domain
dotnet list src/BuildCv.Domain package references

# Domain NO debe referenciar Application ni Infrastructure ni Api
dotnet list src/BuildCv.Domain reference
```

Si cualquiera de las dos devuelve algo distinto a "solo Microsoft.NETCore.App", **detente y corrige**.

## Puertos de IO (Art. VI, FR-030)

| Puerto (interfaz) | Vive en | Implementación en |
|---|---|---|
| `ISkillGazetteer` | `Domain/Lexicon/` | `Infrastructure/Lexicon/` (YAML embebido) |
| `IAiClient` | `Application/` | `Infrastructure/` (Anthropic / OpenRouter) |
| `ICvParser` | `Application/` | `Infrastructure/` (PdfPig / DocX) |
| `IPdfExporter` | `Application/` | `Infrastructure/` (QuestPDF) |
| `IPaymentProvider` | `Application/` | `Infrastructure/` (Wompi, en v1) |

**Regla:** ningún tipo de un SDK externo sale de `Infrastructure`. Si en `Application` necesitas un `HttpClient`, defines un puerto; la implementación inyecta el `HttpClient`.

## "No sobre-ingeniería" (Art. VI)

Un patrón se introduce **solo cuando paga su costo**:

| Tamaño | Patrón |
|---|---|
| 1 archivo, 1 clase | directo |
| 1 feature, 1 endpoint | handler + validator + mapper |
| Múltiples features | carpeta por feature bajo `Application/Features/` |
| Estado compartido | `Result<T>` o BackgroundServices (cuando llegue) |
| Múltiples fuentes de un mismo concepto | puerto + adaptador |

**Anti-patrones prohibidos:**
- Repositorios genéricos antes de tener 2+ agregados.
- CQRS/MediatR si no hay más de 5 comandos (v0 tiene 1).
- UnitOfWork antes de tener persistencia (v0 no persiste nada, Art. III).
- Inyección de `IServiceProvider` desde el dominio.

## Anatomía de un caso de uso

```csharp
public sealed record ScoreCvCommand(string CvText, string JobText) : IRequest<Result<ScoreResponse>>;

public sealed class ScoreCvHandler(IScoringEngine engine, IValidator<ScoreCvCommand> validator)
{
    public async Task<Result<ScoreResponse>> Handle(ScoreCvCommand cmd, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(cmd, ct);
        // orquestar: analizar, hacer match, calcular, mapear
        return result;
    }
}
```

- **Comando** = record inmutable con `IRequest` (si usas MediatR; si no, registro directo en `DependencyInjection.cs`).
- **Handler** = clase sellada con dependencias explícitas vía primary constructor.
- **Validator** = `AbstractValidator<T>` de FluentValidation.
- **Endpoint** = minimal API delgado: parsea, llama, mapea al DTO de respuesta.

## Endpoints (Api)

- **Minimal APIs**, no MVC controllers (ver `Api/Endpoints/ScoringEndpoints.cs`).
- `MapGroup("/api/v{version:apiVersion}")` para agrupación versionada.
- `WithName("Score")` + `Produces<ScoreResponse>` + `ProducesValidationProblem()` para OpenAPI rico.
- `RequireRateLimiting("ai")` o `"deterministic"` según costo (Art. VII).
- Errores uniformes: `Results.Problem(...)` con `ProblemDetails` (RFC 9457) o el `ValidationFilter` para 400.

## Tests por capa

- **Domain.Tests** — puros, sin fixtures, sin mocks, sin IO. Aquí vive TDD del motor (Art. VIII).
- **Application.Tests** — handler + validator; el resto con dobles cuando se introduzcan puertos con IO.
- **Api.IntegrationTests** — `WebApplicationFactory<Program>` + `HttpClient`; verifican cableado, OpenAPI, ProblemDetails, rate-limit.

> **Antes de cerrar tarea:** `dotnet build -c Release` → 0 warnings · `dotnet test` → 100% verde · `dotnet format --verify-no-changes` → limpio.
