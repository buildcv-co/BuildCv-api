using System.Text;
using System.Text.RegularExpressions;
using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace BuildCv.Infrastructure.Parsing;

/// <summary>
/// Adaptador de ICvParser para DOCX (MIT, Microsoft Open XML SDK).
/// Constitución Art. VI: el parseo vive en Infrastructure, detrás del puerto ICvParser.
/// Art. III: el archivo se procesa en RAM, nunca se persiste.
/// Art. I: confidence solo emite <c>inferred</c> o <c>explicit</c>; <c>user_confirmed</c>
/// es exclusivo del editor (PR 4 de 021).
/// Micro-batch 2c de 021: además de la ruta legacy 1.0.0, expone la ruta
/// <see cref="IStructuredParser"/> 2.0.0 que devuelve <see cref="StructuredParseResult"/>
/// preservando la estructura del DOCX (tablas y listas como
/// <see cref="ResumeWorkEntry.Highlights"/>, sin aplanar con '\t').
/// </summary>
public sealed class OpenXmlCvParser : ICvParser, IStructuredParser, IKnownMimeParser
{
    private const int MaxTextLength = 50_000;
    private const string LegacyEngineVersion = "1.0.0";
    private const string StructuredEngineVersion = "2.0.0";
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <inheritdoc />
    public string SupportedMimeType => DocxMimeType;

    private const string WorkHeaderRegex =
        @"^\s*(?:EXPERIENCE|WORK\s+EXPERIENCE|EMPLOYMENT|EXPERIENCIA|EXPERIENCIA\s+LABORAL|TRABAJO)\s*[\.\:]*\s*$";
    private const string EducationHeaderRegex =
        @"^\s*(?:EDUCATION|ACADEMIC|EDUCACION|EDUCACIÓN|FORMACION|FORMACIÓN)\s*[\.\:]*\s*$";
    private const string SkillsHeaderRegex =
        @"^\s*(?:SKILLS|TECHNICAL\s+SKILLS|HABILIDADES|HABILIDADES\s+TECNICAS|HABILIDADES\s+TÉCNICAS|COMPETENCIAS)\s*[\.\:]*\s*$";
    private const string ProjectsHeaderRegex =
        @"^\s*(?:PROJECTS|PROYECTOS)\s*[\.\:]*\s*$";
    private const string GenericHeaderRegex =
        @"^\s*(?:CONTACTO|PERFIL|RESUMEN|IDIOMAS|CERTIFICACIONES|REFERENCIAS|PUBLICACIONES|CONTACT|PROFILE|SUMMARY|LANGUAGES|CERTIFICATIONS|REFERENCES|PUBLICATIONS)\s*[\.\:]*\s*$";

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
    private static readonly Regex DateRangePattern = new(
        @"(\d{4})\s*[\-–—]\s*(\d{4}|present|actualidad|hoy)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NameLinePattern = new(
        @"^([A-ZÁÉÍÓÚÑ][a-záéíóúñ']+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ']+){1,3})\s*$",
        RegexOptions.Compiled);
    private static readonly Regex BulletPrefixPattern = new(
        @"^\s*[•\-\*·◦▪►]+\s*",
        RegexOptions.Compiled);

