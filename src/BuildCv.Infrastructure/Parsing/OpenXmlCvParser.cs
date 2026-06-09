using BuildCv.Application.Features.Import;
using DocumentFormat.OpenXml.Packaging;

namespace BuildCv.Infrastructure.Parsing;

/// <summary>
/// Adaptador de ICvParser para DOCX (MIT, Microsoft Open XML SDK).
/// Constitución Art. VI: el parseo vive en Infrastructure, detrás del puerto ICvParser.
/// Art. III: el archivo se procesa en RAM, nunca se persiste.
/// </summary>
public sealed class OpenXmlCvParser : ICvParser
{
    private const int MaxTextLength = 50_000;
    private const string EngineVersion = "1.0.0";

    public ImportResult Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FileBytes is null || command.FileBytes.Length == 0)
        {
            throw new ParserEngineException("EMPTY_FILE", "El archivo está vacío.");
        }

        if (!LooksLikeDocx(command.FileBytes))
        {
            throw new ParserEngineException(
                "INVALID_DOCX",
                "El archivo no es un DOCX válido (faltan bytes mágicos de ZIP).");
        }

        using var ms = new MemoryStream(command.FileBytes);
        WordprocessingDocument doc;
        try
        {
            doc = WordprocessingDocument.Open(ms, isEditable: false);
        }
        catch (OpenXmlPackageException ex) when (IsDocumentProtectionMessage(ex))
        {
            throw new ParserEngineException(
                "DOCX_PROTECTED",
                "Este archivo de Word está protegido. Quítale la contraseña y vuelve a subirlo.");
        }
        catch (OpenXmlPackageException)
        {
            throw new ParserEngineException(
                "INVALID_DOCX",
                "El archivo no es un DOCX válido o está dañado.");
        }

        using (doc)
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                throw new ParserEngineException(
                    "DOCX_NO_TEXT",
                    "Este archivo de Word no contiene texto extraíble.");
            }

            var sb = new System.Text.StringBuilder();
            var warnings = new List<ImportWarning>();

            foreach (var element in body.Elements())
            {
                AppendElementText(element, sb);
            }

            var imageCount = doc.MainDocumentPart?.ImageParts?.Count() ?? 0;
            if (imageCount > 0)
            {
                warnings.Add(new ImportWarning(
                    "IMAGE_OMITTED",
                    $"Se omitieron {imageCount} imagen(es).",
                    "Info"));
            }

            var text = sb.ToString().Trim();
            if (text.Length == 0)
            {
                throw new ParserEngineException(
                    "DOCX_NO_TEXT",
                    "Este archivo de Word no contiene texto extraíble.");
            }

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

    private static void AppendElementText(DocumentFormat.OpenXml.OpenXmlElement element, System.Text.StringBuilder sb)
    {
        switch (element)
        {
            case DocumentFormat.OpenXml.Wordprocessing.Paragraph p:
                var paragraphText = p.InnerText;
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    sb.AppendLine(paragraphText);
                }
                break;
            case DocumentFormat.OpenXml.Wordprocessing.Table table:
                foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    var cells = row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                        .Select(c => c.InnerText);
                    sb.AppendLine(string.Join('\t', cells));
                }
                break;
            case DocumentFormat.OpenXml.Wordprocessing.SdtBlock sdt:
                var sdtText = sdt.InnerText;
                if (!string.IsNullOrWhiteSpace(sdtText))
                {
                    sb.AppendLine(sdtText);
                }
                break;
        }
    }

    private static bool LooksLikeDocx(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return false;
        }

        return bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;
    }

    private static bool IsDocumentProtectionMessage(OpenXmlPackageException ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("protection", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("password", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Encrypted", StringComparison.OrdinalIgnoreCase);
    }
}
