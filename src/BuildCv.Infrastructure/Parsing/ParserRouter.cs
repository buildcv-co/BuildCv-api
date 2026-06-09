using BuildCv.Application.Features.Import;

namespace BuildCv.Infrastructure.Parsing;

/// <summary>
/// Compuesto que despacha al parser concreto según MIME declarado y magic bytes.
/// Es la única implementación de ICvParser que se inyecta (DI); los adaptadores
/// específicos quedan disponibles como servicios resolubles si se requieren.
/// Constitución Art. VI: el dominio solo conoce ICvParser (puerto).
/// </summary>
public sealed class ParserRouter : ICvParser
{
    private readonly PdfPigCvParser _pdfParser;
    private readonly OpenXmlCvParser _docxParser;

    public ParserRouter(PdfPigCvParser pdfParser, OpenXmlCvParser docxParser)
    {
        _pdfParser = pdfParser;
        _docxParser = docxParser;
    }

    public ImportResult Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var mime = command.MimeType?.Trim() ?? string.Empty;

        if (mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            EnsureMagicBytes(command.FileBytes, expectedPdfMagic: true);
            return _pdfParser.Parse(command);
        }

        if (mime.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase))
        {
            EnsureMagicBytes(command.FileBytes, expectedPdfMagic: false);
            return _docxParser.Parse(command);
        }

        throw new ParserEngineException(
            "UNSUPPORTED_MIME",
            $"Tipo de archivo no soportado: {mime}. Sube un PDF o DOCX.");
    }

    private static void EnsureMagicBytes(byte[] bytes, bool expectedPdfMagic)
    {
        if (bytes is null || bytes.Length < 4)
        {
            throw new ParserEngineException(
                "UNSUPPORTED_MIME",
                "Archivo demasiado pequeño para validar.");
        }

        if (expectedPdfMagic)
        {
            var isPdf = bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
            if (!isPdf)
            {
                throw new ParserEngineException(
                    "UNSUPPORTED_MIME",
                    "El archivo no tiene la firma de un PDF (%PDF-).");
            }
        }
        else
        {
            var isZip = bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;
            if (!isZip)
            {
                throw new ParserEngineException(
                    "UNSUPPORTED_MIME",
                    "El archivo no tiene la firma de un DOCX (PK\\x03\\x04).");
            }
        }
    }
}
