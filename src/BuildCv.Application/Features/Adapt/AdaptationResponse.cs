using System.ComponentModel.DataAnnotations;

namespace BuildCv.Application.Features.Adapt;

/// <summary>
/// Respuesta estructurada del proveedor de IA para la adaptación de un CV.
/// Constitution Art. I: el <see cref="AdaptedText"/> no debe inventar entidades;
/// <see cref="AddedEntities"/> y <see cref="RemovedEntities"/> son metadata honesta
/// que la IA declara explícitamente para auditoría (se cruza con CrossEntityValidator).
/// Constitution Art. VI: contrato tipado en Application; ningún <c>string</c> opaco.
/// </summary>
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
