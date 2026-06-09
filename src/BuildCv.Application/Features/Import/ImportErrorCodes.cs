namespace BuildCv.Application.Features.Import;

/// <summary>
/// Catálogo cerrado de códigos de error y avisos de import. Vive en Application
/// para que el endpoint (Api) los use al mapear a ProblemDetails (RFC 9457).
/// Los códigos siguen el formato <c>IMPORT_*</c> (Constitution Art. IV honestidad).
/// </summary>
public static class ImportErrorCodes
{
    public const string Validation = "IMPORT_VALIDATION";
    public const string TooLarge = "IMPORT_TOO_LARGE";
    public const string UnsupportedMedia = "IMPORT_UNSUPPORTED_MEDIA";

    public const string PdfEncrypted = "IMPORT_PDF_ENCRYPTED";
    public const string ScannedPdf = "IMPORT_SCANNED_PDF";
    public const string DocxProtected = "IMPORT_DOCX_PROTECTED";
    public const string DocxNoText = "IMPORT_DOCX_NO_TEXT";
    public const string TooManyPages = "IMPORT_TOO_MANY_PAGES";
    public const string EmptyFile = "IMPORT_EMPTY_FILE";
    public const string InvalidPdf = "IMPORT_INVALID_PDF";
    public const string InvalidDocx = "IMPORT_INVALID_DOCX";

    public const string EngineError = "IMPORT_ENGINE_ERROR";
}
