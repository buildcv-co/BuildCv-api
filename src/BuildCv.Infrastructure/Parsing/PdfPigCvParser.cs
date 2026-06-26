using System.Text;
using System.Text.RegularExpressions;
using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace BuildCv.Infrastructure.Parsing;

/// <summary>
/// Adaptador de ICvParser para PDF (Apache-2.0, UglyToad.PdfPig).
/// Constitución Art. VI: el parseo vive en Infrastructure, detrás del puerto.
/// Art. III: el archivo se procesa en RAM, nunca se persiste.
/// Art. V: el texto extraído se entrega como DATO inerte.
/// Art. I: confidence solo emite <c>inferred</c> o <c>explicit</c>; <c>user_confirmed</c>
/// es exclusivo del editor (PR 4 de 021).
/// Micro-batch 2b de 021: además de la ruta legacy 1.0.0, expone la ruta
/// <see cref="IStructuredParser"/> 2.0.0 que devuelve <see cref="StructuredParseResult"/>.
/// </summary>
public sealed class PdfPigCvParser : ICvParser, IStructuredParser
{
    private const int MaxPages = 100;
    private const int MaxTextLength = 50_000;
    private const int LineTolerancePts = 2;
    private const string LegacyEngineVersion = "1.0.0";
    private const string StructuredEngineVersion = "2.0.0";

    private const string WorkHeaderRegex =
        @"^\s*(?:EXPERIENCE|WORK\s+EXPERIENCE|EMPLOYMENT|EXPERIENCIA|EXPERIENCIA\s+LABORAL|TRABAJO)\s*[\.\:]*\s*$";
    private const string EducationHeaderRegex =
        @"^\s*(?:EDUCATION|ACADEMIC|EDUCACION|EDUCACIÓN|FORMACION|FORMACIÓN)\s*[\.\:]*\s*$";
    private const string SkillsHeaderRegex =
        @"^\s*(?:SKILLS|TECHNICAL\s+SKILLS|HABILIDADES|HABILIDADES\s+TECNICAS|HABILIDADES\s+TÉCNICAS|COMPETENCIAS)\s*[\.\:]*\s*$";
    private const string GenericHeaderRegex =
        @"^\s*(?:CONTACTO|PERFIL|RESUMEN|IDIOMAS|CERTIFICACIONES|REFERENCIAS|PUBLICACIONES|PROYECTOS|CONTACT|PROFILE|SUMMARY|LANGUAGES|CERTIFICATIONS|PROJECTS)\s*[\.\:]*\s*$";

    private static readonly Regex EmailPattern = new(
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"\+\d{1,3}[\s\-]?\(?\d{1,4}\)?[\s\-]?\d{2,4}(?:[\s\-]?\d{2,4}){1,2}",
        RegexOptions.Compiled);

