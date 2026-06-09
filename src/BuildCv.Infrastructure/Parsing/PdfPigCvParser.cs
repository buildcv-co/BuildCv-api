using BuildCv.Application.Features.Import;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace BuildCv.Infrastructure.Parsing;

/// <summary>
/// Adaptador de ICvParser para PDF (Apache-2.0, UglyToad.PdfPig).
/// Constitución Art. VI: el parseo vive en Infrastructure, detrás del puerto ICvParser.
/// Art. III: el archivo se procesa en RAM, nunca se persiste.
/// Art. V: el texto extraído se entrega como DATO inerte.
/// </summary>
public sealed class PdfPigCvParser : ICvParser
{
    private const int MaxPages = 100;
    private const int MaxTextLength = 50_000;
    private const string EngineVersion = "1.0.0";

    public ImportResult Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FileBytes is null || command.FileBytes.Length == 0)
        {
            throw new ParserEngineException("EMPTY_FILE", "El archivo está vacío.");
        }

        PdfDocument document;
        try
        {
            document = PdfDocument.Open(command.FileBytes);
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new ParserEngineException(
                "PDF_ENCRYPTED",
                "Este PDF está protegido con contraseña. Quítale la contraseña y vuelve a subirlo.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ParserEngineException(
                "INVALID_PDF",
                "El archivo no es un PDF válido o está dañado.");
        }

        using (document)
        {
            var pageCount = document.NumberOfPages;
            if (pageCount > MaxPages)
            {
                throw new ParserEngineException(
                    "TOO_MANY_PAGES",
                    $"El documento tiene {pageCount} páginas (máx. {MaxPages}).");
            }

            var sb = new System.Text.StringBuilder();
            var textLengthAcrossPages = 0;

            foreach (var page in document.GetPages())
            {
                var pageText = page.Text ?? string.Empty;
                textLengthAcrossPages += pageText.Length;
                sb.AppendLine(pageText);
            }

            if (textLengthAcrossPages == 0)
            {
                throw new ParserEngineException(
                    "SCANNED_PDF",
                    "Este PDF parece un escaneo. No podemos extraer texto. Pega el contenido manualmente o usa un PDF con texto seleccionable.");
            }

            var text = sb.ToString().Trim();
            var warnings = new List<ImportWarning>();

            if (text.Length > MaxTextLength)
            {
                warnings.Add(new ImportWarning(
                    "TEXT_TRUNCATED",
                    $"Texto truncado de {text.Length} a {MaxTextLength} caracteres.",
                    "Warning"));
                text = text.Substring(0, MaxTextLength);
            }

            var sections = SectionDetector.Detect(text);
            if (sections.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    "NO_SECTIONS_DETECTED",
                    "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                    "Info"));
            }

            return new ImportResult(
                text,
                sections,
                warnings,
                EngineVersion: EngineVersion,
                TraceId: command.TraceId);
        }
    }
}
