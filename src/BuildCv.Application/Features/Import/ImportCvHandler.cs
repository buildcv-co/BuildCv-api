using BuildCv.Domain.Common;
using FluentValidation;

namespace BuildCv.Application.Features.Import;

/// <summary>
/// Orquesta el flujo de import:
/// 1. Valida el comando con FluentValidation (nombre no vacío, mime pdf/docx).
/// 2. Llama al <see cref="IParserRouter"/> (micro-batch 2d de 021) que despacha por
///    <c>command.EngineVersion</c>: <c>1.0.0</c> → legacy (RawParseResult), <c>2.0.0</c> →
///    estructurado (StructuredParseResult).
/// 3. Adapta el <see cref="ParseResult"/> discriminated union al contrato legacy
///    <see cref="ImportResult"/> mientras el endpoint se migra a <c>engineVersion 2.0.0</c>
///    en el micro-batch 2e de 021. La adaptación es pura (sin IO/reloj/aleatoriedad) —
///    Constitution Art. II (determinista y explicable).
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
            var legacyResult = AdaptToLegacy(parseResult, command);
            return Task.FromResult(Result.Success(legacyResult));
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
    /// Adapta el <see cref="ParseResult"/> v2 al contrato legacy <see cref="ImportResult"/>
    /// mientras el endpoint se migra (micro-batch 2e). Para <see cref="StructuredParseResult"/>
    /// se serializa el <c>CvDocument</c> como texto plano vía un renderizador
    /// determinista (preservando Constitution Art. I: cero invención — sólo lo que está
    /// en el CV original). Para <see cref="RawParseResult"/> se mapea 1:1.
    ///
    /// Las <see cref="ImportSection"/> se recomputan con <see cref="SectionDetector"/>
    /// sobre el texto extraído — preservando el comportamiento del router legacy
    /// (micro-batch 2a de 021) que también las calculaba como heurística.
    ///
    /// TODO(021/2e): retirar este shim cuando el endpoint consuma directamente
    /// <see cref="ParseResult"/> (PR 2e de 021).
    /// </summary>
    private static ImportResult AdaptToLegacy(ParseResult result, ImportCvCommand command)
    {
        var (text, sections, warnings, engineVersion) = result switch
        {
            RawParseResult raw => (
                raw.Text,
                SectionDetector.Detect(raw.Text),
                raw.Warnings,
                raw.EngineVersion),

            StructuredParseResult structured => (
                RenderStructuredAsText(structured.Cv),
                Array.Empty<ImportSection>(),
                structured.Warnings,
                structured.EngineVersion),

            _ => throw new InvalidOperationException(
                $"Variante de ParseResult desconocida: {result.GetType().FullName}."),
        };

        var warningsList = new List<ImportWarning>(ConvertWarnings(warnings));
        if (sections.Count == 0 && engineVersion == "1.0.0" && warningsList.All(w => w.Code != "NO_SECTIONS_DETECTED"))
        {
            warningsList.Add(new ImportWarning(
                "NO_SECTIONS_DETECTED",
                "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                "Info"));
        }

        return new ImportResult(
            Text: text,
            Sections: sections,
            Warnings: warningsList,
            EngineVersion: engineVersion,
            TraceId: command.TraceId);
    }

    private static string RenderStructuredAsText(BuildCv.Domain.Resumes.CvDocument cv)
    {
        var sb = new System.Text.StringBuilder();
        var basics = cv.Basics;

        if (!string.IsNullOrWhiteSpace(basics.Name))
        {
            sb.AppendLine(basics.Name);
        }

        var contact = string.Join(" | ",
            new[] { basics.Email, basics.Phone, basics.Url }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (contact.Length > 0)
        {
            sb.AppendLine(contact);
        }

        foreach (var profile in basics.Profiles)
        {
            sb.AppendLine($"{profile.Network}: {profile.Url}");
        }

        if (cv.Work.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("EXPERIENCIA");
            foreach (var work in cv.Work)
            {
                sb.AppendLine($"{work.Entry.Name} — {work.Entry.Position} ({work.Entry.StartDate} – {work.Entry.EndDate ?? "actualidad"})");
                if (!string.IsNullOrWhiteSpace(work.Entry.Summary))
                {
                    sb.AppendLine(work.Entry.Summary);
                }

                if (work.Entry.Highlights is not null)
                {
                    foreach (var highlight in work.Entry.Highlights)
                    {
                        sb.AppendLine($"  • {highlight}");
                    }
                }
            }
        }

        if (cv.Education.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("EDUCACIÓN");
            foreach (var edu in cv.Education)
            {
                sb.AppendLine($"{edu.Entry.Institution} — {edu.Entry.Area ?? edu.Entry.StudyType ?? string.Empty}");
            }
        }

        if (cv.Skills.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("HABILIDADES");
            sb.AppendLine(string.Join(", ", cv.Skills.Select(s => s.Entry.Name)));
        }

        return sb.ToString().Trim();
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
