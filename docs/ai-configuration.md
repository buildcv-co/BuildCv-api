# AI Configuration

Proveedor de IA para `AdaptCvHandler`. Constitution Art. VI: el puerto `IAiClient` vive en Application; las implementaciones (Anthropic, Minimax, Stub) en Infrastructure.

## Provider Selection

`Ai:Provider` selecciona la implementación:

| Valor | Descripción | Cuándo |
|---|---|---|
| `Stub` | Determinista, sin clave, sin IO | Default para tests y v0 (Constitution Art. IX) |
| `Anthropic` | Claude Sonnet 4 con structured output vía tool use | Producción con acceso Anthropic |
| `Minimax` | JSON mode OpenAI-compatible | Producción sin acceso Anthropic |

## API Key Placement

**Regla dura**: la API key NUNCA se commitea al repo.

### Local (Development)

Editar `src/BuildCv.Api/appsettings.Development.json`:

```json
{
  "Ai": {
    "Provider": "Anthropic",
    "ApiKey": "sk-ant-...",
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 4096
  }
}
```

Este archivo está listado en `.gitignore` (regla `appsettings.Development.json`). No se commitea.

Alternativa con `dotnet user-secrets` (también fuera del repo):

```bash
cd src/BuildCv.Api
dotnet user-secrets init
dotnet user-secrets set "Ai:Provider" "Anthropic"
dotnet user-secrets set "Ai:ApiKey" "sk-ant-..."
```

### Producción (Deploy)

Variable de entorno (Render / Docker / K8s usan el binder estándar con `__`):

```bash
export Ai__Provider="Anthropic"
export Ai__ApiKey="sk-ant-..."
```

O en `render.yaml` (ya configurado con `sync: false`):

```yaml
envVars:
  - key: Ai__ApiKey
    sync: false
```

**Constitution Art. III**: las claves se loguean a nivel `Debug` solo en `Development`. Nunca en producción.

## Structured Output (Pydantic-equivalent en C#)

Las llamadas a IA usan `CompleteStructuredAsync<T>` en lugar de `CompleteAsync(string)` para garantizar JSON tipado:

1. `JsonSchemaExporterHelper.Export<T>()` (.NET 9+) genera el JSON schema desde un C# record.
2. El schema se pasa al proveedor:
   - **Anthropic**: como tool input (function calling). El modelo DEBE llamar al tool → JSON garantizado.
   - **Minimax**: como `response_format.schema` (OpenAI-compatible).
3. La respuesta se deserializa y se valida con `DataAnnotations.Validator`.
4. Falla ruidosamente si la validación no pasa — no se devuelve `string` opaco.

### DTO ejemplo

```csharp
public sealed record AdaptationResponse
{
    [Required, MinLength(1)]
    public required string AdaptedText { get; init; }

    [Required, MinLength(1)]
    public required string Reasoning { get; init; }

    [Required]
    public required IReadOnlyList<string> AddedEntities { get; init; }

    [Required]
    public required IReadOnlyList<string> RemovedEntities { get; init; }
}
```

### Uso en el handler

```csharp
var response = await _aiClient.CompleteStructuredAsync<AdaptationResponse>(prompt, ct);
var adaptedText = response.AdaptedText;  // null-safe, MinLength(1) garantizada
```

## Provider Notes

### Anthropic

- NuGet: `Anthropic.SDK` 5.5.0 (terceros, único .NET maduro).
- Modelos disponibles via `Anthropic.SDK.Constants.AnthropicModels`:
  - `Claude4Sonnet` = `claude-sonnet-4-20250514` (default)
  - `Claude4Opus`, `Claude37Sonnet`, `Claude35Sonnet`, `Claude35Haiku`, `Claude3Opus`, `Claude3Haiku`
- Tool use forzado via `ToolChoice { Type = ToolChoiceType.Tool, Name = "..." }`.

### Minimax

- HTTP a `https://api.MiniMax.chat/v1/chat/completions`.
- Auth: `Authorization: Bearer {ApiKey}`.
- Model: `MiniMax-Text-01` (default), configurable via `Ai:Model`.
- `BaseUrl` configurable para gateways on-prem.

## Adding a New Provider

1. Crear `src/BuildCv.Infrastructure/Ai/MiNuevoAiClient.cs` con la implementación de `IAiClient`.
2. Registrar en `DependencyInjection.RegisterAiClient(...)`.
3. Agregar entrada en `appsettings.json` (solo defaults, sin claves).
4. Tests: extender `StubAiClientStructuredOutputTests` con el tipo soportado por el stub, o crear un fake específico.
5. Constitution compliance: la implementación NO debe loguear contenido del CV (Art. III) ni inventar entidades (Art. I).