    private static readonly Regex SectionLineWork = new(WorkHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineEducation = new(EducationHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineSkills = new(SkillsHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineProjects = new(ProjectsHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLineGeneric = new(GenericHeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

            var sb = new StringBuilder();
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

            return new LegacyImportResult(
                text,
                sections,
                warnings,
                command.TraceId);
        }
    }

    ParseResult IStructuredParser.Parse(ImportCvCommand command)
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

            var context = new StructuredParseContext();
            foreach (var element in body.Elements())
            {
                VisitElement(element, context);
            }

            var cv = BuildStructuredCv(context);
            var warnings = new List<ParsingWarning>(ConvertLegacyWarnings(context.Warnings));

            var hasAnySection = context.HasWork
                || context.HasEducation
                || context.HasSkills
                || context.HasProjects;
            if (!hasAnySection)
            {
                warnings.Add(new ParsingWarning(
                    "DOCX_NO_SEMANTIC_STRUCTURE",
                    "No se detectaron secciones por heurística. El editor permitirá marcarlas manualmente.",
                    "Info"));
            }

            return new StructuredParseResult(cv, warnings);
        }
    }

    private static void VisitElement(DocumentFormat.OpenXml.OpenXmlElement element, StructuredParseContext ctx)
    {
        switch (element)
        {
            case Paragraph p:
                VisitParagraph(p, ctx);
                break;
            case Table table:
                VisitTable(table, ctx);
                break;
        }
    }

    private static void VisitParagraph(Paragraph paragraph, StructuredParseContext ctx)
    {
        var text = paragraph.InnerText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var trimmed = text.Trim();

        if (SectionLineWork.IsMatch(trimmed))
        {
            ctx.Section = StructuredSection.Work;
            ctx.HasWork = true;
            return;
        }

        if (SectionLineEducation.IsMatch(trimmed))
        {
            ctx.Section = StructuredSection.Education;
            ctx.HasEducation = true;
            return;
        }

        if (SectionLineSkills.IsMatch(trimmed))
        {
            ctx.Section = StructuredSection.Skills;
            ctx.HasSkills = true;
            return;
        }

        if (SectionLineProjects.IsMatch(trimmed))
        {
            ctx.Section = StructuredSection.Projects;
            ctx.HasProjects = true;
            return;
        }

        if (SectionLineGeneric.IsMatch(trimmed))
        {
            ctx.Section = StructuredSection.Other;
            return;
        }

        if (ctx.Section == StructuredSection.None)
        {
            ctx.PreludeLines.Add(trimmed);
            return;
        }

        var stripped = BulletPrefixPattern.Replace(trimmed, string.Empty).Trim();
        if (stripped.Length == 0)
        {
            return;
        }

        switch (ctx.Section)
        {
            case StructuredSection.Work:
                ctx.WorkLines.Add(stripped);
                break;
            case StructuredSection.Education:
                ctx.EducationLines.Add(stripped);
                break;
            case StructuredSection.Skills:
                ctx.SkillLines.Add(stripped);
                break;
            case StructuredSection.Projects:
                ctx.ProjectLines.Add(stripped);
                break;
        }
    }

    private static void VisitTable(Table table, StructuredParseContext ctx)
    {
        if (ctx.Section == StructuredSection.None)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var preludeRow = row.Elements<TableCell>()
                    .Select(c => c.InnerText.Trim())
                    .Where(t => t.Length > 0)
                    .ToList();
                if (preludeRow.Count > 0)
                {
                    ctx.PreludeLines.Add(string.Join(" | ", preludeRow));
                }
            }

            return;
        }

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>()
                .Select(c => c.InnerText.Trim())
                .Where(t => t.Length > 0)
                .ToList();
            if (cells.Count == 0)
            {
                continue;
            }

            var rowText = string.Join(" | ", cells);

