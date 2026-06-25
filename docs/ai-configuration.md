# AI Configuration — Secure Setup

> Proveedor de IA para `AdaptCvHandler`. Constitution Art. VI: el puerto `IAiClient` vive en Application; las implementaciones (Anthropic, Minimax, Stub) en Infrastructure.

## Supported Providers

| Provider | Config value | API Docs | Notes |
|----------|--------------|----------|-------|
| **Stub** (default) | `"Stub"` | — | No API key, deterministic responses. Default v0 (Constitution Art. IX). |
| **Anthropic** (Claude Sonnet 4) | `"Anthropic"` | https://docs.anthropic.com/ | Tool-use structured output. Model: `claude-sonnet-4-20250514`. |
| **Minimax** (Minimax Text-01) | `"Minimax"` | https://platform.minimax.io/docs/api-reference/api-overview | JSON-mode structured output (OpenAI-compatible). Model: `MiniMax-Text-01`. |

Default in `appsettings.json` (committed) is `Stub`. Switch to `Anthropic` or `Minimax` locally via the methods below.

## Security: NEVER commit API keys

The file `BuildCv-api/src/BuildCv.Api/appsettings.Development.json` is **.gitignored** (explicit rule at `BuildCv-api/.gitignore:15`). Real keys go there or via the secure methods below.

The committed template `appsettings.Development.json.example` shows the structure but contains empty/default values — no secrets.

**Verify the ignore rule is working**:

```bash
cd BuildCv-api
git check-ignore -v src/BuildCv.Api/appsettings.Development.json
# Expected: .gitignore:15:appsettings.Development.json	src/BuildCv.Api/appsettings.Development.json
```

**Verify no API keys are in git history**:

```bash
cd BuildCv-api
git log --all -p -- src/BuildCv.Api/appsettings.Development.json
# The file was removed from the index in commit (this hardening). Earlier commits contain
# only empty `ApiKey: ""` — verify with the command above before pushing.
```

If you accidentally committed a real key, use `git filter-repo` to scrub history and **rotate the key immediately** at the provider.

## 3 Secure Methods to Set Your API Key

Pick ONE. All three work with the same `Ai:Provider` / `Ai:ApiKey` / `Ai:Model` keys.

### Method 1: Edit `appsettings.Development.json` (simplest, local dev only)

```bash
# Copy the committed template
cp BuildCv-api/src/BuildCv.Api/appsettings.Development.json.example \
   BuildCv-api/src/BuildCv.Api/appsettings.Development.json

# Edit and paste your real key
$EDITOR BuildCv-api/src/BuildCv.Api/appsettings.Development.json
```

The file is `.gitignored`, so your key stays local. Do **not** commit it.

### Method 2: `dotnet user-secrets` (recommended, stays out of repo entirely)

```bash
cd BuildCv-api/src/BuildCv.Api

# Initialize (one-time)
dotnet user-secrets init

# Anthropic (Claude Sonnet 4)
dotnet user-secrets set "Ai:Provider" "Anthropic"
dotnet user-secrets set "Ai:ApiKey" "sk-ant-..."
dotnet user-secrets set "Ai:Model" "claude-sonnet-4-20250514"

# OR Minimax
dotnet user-secrets set "Ai:Provider" "Minimax"
dotnet user-secrets set "Ai:ApiKey" "eyJhbGc..."
dotnet user-secrets set "Ai:BaseUrl" "https://api.MiniMax.chat"
dotnet user-secrets set "Ai:Model" "MiniMax-Text-01"
```

Secrets are stored at `~/.microsoft/usersecrets/<project-id>/secrets.json` (encrypted on macOS/Linux, never leaves your machine).

### Method 3: Environment variables (for production / Render / Docker / K8s)

```bash
# Anthropic
export Ai__Provider=Anthropic
export Ai__ApiKey=sk-ant-...
export Ai__Model=claude-sonnet-4-20250514

# OR Minimax
export Ai__Provider=Minimax
export Ai__ApiKey=eyJhbGc...
export Ai__BaseUrl=https://api.MiniMax.chat
export Ai__Model=MiniMax-Text-01
```

ASP.NET Core uses `__` (double underscore) to express nested keys in env vars. Render's `render.yaml` already binds `Ai__ApiKey` with `sync: false`.

## Switch Providers

Change `Ai:Provider` to switch:

- `"Stub"` → deterministic, no key needed (default)
- `"Anthropic"` → uses `Ai.ApiKey` for Claude
- `"Minimax"` → uses `Ai.ApiKey` + `Ai.BaseUrl` for Minimax (defaults to `https://api.MiniMax.chat` if `BaseUrl` is empty)

The DI in `BuildCv.Infrastructure/DependencyInjection.RegisterAiClient` selects the implementation based on `Ai:Provider` and throws clearly for unknown values.

## Structured Output (Pydantic-equivalent en C#)