    private static readonly Regex UrlPattern = new(
        @"\bhttps?://[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BareDomainPattern = new(
        @"\b(?:www\.)?[a-zA-Z0-9\-]+\.(?:com|net|org|io|dev|co|me|ai|app)(?:/[^\s,;]*)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SectionLineWork = new(
        WorkHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineEducation = new(
        EducationHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineSkills = new(
        SkillsHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineGeneric = new(
        GenericHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NameLinePattern = new(
        @"^([A-ZÁÉÍÓÚÑ][a-záéíóúñ']+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ']+){1,3})\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DateRangePattern = new(
        @"(\d{4})\s*[\-–—]\s*(\d{4}|present|actualidad|hoy)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ImportResult Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FileBytes is null || command.FileBytes.Length == 0)
        {
            throw new ParserEngineException("EMPTY_FILE", "El archivo está vacío.");
        }

        var (text, warnings) = ExtractFlatText(command);

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
            EngineVersion: LegacyEngineVersion,
            TraceId: command.TraceId);
    }

    ParseResult IStructuredParser.Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FileBytes is null || command.FileBytes.Length == 0)
        {
            throw new ParserEngineException("EMPTY_FILE", "El archivo está vacío.");
        }

        var (lines, legacyWarnings) = ExtractLines(command);

        var cv = BuildStructuredCv(lines);
        var warnings = new List<ParsingWarning>(ConvertLegacyWarnings(legacyWarnings));

        if (lines.Count == 0 || !HasAnyStructuredSection(lines))
        {
            warnings.Add(new ParsingWarning(
                "PDF_NO_SEMANTIC_STRUCTURE",
                "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                "Info"));
        }

        return new StructuredParseResult(cv, warnings);
    }

    private static bool HasAnyStructuredSection(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (SectionLineWork.IsMatch(line))
            {
                return true;
            }

            if (SectionLineEducation.IsMatch(line))
            {
                return true;
            }

            if (SectionLineSkills.IsMatch(line))
            {
                return true;
            }

            if (SectionLineGeneric.IsMatch(line))
            {
                return true;
            }
        }
        return false;
    }

    private (string Text, List<ImportWarning> Warnings) ExtractFlatText(ImportCvCommand command)
    {
        PdfDocument document = OpenDocument(command);

        using (document)
        {
            EnforcePageBudget(document);
            var sb = new StringBuilder();
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

            return (text, warnings);
        }
    }

    private (List<string> Lines, List<ImportWarning> Warnings) ExtractLines(ImportCvCommand command)
    {
        PdfDocument document = OpenDocument(command);

        using (document)
        {
            EnforcePageBudget(document);

            var lines = new List<string>();
            var totalChars = 0;

            foreach (var page in document.GetPages())
            {
                var pageLines = GroupWordsIntoLines(page.GetWords());
                foreach (var lineText in pageLines)
                {
                    var trimmed = lineText.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    lines.Add(trimmed);
                    totalChars += trimmed.Length + 1;
                }
            }

            if (lines.Count == 0)
            {
                throw new ParserEngineException(
                    "SCANNED_PDF",
                    "Este PDF parece un escaneo. No podemos extraer texto. Pega el contenido manualmente o usa un PDF con texto seleccionable.");
            }

            var warnings = new List<ImportWarning>();
            if (totalChars > MaxTextLength)
            {
                warnings.Add(new ImportWarning(
                    "TEXT_TRUNCATED",
                    $"Texto truncado a {MaxTextLength} caracteres.",
                    "Warning"));
                lines = TruncateLines(lines, MaxTextLength);
            }

            return (lines, warnings);
        }
    }

    private static List<string> GroupWordsIntoLines(IEnumerable<Word> words)
    {
        var ordered = words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
        var lines = new List<List<Word>>();
        List<Word>? current = null;
        double currentY = double.NaN;

        foreach (var word in ordered)
        {
            var y = word.BoundingBox.Bottom;
            if (current is null || Math.Abs(y - currentY) > LineTolerancePts)
            {
                if (current is not null)
                {
                    lines.Add(current);
                }
                current = new List<Word> { word };
                currentY = y;
            }
            else
            {
                current.Add(word);
            }
        }

        if (current is not null)
        {
            lines.Add(current);
        }

        var result = new List<string>(lines.Count);
        foreach (var lineWords in lines)
        {
            var orderedLine = lineWords.OrderBy(w => w.BoundingBox.Left).ToList();
            var sb = new StringBuilder();
            for (var i = 0; i < orderedLine.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(orderedLine[i].Text);
            }
            result.Add(sb.ToString());
        }

        return result;
    }

    private static List<string> TruncateLines(List<string> lines, int maxChars)
    {
        var result = new List<string>(lines.Count);
        var running = 0;
        foreach (var line in lines)
        {
            if (running + line.Length + 1 > maxChars)
            {
                break;
            }
            result.Add(line);
            running += line.Length + 1;
        }
        return result;
    }

    private static PdfDocument OpenDocument(ImportCvCommand command)
    {
        try
        {
            return PdfDocument.Open(command.FileBytes);
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
    }

    private static void EnforcePageBudget(PdfDocument document)
    {
        var pageCount = document.NumberOfPages;
        if (pageCount > MaxPages)
        {
            throw new ParserEngineException(
                "TOO_MANY_PAGES",
                $"El documento tiene {pageCount} páginas (máx. {MaxPages}).");
        }
    }

    private static CvDocument BuildStructuredCv(IReadOnlyList<string> lines)
    {
        var nonBlank = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var (name, email, phone, url, profiles) = ExtractBasics(nonBlank);

        var work = ExtractWork(nonBlank);
        var education = ExtractEducation(nonBlank);
        var skills = ExtractSkills(nonBlank);

        var basics = new Basics(
            Name: name,
            Email: email,
            Phone: phone,
            Location: null,
            Url: url,
            Profiles: profiles,
            Summary: null,
            DatosPersonales: null,
            Confidence: new BasicsConfidence(
                Name: ConfidenceMarker.Inferred,
                Email: IsExplicitEmail(email) ? ConfidenceMarker.Explicit : ConfidenceMarker.Inferred,
                Phone: phone is not null && PhonePattern.IsMatch(phone) ? ConfidenceMarker.Explicit : ConfidenceMarker.Inferred,
                Location: ConfidenceMarker.Inferred,
                Url: url is not null && url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? ConfidenceMarker.Explicit : ConfidenceMarker.Inferred,
                Profiles: profiles.Count > 0 ? ConfidenceMarker.Explicit : ConfidenceMarker.Inferred,
                Summary: ConfidenceMarker.Inferred,
                DatosPersonales: ConfidenceMarker.Inferred));

        return new CvDocument(
            Basics: basics,
            Work: work,
            Education: education,
            Skills: skills,
            Projects: Array.Empty<TaggedResumeProject>(),
            Certificates: Array.Empty<TaggedResumeCertificate>(),
            Languages: Array.Empty<TaggedResumeLanguage>(),
            Meta: new CvMeta(EngineVersion: StructuredEngineVersion));
    }

    private static (string Name, string Email, string? Phone, string? Url, IReadOnlyList<ResumeProfile> Profiles) ExtractBasics(IReadOnlyList<string> nonBlank)
    {
        var name = string.Empty;
        var email = string.Empty;
        string? phone = null;
        string? url = null;
        var profiles = new List<ResumeProfile>();

        foreach (var raw in nonBlank)
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(email))
            {
                var emailMatch = EmailPattern.Match(line);
                if (emailMatch.Success)
                {
                    email = emailMatch.Value;
                    continue;
                }
            }

            if (phone is null)
            {
                var phoneMatch = PhonePattern.Match(line);
                if (phoneMatch.Success)
                {
                    phone = phoneMatch.Value.Trim();
                    continue;
                }
            }

            if (url is null)
            {
                var urlMatch = UrlPattern.Match(line);
                if (urlMatch.Success)
                {
                    url = urlMatch.Value.TrimEnd('.', ',', ';');
                    continue;
                }
            }

            if (string.IsNullOrEmpty(name) && NameLinePattern.IsMatch(line))
            {
                name = line;
            }
        }

        foreach (var raw in nonBlank)
        {
            var line = raw.Trim();
            foreach (Match match in BareDomainPattern.Matches(line))
            {
                var raw2 = match.Value;
                var normalized = raw2.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? raw2
                    : "https://" + raw2;

                var network = InferNetwork(raw2);
                if (network is null)
                {
                    continue;
                }

                if (profiles.Any(p => p.Url.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                profiles.Add(new ResumeProfile(network, normalized));
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            name = "Unknown Candidate";
        }

        if (string.IsNullOrEmpty(email))
        {
            email = "unknown@example.com";
        }

        return (name, email, phone, url, profiles);
    }

    private static bool IsExplicitEmail(string email)
    {
        return EmailPattern.IsMatch(email);
    }

    private static string? InferNetwork(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains("linkedin.com"))
        {
            return "LinkedIn";
        }

        if (lower.Contains("github.com") || lower.Contains("github.io"))
        {
            return "GitHub";
        }

        if (lower.Contains("twitter.com") || lower.Contains("x.com"))
        {
            return "Twitter";
        }

        if (lower.Contains("behance.net"))
        {
            return "Behance";
        }

        if (lower.Contains("dribbble.com"))
        {
            return "Dribbble";
        }

        return null;
    }

    private static IReadOnlyList<TaggedResumeWork> ExtractWork(IReadOnlyList<string> lines)
    {
        var entries = new List<TaggedResumeWork>();
        var (start, end) = FindSection(lines, SectionLineWork);
        if (start < 0)
        {
            return entries;
        }

        var body = SectionBody(lines, start, end);
        var blocks = SplitBlocks(body);
        foreach (var block in blocks)
        {
            var parsed = ParseWorkBlock(block);
            if (parsed.Company is null && parsed.Position is null)
            {
                continue;
            }

            entries.Add(new TaggedResumeWork(
                new ResumeWorkEntry(
                    Name: parsed.Company ?? "Unknown",
                    Position: parsed.Position ?? "Unknown",
                    StartDate: parsed.StartDate ?? "1970-01",
                    EndDate: parsed.EndDate,
                    Summary: parsed.Summary,
                    Highlights: parsed.Highlights),
                new WorkConfidence(
                    Name: ConfidenceMarker.Inferred,
                    Position: ConfidenceMarker.Inferred,
                    StartDate: ConfidenceMarker.Inferred,
                    EndDate: ConfidenceMarker.Inferred,
                    Summary: ConfidenceMarker.Inferred,
                    Highlights: ConfidenceMarker.Inferred)));
        }

        return entries;
    }

    private static (string? Company, string? Position, string? StartDate, string? EndDate, string? Summary, IReadOnlyList<string>? Highlights) ParseWorkBlock(IReadOnlyList<string> block)
    {
        string? company = null;
        string? position = null;
        string? startDate = null;
        string? endDate = null;
        string? summary = null;
        List<string>? highlights = null;

        foreach (var raw in block)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (company is null && line.Contains('·'))
            {
                var parts = line.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    company = parts[0];
                    position = parts[1];
                    ExtractDateFromParts(parts, ref startDate, ref endDate);
                    continue;
                }
            }

            var dateMatch = DateRangePattern.Match(line);
            if (dateMatch.Success)
            {
                startDate ??= dateMatch.Groups[1].Value + "-01";
                var endRaw = dateMatch.Groups[2].Value;
                endDate ??= endRaw.Length == 4 ? endRaw + "-12" : null;
                continue;
            }

            if (position is null && company is null)
            {
                position = line;
                continue;
            }

            if (summary is null)
            {
                summary = line;
            }
            else
            {
                highlights ??= new List<string>();
                highlights.Add(line);
            }
        }

        return (company, position, startDate, endDate, summary, highlights);
    }

    private static void ExtractDateFromParts(string[] parts, ref string? startDate, ref string? endDate)
    {
        foreach (var part in parts)
        {
            var dateMatch = DateRangePattern.Match(part);
            if (!dateMatch.Success)
            {
                continue;
            }

            startDate ??= dateMatch.Groups[1].Value + "-01";
            var endRaw = dateMatch.Groups[2].Value;
            endDate ??= endRaw.Length == 4 ? endRaw + "-12" : null;
        }
    }

    private static IReadOnlyList<TaggedResumeEducation> ExtractEducation(IReadOnlyList<string> lines)
    {
        var entries = new List<TaggedResumeEducation>();
        var (start, end) = FindSection(lines, SectionLineEducation);
        if (start < 0)
        {
            return entries;
        }

        var body = SectionBody(lines, start, end);
        var blocks = SplitBlocks(body);
        foreach (var block in blocks)
        {
            var parsed = ParseEducationBlock(block);
            if (parsed.Institution is null)
            {
                continue;
            }

            entries.Add(new TaggedResumeEducation(
                new ResumeEducationEntry(
                    Institution: parsed.Institution,
                    Area: parsed.Area,
                    StudyType: parsed.StudyType,
                    StartDate: parsed.StartDate ?? "1970-01",
                    EndDate: parsed.EndDate,
                    Score: parsed.Score),
                new EducationConfidence(
                    Institution: ConfidenceMarker.Inferred,
                    Area: ConfidenceMarker.Inferred,
                    StudyType: ConfidenceMarker.Inferred,
                    StartDate: ConfidenceMarker.Inferred,
                    EndDate: ConfidenceMarker.Inferred,
                    Score: ConfidenceMarker.Inferred)));
        }

        return entries;
    }

    private static (string? Institution, string? Area, string? StudyType, string? StartDate, string? EndDate, string? Score) ParseEducationBlock(IReadOnlyList<string> block)
    {
        string? institution = null;
        string? area = null;
        string? studyType = null;
        string? startDate = null;
        string? endDate = null;

        foreach (var raw in block)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (institution is null && line.Contains('·'))
            {
                var parts = line.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    institution = parts[0];
                    area = string.Join(" · ", parts.Skip(1));
                    ExtractDateFromParts(parts, ref startDate, ref endDate);
                    continue;
                }
            }

            var dateMatch = DateRangePattern.Match(line);
            if (dateMatch.Success)
            {
                startDate ??= dateMatch.Groups[1].Value + "-01";
                var endRaw = dateMatch.Groups[2].Value;
                endDate ??= endRaw.Length == 4 ? endRaw + "-12" : null;
                continue;
            }

            if (institution is null)
            {
                institution = line;
            }
        }

