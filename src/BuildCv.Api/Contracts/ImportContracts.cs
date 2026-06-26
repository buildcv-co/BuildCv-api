using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;

namespace BuildCv.Api.Contracts;

// ─────────────────────────────────────────────────────────────────────
// Legacy shape — engineVersion "1.0.0". Compat con clientes existentes.
// ─────────────────────────────────────────────────────────────────────

public sealed record ImportSectionDto(string Heading, int Start, int End, string Confidence);

public sealed record ImportWarningDto(string Code, string Message, string Severity);

public sealed record ImportResponseDto(
    string Text,
    IReadOnlyList<ImportSectionDto> Sections,
    IReadOnlyList<ImportWarningDto> Warnings,
    string EngineVersion,
    string TraceId);

// ─────────────────────────────────────────────────────────────────────
// Structured shape — engineVersion "2.0.0". CvDocument JSON Resume tipado.
// ─────────────────────────────────────────────────────────────────────

public sealed record ImportResponseV2Dto(
    CvDocument Cv,
    IReadOnlyList<ImportWarningDto> Warnings,
    string EngineVersion,
    string TraceId);

public static class ImportResponseMapper
{
    public const string LegacyEngineVersion = "1.0.0";
    public const string StructuredEngineVersion = "2.0.0";

    /// <summary>
    /// Mapea el <see cref="ImportResult"/> discriminated union al contrato HTTP correcto
    /// según la variante. Sin serialización común (la respuesta JSON la produce el endpoint
    /// directamente con <c>Results.Ok(...)</c> usando los converters de System.Text.Json).
    /// </summary>
    public static object Map(ImportResult result) => result switch
    {
        LegacyImportResult legacy => MapLegacy(legacy),
        StructuredImportResult structured => MapStructured(structured),
        _ => throw new InvalidOperationException(
            $"Variante de ImportResult desconocida: {result.GetType().FullName}."),
    };

    private static ImportResponseDto MapLegacy(LegacyImportResult result) => new(
        Text: result.Text,
        Sections: result.Sections
            .Select(s => new ImportSectionDto(s.Heading, s.Start, s.End, s.Confidence))
            .ToList(),
        Warnings: result.Warnings
            .Select(w => new ImportWarningDto(w.Code, w.Message, w.Severity))
            .ToList(),
        EngineVersion: result.EngineVersion,
        TraceId: result.TraceId);

    private static ImportResponseV2Dto MapStructured(StructuredImportResult result) => new(
        Cv: result.Cv,
        Warnings: result.Warnings
            .Select(w => new ImportWarningDto(w.Code, w.Message, w.Severity))
            .ToList(),
        EngineVersion: result.EngineVersion,
        TraceId: result.TraceId);
}
