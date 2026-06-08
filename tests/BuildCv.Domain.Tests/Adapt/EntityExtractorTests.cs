using BuildCv.Domain.Adapt;
using BuildCv.Domain.Lexicon;
using FluentAssertions;
using Xunit;

namespace BuildCv.Domain.Tests.Adapt;

public sealed class EntityExtractorTests
{
    private readonly EntityExtractor _extractor;

    public EntityExtractorTests()
    {
        ISkillGazetteer gazetteer = new InMemorySkillGazetteer(
            new SkillEntry("skill.csharp", "C#", SkillCategory.HardSkill, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
            new SkillEntry("skill.dotnet", ".NET", SkillCategory.HardSkill, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
            new SkillEntry("skill.aws", "AWS", SkillCategory.Tool, new[] { "Amazon Web Services" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
        _extractor = new EntityExtractor(gazetteer);
    }

    [Fact]
    public void Should_extract_known_skills_from_cv()
    {
        var cv = "Backend developer con 5 años de experiencia en C# y .NET. Conocimientos de AWS y Azure.";

        var result = _extractor.Extract(cv);

        result.Skills.Should().Contain(new[] { "C#", ".NET", "AWS" });
    }

    [Fact]
    public void Should_extract_companies_with_known_prefixes()
    {
        var cv = "Trabajé en Acme Corp durante 3 años. Luego en Globex como senior dev.";

        var result = _extractor.Extract(cv);

        result.Companies.Should().Contain(new[] { "Acme Corp", "Globex" });
    }

    [Fact]
    public void Should_extract_dates_in_dd_mm_yyyy_format()
    {
        var cv = "Experiencia desde 01/2020 hasta 12/2023. También trabajé entre 2018 y 2019.";

        var result = _extractor.Extract(cv);

        result.Dates.Should().Contain("01/2020");
        result.Dates.Should().Contain("12/2023");
    }

    [Fact]
    public void Should_extract_metrics_with_percent_sign()
    {
        var cv = "Mejoré el performance en 35%. Aumenté usuarios activos en 5x. Procesé 1M requests.";

        var result = _extractor.Extract(cv);

        result.Metrics.Should().Contain(m => m.Contains("35%"));
        result.Metrics.Should().Contain(m => m.Contains("5x", StringComparison.OrdinalIgnoreCase) || m.Contains("1M", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_extract_certifications_from_known_list()
    {
        var cv = "Certificaciones: AWS Certified Solutions Architect, Scrum Master, PMP.";

        var result = _extractor.Extract(cv);

        result.Certifications.Should().Contain("AWS Certified Solutions Architect");
        result.Certifications.Should().Contain("Scrum Master");
        result.Certifications.Should().Contain("PMP");
    }

    [Fact]
    public void Should_extract_titles()
    {
        var cv = "Senior Backend Developer con experiencia en liderazgo. Junior frontend.";

        var result = _extractor.Extract(cv);

        result.Titles.Should().Contain("Senior");
    }

    [Fact]
    public void Should_handle_empty_input()
    {
        var result = _extractor.Extract("");

        result.Skills.Should().BeEmpty();
        result.Companies.Should().BeEmpty();
        result.Dates.Should().BeEmpty();
        result.Metrics.Should().BeEmpty();
        result.Certifications.Should().BeEmpty();
        result.Titles.Should().BeEmpty();
    }

    [Fact]
    public void Should_not_duplicate_entities()
    {
        var cv = "C# developer. C# expert. C# architect. C# mentor.";

        var result = _extractor.Extract(cv);

        result.Skills.Count(s => s == "C#").Should().Be(1);
    }
}

internal sealed class InMemorySkillGazetteer : ISkillGazetteer
{
    private readonly Dictionary<string, SkillEntry> _byId;

    public InMemorySkillGazetteer(params SkillEntry[] entries)
    {
        _byId = entries.ToDictionary(e => e.Id);
    }

    public string Version => "test-1.0";

    public bool TryResolve(string normalizedToken, out SkillEntry entry)
    {
        entry = _byId.Values.FirstOrDefault(e =>
            string.Equals(e.Canonical, normalizedToken, StringComparison.OrdinalIgnoreCase) ||
            e.Aliases.Any(a => string.Equals(a, normalizedToken, StringComparison.OrdinalIgnoreCase)))!;
        return entry is not null;
    }

    public bool TryGetById(string canonicalId, out SkillEntry entry)
    {
        var ok = _byId.TryGetValue(canonicalId, out var e);
        entry = e!;
        return ok;
    }

    public IReadOnlyList<string> Related(string canonicalId) => Array.Empty<string>();

    public IReadOnlyList<string> Implies(string canonicalId) => Array.Empty<string>();

    public bool AreConfusable(string a, string b) => false;
}
