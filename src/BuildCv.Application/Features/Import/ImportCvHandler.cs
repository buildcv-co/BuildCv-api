using BuildCv.Domain.Common;
using BuildCv.Domain.Resumes;
using FluentValidation;

namespace BuildCv.Application.Features.Import;

/// <summary>
/// Orquesta el flujo de import:
/// 1. Valida el comando con FluentValidation (nombre no vacío, mime pdf/docx).
/// 2. Llama al <see cref="IParserRouter"/> (micro-batch 2d de 021) que despacha por
///    <c>command.EngineVersion</c>: <c>1.0.0</c> → legacy (<see cref="RawParseResult"/>),
///    <c>2.0.0</c> → estructurado (<see cref="StructuredParseResult"/>).
/// 3. Mapea cada variante del <see cref="ParseResult"/> discriminated union al contrato
///    apropiado: <see cref="LegacyImportResult"/> para v1, <see cref="StructuredImportResult"/>
///    para v2. El discriminador <see cref="ImportResult.EngineVersion"/> queda sellado
///    en cada variante (Constitution Art. II — SemVer estable).
/// 4. Mapea excepciones tipadas a errores de dominio estables.
/// 5. Devuelve <c>Result&lt;ImportResult&gt;</c>.
///
/// Constitution: Art. III (sin logs de contenido), Art. V (la salida es DATO inerte),
/// Art. VI (parseo tras el puerto ICvParser / IStructuredParser), Art. VIII (tests rojos primero).
/// </summary>
public sealed class ImportCvHandler
{
    private readonly IParserRouter _router;
    private readonly IValidator<ImportCvCommand> _validator;

    public ImportCvHandler(IParserRouter router, IValidator<ImportCvCommand> validator)
    {
        _router = router;
        _validator = validator;
    }

    public Task<Result<ImportResult>> HandleAsync(ImportCvCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            return Task.FromResult(Result.Failure<ImportResult>(new Error(
                ImportErrorCodes.Validation,
                "Comando de import inválido.")));
        }

        try
        {
            var parseResult = _router.Parse(command);
            var result = MapToImportResult(parseResult, command.TraceId);
            return Task.FromResult(Result.Success(result));
        }
        catch (ParserEngineException ex)
        {
            return Task.FromResult(Result.Failure<ImportResult>(new Error(MapCode(ex.Code), ex.Message)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result.Failure<ImportResult>(new Error(
                ImportErrorCodes.EngineError,
                "El motor de import falló procesando el archivo.")));
        }
    }

    /// <summary>
    /// Mapea cada variante del <see cref="ParseResult"/> discriminated union al
    /// <see cref="ImportResult"/> apropiado. Esta función es pura (sin IO, sin reloj,
    /// sin aleatoriedad) — Constitution Art. II.
    /// </summary>
    private static ImportResult MapToImportResult(ParseResult parseResult, string traceId)
    {
        return parseResult switch
        {
            RawParseResult raw => MapLegacy(raw, traceId),
            StructuredParseResult structured => MapStructured(structured, traceId),
            _ => throw new InvalidOperationException(
                $"Variante de ParseResult desconocida: {parseResult.GetType().FullName}."),
        };
    }

    private static LegacyImportResult MapLegacy(RawParseResult raw, string traceId)
    {
        var sections = SectionDetector.Detect(raw.Text);
        var warningsList = new List<ImportWarning>(ConvertWarnings(raw.Warnings));
        if (sections.Count == 0 && warningsList.All(w => w.Code != "NO_SECTIONS_DETECTED"))
        {
            warningsList.Add(new ImportWarning(
                "NO_SECTIONS_DETECTED",
                "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                "Info"));
        }

        return new LegacyImportResult(
            text: raw.Text,
            sections: sections,
            warnings: warningsList,
            traceId: traceId);
    }

    private static StructuredImportResult MapStructured(StructuredParseResult structured, string traceId)
    {
        return new StructuredImportResult(
            cv: structured.Cv,
            warnings: structured.Warnings,
            traceId: traceId);
    }

    private static IReadOnlyList<ImportWarning> ConvertWarnings(IReadOnlyList<ParsingWarning> source)
    {
        var list = new List<ImportWarning>(source.Count);
        foreach (var w in source)
        {
            list.Add(new ImportWarning(w.Code, w.Message, w.Severity));
        }

        return list;
    }

    private static string MapCode(string parserCode) => parserCode switch
    {
        "PDF_ENCRYPTED" => ImportErrorCodes.PdfEncrypted,
        "SCANNED_PDF" => ImportErrorCodes.ScannedPdf,
        "DOCX_PROTECTED" => ImportErrorCodes.DocxProtected,
        "DOCX_NO_TEXT" => ImportErrorCodes.DocxNoText,
        "TOO_MANY_PAGES" => ImportErrorCodes.TooManyPages,
        "EMPTY_FILE" => ImportErrorCodes.EmptyFile,
        "INVALID_PDF" => ImportErrorCodes.InvalidPdf,
        "INVALID_DOCX" => ImportErrorCodes.InvalidDocx,
        "UNSUPPORTED_MIME" => ImportErrorCodes.UnsupportedMedia,
        _ => ImportErrorCodes.EngineError,
    };
}
