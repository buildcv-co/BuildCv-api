using BuildCv.Application.Features.Adapt;

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

        var stub = """
# CV Optimizado (v0 stub — no LLM)

> Esta versión es generada por un stub determinista en v0.
> En M1, será reemplazada por AnthropicAiClient con Claude Sonnet 4.
> El flujo de validación post-IA (cero invención) sigue activo.

## Resumen
Backend developer con experiencia en C# y .NET.

## Experiencia
- Trabajé en RealCorp como developer con C# y .NET.

## Skills
- C#, .NET
""";

        return Task.FromResult(stub);
    }
}
