using BuildCv.Application.Features.Adapt;

namespace BuildCv.Infrastructure.Ai;

/// <summary>
/// Implementación v0 del IAiClient. NO usa un LLM real — devuelve una versión
/// "marco" del CV original con la keyword de la vacante highlighted, sin
/// agregar contenido. Permite probar el flujo end-to-end en v0 (v0 no llama
/// al proveedor real — M1 lo habilita con clave Anthropic/Minimax).
///
/// <para>
/// <see cref="CompleteStructuredAsync{T}"/> materializa el DTO <typeparamref name="T"/>
/// a partir de stubs deterministas por nombre de tipo. <see cref="CompleteAsync"/>
/// se conserva para backwards compat con código que aún espera texto libre.
/// </para>
///
/// Constitution compliance: Art. I (no invención — solo reorganiza), Art. III
/// (sin persistencia, sin IO), Art. IX (sin ZDR claim — v0 no usa LLM).
/// </summary>
public sealed class StubAiClient : IAiClient
{
    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
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

    public Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct = default)
        where T : class
    {
        ct.ThrowIfCancellationRequested();

        object instance = typeof(T).Name switch
        {
            nameof(AdaptationResponse) => new AdaptationResponse
            {
                AdaptedText = CompleteAsync(prompt, ct).GetAwaiter().GetResult(),
                Reasoning = "STUB: sin razonamiento (v0 no usa LLM real)",
                AddedEntities = Array.Empty<string>(),
                RemovedEntities = Array.Empty<string>()
            },
            _ => throw new NotSupportedException(
                $"StubAiClient no tiene un stub estructurado para {typeof(T).Name}. " +
                "Agregar entrada en CompleteStructuredAsync o usar otro IAiClient.")
        };

        return Task.FromResult((T)instance);
    }
}