            switch (ctx.Section)
            {
                case StructuredSection.Work:
                    ctx.WorkLines.Add(rowText);
                    break;
                case StructuredSection.Education:
                    ctx.EducationLines.Add(rowText);
                    break;
                case StructuredSection.Skills:
                    ctx.SkillLines.Add(rowText);
                    break;
                case StructuredSection.Projects:
                    ctx.ProjectLines.Add(rowText);
                    break;
            }
        }
    }

    private static CvDocument BuildStructuredCv(StructuredParseContext ctx)
    {
        var (name, email, phone, url, profiles) = ExtractBasics(ctx.PreludeLines);

        var work = ExtractWork(ctx.WorkLines);
        var education = ExtractEducation(ctx.EducationLines);
        var skills = ExtractSkills(ctx.SkillLines);
        var projects = ExtractProjects(ctx.ProjectLines);

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
            Projects: projects,
            Certificates: Array.Empty<TaggedResumeCertificate>(),
            Languages: Array.Empty<TaggedResumeLanguage>(),
            Meta: new CvMeta(EngineVersion: StructuredEngineVersion));
    }

    private static (string Name, string Email, string? Phone, string? Url, IReadOnlyList<ResumeProfile> Profiles) ExtractBasics(IReadOnlyList<string> preludeLines)
    {
        var name = string.Empty;
        var email = string.Empty;
        string? phone = null;
        string? url = null;
        var profiles = new List<ResumeProfile>();

        foreach (var line in preludeLines)
        {
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

        foreach (var line in preludeLines)
        {
            foreach (Match match in BareDomainPattern.Matches(line))
            {
                var raw = match.Value;
                var normalized = raw.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? raw
                    : "https://" + raw;

                var network = InferNetwork(raw);
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
        if (lines.Count == 0)
        {
            return entries;
        }

        var blocks = SplitBlocks(lines);
        foreach (var block in blocks)
        {
            var parsed = ParseWorkBlock(block);
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
            if (dateMatch.Success && company is null)
            {
                startDate = dateMatch.Groups[1].Value + "-01";
                var endRaw = dateMatch.Groups[2].Value;
                endDate ??= endRaw.Length == 4 ? endRaw + "-12" : null;
                continue;
            }

            if (company is null)
            {
                position ??= line;
                continue;
            }

            highlights ??= new List<string>();
            highlights.Add(line);
        }

        return (company, position, startDate, endDate, summary, highlights);
    }

    private static IReadOnlyList<TaggedResumeEducation> ExtractEducation(IReadOnlyList<string> lines)
    {
        var entries = new List<TaggedResumeEducation>();
        if (lines.Count == 0)
        {
            return entries;
        }

        var blocks = SplitBlocks(lines);
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

        return (institution, area, null, startDate, endDate, null);
    }

    private static IReadOnlyList<TaggedResumeSkill> ExtractSkills(IReadOnlyList<string> lines)
    {
        var entries = new List<TaggedResumeSkill>();
        if (lines.Count == 0)
        {
            return entries;
        }

        var combined = string.Join(' ', lines).Trim();
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

    private static IReadOnlyList<TaggedResumeProject> ExtractProjects(IReadOnlyList<string> lines)
    {
        var entries = new List<TaggedResumeProject>();
        if (lines.Count == 0)
        {
            return entries;
        }

        var blocks = SplitBlocks(lines);
        foreach (var block in blocks)
        {
            var cleaned = block.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
            if (cleaned.Count == 0)
            {
                continue;
            }

            var name = cleaned[0];
            var highlights = cleaned.Count > 1 ? cleaned.Skip(1).ToList() : null;

            entries.Add(new TaggedResumeProject(
                new ResumeProjectEntry(
                    Name: name,
                    Description: null,
                    Highlights: highlights,
                    Keywords: null,
                    StartDate: null,
                    EndDate: null,
                    Url: null),
                new ProjectConfidence(
                    Name: ConfidenceMarker.Inferred,
                    Description: ConfidenceMarker.Inferred,
                    Highlights: ConfidenceMarker.Inferred,
                    Keywords: ConfidenceMarker.Inferred,
                    StartDate: ConfidenceMarker.Inferred,
                    EndDate: ConfidenceMarker.Inferred,
                    Url: ConfidenceMarker.Inferred)));
        }

        return entries;
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

    private static void AppendElementText(DocumentFormat.OpenXml.OpenXmlElement element, StringBuilder sb)
    {
        switch (element)
        {
            case Paragraph p:
                var paragraphText = p.InnerText;
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    sb.AppendLine(paragraphText);
                }
                break;
            case Table table:
                foreach (var row in table.Elements<TableRow>())
                {
                    var cells = row.Elements<TableCell>()
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

    private enum StructuredSection
    {
        None,
        Work,
        Education,
        Skills,
        Projects,
        Other,
    }

    private sealed class StructuredParseContext
    {
        public List<string> PreludeLines { get; } = new();
        public List<string> WorkLines { get; } = new();
        public List<string> EducationLines { get; } = new();
        public List<string> SkillLines { get; } = new();
        public List<string> ProjectLines { get; } = new();
        public List<ImportWarning> Warnings { get; } = new();
        public StructuredSection Section { get; set; } = StructuredSection.None;
        public bool HasWork { get; set; }
        public bool HasEducation { get; set; }
        public bool HasSkills { get; set; }
        public bool HasProjects { get; set; }
    }
}
