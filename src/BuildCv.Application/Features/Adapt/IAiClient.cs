namespace BuildCv.Application.Features.Adapt;

/// <summary>
/// Puerto de IO para el proveedor de IA. La capa Domain y Application NO saben
/// qué proveedor existe (Anthropic, Minimax, etc.). La implementación vive en
/// Infrastructure (Constitution Art. VI — Clean Arch).
///
/// <para>
/// <see cref="CompleteStructuredAsync{T}"/> es el camino canónico: el handler
/// declara un DTO tipado, el proveedor lo materializa (Anthropic tool use /
/// Minimax JSON mode) y la respuesta se valida con DataAnnotations antes de
/// salir del puerto. Constitution Art. VI: contratos tipados, no <c>string</c> opaco.
/// </para>
/// </summary>
public interface IAiClient
{
    Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct = default)
        where T : class;

    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
