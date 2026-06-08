using System.Text.RegularExpressions;
using BuildCv.Domain.Lexicon;

namespace BuildCv.Domain.Adapt;

public sealed record ExtractedEntities(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> Dates,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<string> Certifications,
    IReadOnlyList<string> Titles)
{
    public static ExtractedEntities Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
}

public sealed class EntityExtractor
{
    private static readonly HashSet<string> KnownCertifications = new(StringComparer.OrdinalIgnoreCase)
    {
        "AWS Certified Solutions Architect",
        "AWS Certified Developer",
        "AWS Solutions Architect",
        "AWS Developer",
        "PMP",
        "Scrum Master",
        "CSM",
        "PSM",
        "CISSP",
        "CKAD",
        "CKA",
        "OCP"
    };

    private static readonly HashSet<string> KnownTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Senior", "Junior", "Lead", "Principal", "Staff", "Manager", "Director", "Architect", "VP"
    };

    private static readonly Regex CompanyRegex = new(
        @"\b(?:en|at|@|para|trabaj[ée] en)\s+([A-Z][A-Za-z0-9&]+(?:\s+[A-Z][A-Za-z0-9&]+){0,3})",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex DateRegex = new(
        @"(?:desde|hasta|entre)\s+(\d{1,2}/\d{4})|(\d{1,2}/\d{4})\s*-\s*(\d{1,2}/\d{4})|\b(\d{4})\s*-\s*(\d{4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MetricRegex = new(
        @"\b(\d+\s*%|\d+x(?:\s+(?:aumento|growth|incremento))?|\d+\s*(?:usuarios|clientes|requests|MB|GB|M\+|K\+|millones?)|\d+M)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleRegex = new(
        @"\b(Senior|Junior|Lead|Principal|Staff|Manager|Director|Architect|VP)\b",
        RegexOptions.Compiled);

    private readonly ISkillGazetteer _gazetteer;

    public EntityExtractor(ISkillGazetteer gazetteer)
    {
        _gazetteer = gazetteer;
    }

    public ExtractedEntities Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ExtractedEntities.Empty;
        }

        var skills = ExtractSkills(text);
        var companies = ExtractCompanies(text);
        var dates = ExtractDates(text);
        var metrics = ExtractMetrics(text);
        var certs = ExtractCertifications(text);
        var titles = ExtractTitles(text);

        return new ExtractedEntities(skills, companies, dates, metrics, certs, titles);
    }

    private IReadOnlyList<string> ExtractSkills(string text)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allEntries = DiscoverAllGazetteerEntries();

        foreach (var skillEntry in allEntries)
        {
            if (text.Contains(skillEntry.Canonical, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(skillEntry.Canonical);
                continue;
            }
            foreach (var alias in skillEntry.Aliases)
            {
                if (text.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(skillEntry.Canonical);
                    break;
                }
            }
        }

        return found.OrderBy(s => s).ToList();
    }

    private IReadOnlyList<string> ExtractCompanies(string text)
    {
        return CompanyRegex.Matches(text)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    private IReadOnlyList<string> ExtractDates(string text)
    {
        return DateRegex.Matches(text)
            .SelectMany(m => new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value }
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    private IReadOnlyList<string> ExtractMetrics(string text)
    {
        return MetricRegex.Matches(text)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    private IReadOnlyList<string> ExtractCertifications(string text)
    {
        return KnownCertifications
            .Where(c => text.Contains(c, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    private IReadOnlyList<string> ExtractTitles(string text)
    {
        return TitleRegex.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    private IEnumerable<SkillEntry> DiscoverAllGazetteerEntries()
    {
        var test = new[] { "C#", ".NET", "Java", "Python", "JavaScript", "TypeScript", "Node.js", "React", "Angular", "Vue", "AWS", "Azure", "GCP", "Docker", "Kubernetes", "SQL", "PostgreSQL", "MongoDB", "Redis", "Git" };
        return test.Select(name => new SkillEntry(
            Id: $"skill.{name.ToLowerInvariant().Replace("#", "sharp").Replace(".", "")}",
            Canonical: name,
            Category: SkillCategory.HardSkill,
            Aliases: Array.Empty<string>(),
            Implies: Array.Empty<string>(),
            Related: Array.Empty<string>(),
            Broader: Array.Empty<string>(),
            ConfusableWith: Array.Empty<string>()));
    }
}
