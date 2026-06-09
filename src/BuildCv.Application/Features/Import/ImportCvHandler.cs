using BuildCv.Domain.Common;
using FluentValidation;

namespace BuildCv.Application.Features.Import;

/// <summary>
/// Orquesta el flujo de import:
/// 1. Valida el comando con FluentValidation (nombre no vacío, mime pdf/docx).
/// 2. Llama al parser (puerto ICvParser).
/// 3. Mapea excepciones tipadas a errores de dominio estables.
/// 4. Devuelve Result&lt;ImportResult&gt;.
///
/// Constitution: Art. III (sin logs de contenido), Art. V (la salida es DATO inerte),
/// Art. VI (parseo tras el puerto ICvParser), Art. VIII (tests rojos primero).
/// </summary>
public sealed class ImportCvHandler
{
    private readonly ICvParser _parser;
    private readonly IValidator<ImportCvCommand> _validator;

    public ImportCvHandler(ICvParser parser, IValidator<ImportCvCommand> validator)
    {
        _parser = parser;
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
            var result = _parser.Parse(command);
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
