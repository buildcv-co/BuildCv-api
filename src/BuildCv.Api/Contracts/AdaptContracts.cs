using System.ComponentModel.DataAnnotations;
using BuildCv.Domain.Adapt;

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

public static class AdaptResponseMapper
{
    public static AdaptResponseDto Map(AdaptationResult result) => new(
        AdaptedCv: result.AdaptedCv,
        Validation: new ValidationReportDto(
            IsValid: result.Validation.IsValid,
            Severity: result.Validation.Severity.ToString(),
            Inventions: result.Validation.Inventions
                .Select(i => new EntityInventionDto(
                    Type: i.Type.ToString(),
                    Claimed: i.Claimed,
                    Original: i.Original,
                    Severity: i.InventionSeverity.ToString(),
                    Position: i.Position))
                .ToList(),
            Warnings: result.Validation.Warnings),
        EngineVersion: result.EngineVersion,
        AiModel: result.AiModel);
}