        return (institution, area, studyType, startDate, endDate, null);
    }

    private static IReadOnlyList<TaggedResumeSkill> ExtractSkills(IReadOnlyList<string> lines)
    {
        var entries = new List<TaggedResumeSkill>();
        var (start, end) = FindSection(lines, SectionLineSkills);
        if (start < 0)
        {
            return entries;
        }

        var body = SectionBody(lines, start, end);
        var combined = string.Join(' ', body).Trim();
        if (combined.Length == 0)
        {
            return entries;
        }

        var separators = new[] { ',', ';', '\u2022', '·' };
        var parts = combined.Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            entries.Add(new TaggedResumeSkill(
                new ResumeSkillEntry(Name: trimmed, Level: null),
                new SkillConfidence(Name: ConfidenceMarker.Inferred, Level: ConfidenceMarker.Inferred)));
        }

        return entries;
    }

    private static (int Start, int End) FindSection(IReadOnlyList<string> lines, Regex headerPattern)
    {
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (headerPattern.IsMatch(lines[i]))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return (-1, -1);
        }

        var end = lines.Count;
        for (var i = start + 1; i < lines.Count; i++)
        {
            if (SectionLineWork.IsMatch(lines[i])
                || SectionLineEducation.IsMatch(lines[i])
                || SectionLineSkills.IsMatch(lines[i])
                || SectionLineGeneric.IsMatch(lines[i]))
            {
                end = i;
                break;
            }
        }

        return (start, end);
    }

    private static IReadOnlyList<string> SectionBody(IReadOnlyList<string> lines, int start, int end)
    {
        var result = new List<string>();
        for (var i = start + 1; i < end; i++)
        {
            result.Add(lines[i]);
        }
        return result;
    }

    private static List<List<string>> SplitBlocks(IReadOnlyList<string> body)
    {
        var blocks = new List<List<string>>();
        var current = new List<string>();
        foreach (var raw in body)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (current.Count > 0)
                {
                    blocks.Add(current);
                    current = new List<string>();
                }
            }
            else
            {
                current.Add(raw);
            }
        }

        if (current.Count > 0)
        {
            blocks.Add(current);
        }

        return blocks;
    }

    private static IReadOnlyList<ParsingWarning> ConvertLegacyWarnings(IReadOnlyList<ImportWarning> legacy)
    {
        var list = new List<ParsingWarning>(legacy.Count);
        foreach (var w in legacy)
        {
            list.Add(new ParsingWarning(w.Code, w.Message, w.Severity));
        }

        return list;
    }
}
