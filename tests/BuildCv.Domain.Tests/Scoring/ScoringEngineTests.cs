using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Scoring;

public sealed class ScoringEngineTests
{
    private const string Vacante = """
        Ingeniero Backend .NET

        Requisitos:
        C#, ASP.NET Core, SQL, Docker

        Deseable:
        Azure
        """;

    private const string CvFuerte = """
        Juan Pérez
        juan.perez@example.com | +57 300 123 4567
        Desarrollador Backend .NET

        PERFIL
        Backend con 4 años de experiencia.

        EXPERIENCIA
        - Lideré la migración a contenedores con Docker.
        - Reduje la latencia de las APIs en un 30%.
        - Desarrollé servicios en C# con ASP.NET Core sobre SQL.

        EDUCACIÓN
        Ingeniería de Sistemas

        HABILIDADES
        C#, ASP.NET Core, SQL, Docker, Azure
        """;

    private const string CvDebil = "Persona responsable busca empleo. Sé algo de programación.";

    private readonly Harness _harness = Harness.Build();

    [Fact]
    public void El_puntaje_global_esta_en_rango()
    {
        var result = _harness.Score(Vacante, CvFuerte);

        result.Overall.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Un_cv_fuerte_supera_a_uno_debil()
    {
        var fuerte = _harness.Score(Vacante, CvFuerte).Overall;
        var debil = _harness.Score(Vacante, CvDebil).Overall;

        fuerte.Should().BeGreaterThan(debil);
    }

    [Fact]
    public void Es_determinista_para_la_misma_entrada()
    {
        var a = _harness.Score(Vacante, CvFuerte);
        var b = _harness.Score(Vacante, CvFuerte);

        a.Overall.Should().Be(b.Overall);
        a.ContextHash.Should().Be(b.ContextHash);
        a.Components.Select(c => c.SubScore).Should().Equal(b.Components.Select(c => c.SubScore));
    }

    [Fact]
    public void El_componente_de_formato_declara_medibilidad_parcial_en_v0()
    {
        var result = _harness.Score(Vacante, CvFuerte);

        result.Components.Single(c => c.Id == ComponentId.Format).Measurability.Should().Be(0.5);
        result.GatesApplied.Should().Contain(g => g.Reason == "partial-measurement");
    }

    [Fact]
    public void Clasifica_keywords_presentes_y_faltantes()
    {
        var result = _harness.Score(Vacante, CvFuerte);

        result.Keywords.Present.Select(k => k.CanonicalTerm).Should().Contain("C#");
        result.Keywords.Present.Select(k => k.CanonicalTerm).Should().Contain("Docker");
    }

    [Fact]
    public void Un_cv_sin_contacto_activa_la_compuerta_de_estructura()
    {
        var result = _harness.Score(Vacante, CvDebil);

        result.GatesApplied.Should().Contain(g => g.Reason == "no-contact");
    }

    [Fact]
    public void Sella_versiones_de_motor_y_lexico()
    {
        var result = _harness.Score(Vacante, CvFuerte);

        result.EngineVersion.Should().Be(ScoringEngine.VersionV1);
        result.LexiconVersion.Should().Be("test-lex");
    }

    private sealed class Harness(IJobAnalyzer jobAnalyzer, ICvAnalyzer cvAnalyzer, IScoringEngine engine)
    {
        public ScoreResult Score(string job, string cv) =>
            engine.Score(jobAnalyzer.Analyze(job), cvAnalyzer.Analyze(cv));

        public static Harness Build()
        {
            var normalizer = new SpanishTextNormalizer();
            SkillEntry[] entries =
            [
                new("csharp", "C#", SkillCategory.HardSkill, ["c sharp"], ["dotnet"], ["dotnet"], [], ["c"]),
                new("dotnet", ".NET", SkillCategory.Tool, [], [], ["csharp", "aspnet-core"], [], []),
                new("aspnet-core", "ASP.NET Core", SkillCategory.Tool, ["asp.net core"], ["dotnet"], ["csharp"], [], []),
                new("sql", "SQL", SkillCategory.HardSkill, [], [], [], [], []),
                new("docker", "Docker", SkillCategory.Tool, [], [], [], [], []),
                new("azure", "Azure", SkillCategory.Tool, [], [], [], [], []),
            ];

            var gazetteer = new SkillGazetteer("test-lex", entries, normalizer);
            var splitter = new SectionSplitter(normalizer);
            var scanner = new SkillScanner(gazetteer, normalizer);
            var stemmer = new SpanishLightStemmer();
            var matcher = new SkillMatcher(gazetteer, stemmer, normalizer, new ConfusableBlocklist());

            return new Harness(
                new JobAnalyzer(splitter, scanner, gazetteer),
                new CvAnalyzer(splitter, scanner, normalizer, stemmer),
                new ScoringEngine(matcher, gazetteer));
        }
    }
}
