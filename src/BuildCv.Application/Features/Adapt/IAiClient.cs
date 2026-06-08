namespace BuildCv.Application.Features.Adapt;

/// <summary>
/// Puerto de IO para el proveedor de IA. La capa Domain y Application NO saben
/// qué proveedor existe (Anthropic, OpenAI, etc.). La implementación vive en
/// Infrastructure (Constitution Art. VI — Clean Arch).
/// </summary>
public interface IAiClient
{
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
}
