using System.ComponentModel.DataAnnotations;
using BuildCv.Application.Features.Export;
using BuildCv.Domain.Adapt;

namespace BuildCv.Api.Contracts;

public sealed record ExportRequestDto(
    [Required, MaxLength(50_000)] string AdaptedCv,
    [Required] ValidationReportDto Validation,
    [MaxLength(100)] string CandidateName);

public static class ExportResponseMapper
{
    public static ExportPdfCommand ToCommand(ExportRequestDto dto) => new(
        AdaptedCv: dto.AdaptedCv,
        Validation: new ValidationReport(
            IsValid: dto.Validation.IsValid,
            Severity: Enum.Parse<Severity>(dto.Validation.Severity, ignoreCase: true),
            Inventions: dto.Validation.Inventions
                .Select(i => new EntityInvention(
                    Type: Enum.Parse<InventionType>(i.Type, ignoreCase: true),
                    Claimed: i.Claimed,
                    Original: i.Original,
                    InventionSeverity: Enum.Parse<InventionSeverity>(i.Severity, ignoreCase: true),
                    Position: i.Position))
                .ToList(),
            Warnings: dto.Validation.Warnings),
        CandidateName: dto.CandidateName);
}