All AI calls use `CompleteStructuredAsync<T>` instead of `CompleteAsync(string)` to guarantee JSON typed responses:

1. `JsonSchemaExporterHelper.Export<T>()` (.NET 9+) generates JSON schema from a C# record at runtime.
2. The schema is passed to the provider:
   - **Anthropic**: as a tool input (function calling). The model MUST call the tool → JSON guaranteed.
   - **Minimax**: as `response_format.schema` (OpenAI-compatible JSON mode) → JSON guaranteed.
3. The response is deserialized and validated with `DataAnnotations.Validator`.
4. Validation failures fail noisily — no opaque `string` escapes the port.

### Example DTO

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

### Example usage

```csharp
public sealed class AdaptCvHandler(IAiClient ai)
{
    public async Task<AdaptedCv> HandleAsync(string cv, string job, CancellationToken ct)
    {
        var prompt = PromptBuilder.Build(cv, job);
        var response = await ai.CompleteStructuredAsync<AdaptationResponse>(prompt, ct);
        // response.AdaptedText is guaranteed non-null, non-empty by DataAnnotations
        return new AdaptedCv(response.AdaptedText, response.Reasoning);
    }
}
```

## Provider Notes

### Anthropic (Claude Sonnet 4)

- NuGet: `Anthropic.SDK` 5.5.0 (terceros — Anthropic no tiene .NET oficial todavía).
- Modelos via `Anthropic.SDK.Constants.AnthropicModels`:
  - `Claude4Sonnet` = `claude-sonnet-4-20250514` (default)
  - `Claude4Opus`, `Claude37Sonnet`, `Claude35Sonnet`, `Claude35Haiku`, `Claude3Opus`, `Claude3Haiku`
- Tool use forzado via `ToolChoice { Type = ToolChoiceType.Tool, Name = "..." }`.
- Pricing: ~$3/M input tokens, ~$15/M output tokens (consultar docs Anthropic para tarifa actualizada).

### Minimax (Minimax Text-01)

- HTTP API a `{Ai:BaseUrl}/v1/chat/completions` (default `https://api.MiniMax.chat`, OpenAI-compatible).
- Auth: `Authorization: Bearer {Ai:ApiKey}`.
- Structured output: `response_format: { type: "json_schema", schema: ... }`.
- Model default: `MiniMax-Text-01`.
- Docs: https://platform.minimax.io/docs/api-reference/api-overview

## Troubleshooting

### Key not found at runtime
```
System.InvalidOperationException: Ai:ApiKey is required when Ai:Provider is 'Anthropic'
```
→ Set the key via user-secrets or env var (Method 2 or 3).

### Provider not recognized
```
Ai:Provider desconocido: 'openai'. Valores válidos: Stub, Anthropic, Minimax.
```
→ Use one of `Stub`, `Anthropic`, or `Minimax` (case-insensitive).

### `BaseAddress` crashes with `BaseUrl: ""`
```
System.UriFormatException: Invalid URI: The format of the URI could not be determined.
```
→ This is now handled defensively in `DependencyInjection.RegisterAiClient`: empty BaseUrl falls back to `https://api.MiniMax.chat`. To use a custom gateway, set `Ai:BaseUrl` explicitly.

### Structured output validation fails
```
InvalidOperationException: AI response failed validation: The AdaptedText field is required
```
→ The model returned invalid JSON. Retry with a different model, lower temperature, or a sharper prompt. Constitution Art. VI: never return an opaque `string` from the port.

## Security Checklist

- [ ] `BuildCv-api/src/BuildCv.Api/appsettings.Development.json` is gitignored (`git check-ignore -v` returns the rule)
- [ ] `BuildCv-api/src/BuildCv.Api/appsettings.Development.json.example` is the committed template (no real keys)
- [ ] Real API keys are NEVER committed to git (search history with `git log --all -p -- src/BuildCv.Api/appsettings.Development.json`)
- [ ] `.env` files are gitignored (verify with `git check-ignore -v .env`)
- [ ] Production uses env vars or a secrets manager — not files
- [ ] `appsettings.json` (committed) has `Provider: "Stub"` and `ApiKey: ""` (Constitution Art. IX: v0 default)
- [ ] Local override uses one of the 3 secure methods above

## Adding a New Provider

1. Crear `src/BuildCv.Infrastructure/Ai/MiNuevoAiClient.cs` con la implementación de `IAiClient`.
2. Registrar en `DependencyInjection.RegisterAiClient(...)`.
3. Agregar entrada en `appsettings.json` (solo defaults, sin claves).
4. Tests: extender `StubAiClientStructuredOutputTests` con el tipo soportado por el stub, o crear un fake específico.
5. Constitution compliance: la implementación NO debe loguear contenido del CV (Art. III) ni inventar entidades (Art. I).
6. Actualizar este documento y `appsettings.Development.json.example`.
