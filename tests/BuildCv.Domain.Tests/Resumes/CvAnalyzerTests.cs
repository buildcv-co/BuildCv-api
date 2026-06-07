using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Resumes;

public sealed class CvAnalyzerTests
{
    private const string Cv = """
        Juan Pérez
        juan.perez@example.com | +57 300 123 4567
        Desarrollador Backend

        PERFIL
        Backend con 4 años en .NET.

        EXPERIENCIA
        - Lideré la migración a microservicios.
        - Reduje la latencia de las APIs en un 30%.
        - Desarrollé servicios en C# y PostgreSQL.

        HABILIDADES
        C#, ASP.NET Core, PostgreSQL, Docker
        """;

    private readonly CvAnalyzer _sut = BuildAnalyzer();

    [Fact]
    public void Detecta_las_secciones_estandar()
    {
        var analysis = _sut.Analyze(Cv);

        analysis.SectionsPresent.Should().Contain(["summary", "experience", "skills"]);
    }

    [Fact]
    public void Detecta_contacto_y_experiencia()
    {
        var analysis = _sut.Analyze(Cv);

        analysis.HasContact.Should().BeTrue();
        analysis.HasExperience.Should().BeTrue();
    }

    [Fact]
    public void Una_skill_en_habilidades_es_prominente()
    {
        var analysis = _sut.Analyze(Cv);

        analysis.Profile.SkillPlacements["docker"].Should().Be(Placement.Prominent);
    }

    [Fact]
    public void Una_skill_solo_en_experiencia_queda_enterrada()
    {
        var analysis = _sut.Analyze(Cv);

        analysis.Profile.SkillPlacements["microservices"].Should().Be(Placement.Buried);
    }

    [Fact]
    public void Cuenta_verbos_de_accion_y_logros_cuantificados()
    {
        var analysis = _sut.Analyze(Cv);

        analysis.ActionVerbCount.Should().BeGreaterThanOrEqualTo(2);
        analysis.QuantifiedAchievementCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Sin_contacto_lo_reporta()
    {
        var analysis = _sut.Analyze("EXPERIENCIA\nDesarrollé software.\nHABILIDADES\nC#");

        analysis.HasContact.Should().BeFalse();
    }

    private static CvAnalyzer BuildAnalyzer()
    {
        var normalizer = new SpanishTextNormalizer();
        SkillEntry[] entries =
        [
            new("csharp", "C#", SkillCategory.HardSkill, ["c sharp"], ["dotnet"], [], [], []),
            new("dotnet", ".NET", SkillCategory.Tool, [], [], [], [], []),
            new("aspnet-core", "ASP.NET Core", SkillCategory.Tool, ["asp.net core"], ["dotnet"], [], [], []),
            new("postgresql", "PostgreSQL", SkillCategory.Tool, ["postgres"], [], [], [], []),
            new("docker", "Docker", SkillCategory.Tool, [], [], [], [], []),
            new("microservices", "Microservicios", SkillCategory.HardSkill, ["microservicios"], [], [], [], []),
        ];

        var gazetteer = new SkillGazetteer("test", entries, normalizer);
        return new CvAnalyzer(
            new SectionSplitter(normalizer),
            new SkillScanner(gazetteer, normalizer),
            normalizer,
            new SpanishLightStemmer());
    }
}
