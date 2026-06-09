using BuildCv.Application.Features.Import;

namespace BuildCv.Api.Contracts;

public sealed record ImportSectionDto(string Heading, int Start, int End, string Confidence);

public sealed record ImportWarningDto(string Code, string Message, string Severity);

public sealed record ImportResponseDto(
    string Text,
    IReadOnlyList<ImportSectionDto> Sections,
    IReadOnlyList<ImportWarningDto> Warnings,
    string EngineVersion,
    string TraceId);

public static class ImportResponseMapper
{
    public static ImportResponseDto Map(ImportResult result) => new(
        Text: result.Text,
        Sections: result.Sections
            .Select(s => new ImportSectionDto(s.Heading, s.Start, s.End, s.Confidence))
            .ToList(),
        Warnings: result.Warnings
            .Select(w => new ImportWarningDto(w.Code, w.Message, w.Severity))
            .ToList(),
        EngineVersion: result.EngineVersion,
        TraceId: result.TraceId);
}
