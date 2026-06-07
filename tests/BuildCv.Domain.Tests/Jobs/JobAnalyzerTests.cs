using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Jobs;

public sealed class JobAnalyzerTests
{
    private const string Vacante = """
        Ingeniero Backend .NET

        Requisitos:
        C#, ASP.NET Core, SQL, Docker

        Deseable:
        Azure, Kubernetes
        """;

    private readonly JobAnalyzer _sut = BuildAnalyzer();

    [Fact]
    public void Extrae_los_skills_de_la_vacante()
    {
        var set = _sut.Analyze(Vacante);

        set.Requirements.Select(r => r.CanonicalId)
            .Should().Contain(["csharp", "aspnet-core", "sql", "docker", "azure", "kubernetes"]);
    }

    [Fact]
    public void Clasifica_por_seccion_must_have_vs_deseable()
    {
        var set = _sut.Analyze(Vacante);

        set.Requirements.Single(r => r.CanonicalId == "csharp").Section
            .Should().Be(RequirementSection.MustHave);
        set.Requirements.Single(r => r.CanonicalId == "azure").Section
            .Should().Be(RequirementSection.NiceToHave);
    }

    [Fact]
    public void Un_requisito_obligatorio_pesa_mas_que_uno_deseable()
    {
        var set = _sut.Analyze(Vacante);

        var docker = set.Requirements.Single(r => r.CanonicalId == "docker").Weight;
        var kubernetes = set.Requirements.Single(r => r.CanonicalId == "kubernetes").Weight;

        docker.Should().BeGreaterThan(kubernetes);
    }

    [Fact]
    public void El_context_hash_es_determinista_para_la_misma_entrada()
    {
        _sut.Analyze(Vacante).ContextHash.Should().Be(_sut.Analyze(Vacante).ContextHash);
    }

    [Fact]
    public void Vacantes_distintas_producen_hashes_distintos()
    {
        var otra = "Buscamos Desarrollador Frontend. Requisitos: React, TypeScript.";

        _sut.Analyze(Vacante).ContextHash.Should().NotBe(_sut.Analyze(otra).ContextHash);
    }

    private static JobAnalyzer BuildAnalyzer()
    {
        var normalizer = new SpanishTextNormalizer();
        SkillEntry[] entries =
        [
            new("csharp", "C#", SkillCategory.HardSkill, ["c sharp"], ["dotnet"], [], [], []),
            new("dotnet", ".NET", SkillCategory.Tool, [], [], ["csharp"], [], []),
            new("aspnet-core", "ASP.NET Core", SkillCategory.Tool, ["asp.net core"], ["dotnet"], [], [], []),
            new("sql", "SQL", SkillCategory.HardSkill, [], [], [], [], []),
            new("docker", "Docker", SkillCategory.Tool, [], [], [], [], []),
            new("azure", "Azure", SkillCategory.Tool, [], [], [], [], []),
            new("kubernetes", "Kubernetes", SkillCategory.Tool, ["k8s"], [], [], [], []),
            new("react", "React", SkillCategory.Tool, [], [], [], [], []),
            new("typescript", "TypeScript", SkillCategory.HardSkill, ["ts"], [], [], [], []),
        ];

        var gazetteer = new SkillGazetteer("test", entries, normalizer);
        return new JobAnalyzer(new SectionSplitter(normalizer), new SkillScanner(gazetteer, normalizer), gazetteer);
    }
}
