using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Scoring;

public sealed class SkillMatcherTests
{
    private readonly SkillMatcher _sut = BuildMatcher();

    [Fact]
    public void Exacto_da_credito_pleno_cuando_la_skill_esta_prominente()
    {
        var result = _sut.Match(Req("dotnet", ".NET"), Cv(skills: new() { ["dotnet"] = Placement.Prominent }));

        result.Tier.Should().Be(MatchTier.Exact);
        result.Credit.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Implicacion_ascendente_satisface_el_requisito()
    {
        // El CV tiene ASP.NET Core, que implica .NET ⇒ el requisito .NET queda cubierto.
        var result = _sut.Match(Req("dotnet", ".NET"), Cv(skills: new() { ["aspnet-core"] = Placement.Prominent }));

        result.Tier.Should().Be(MatchTier.Alias);
        result.Credit.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Ubicacion_enterrada_reduce_el_credito()
    {
        var result = _sut.Match(Req("csharp", "C#"), Cv(skills: new() { ["csharp"] = Placement.Buried }));

        result.Tier.Should().Be(MatchTier.Exact);
        result.Credit.Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void Relacion_descendente_da_credito_parcial()
    {
        // El requisito ASP.NET Core es más específico que el .NET del CV ⇒ parcial.
        var result = _sut.Match(Req("aspnet-core", "ASP.NET Core"), Cv(skills: new() { ["dotnet"] = Placement.Prominent }));

        result.Tier.Should().Be(MatchTier.Related);
        result.Credit.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Sin_coincidencia_devuelve_none_sin_credito()
    {
        var result = _sut.Match(Req("docker", "Docker"), Cv(skills: new() { ["csharp"] = Placement.Prominent }));

        result.Tier.Should().Be(MatchTier.None);
        result.Credit.Should().Be(0.0);
        result.Placement.Should().Be(Placement.NotFound);
    }

    [Fact]
    public void Fuzzy_tolera_errores_tipograficos()
    {
        var result = _sut.Match(Req("kubernetes", "Kubernetes"), Cv(tokens: ["kubernets"]));

        result.Tier.Should().Be(MatchTier.Fuzzy);
        result.Credit.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void Lema_cubre_keywords_genericas_por_raiz()
    {
        var result = _sut.Match(Req("liderazgo", "Liderazgo"), Cv(stems: ["liderazg"]));

        result.Tier.Should().Be(MatchTier.Lemma);
    }

    [Fact]
    public void Blindaje_de_confundibles_evita_falsos_positivos()
    {
        // "java" jamás debe coincidir con "javascript" por fuzzy (FR-017).
        var result = _sut.Match(Req("java", "Java"), Cv(tokens: ["javascript"]));

        result.Tier.Should().Be(MatchTier.None);
    }

    private static SkillMatcher BuildMatcher()
    {
        var normalizer = new SpanishTextNormalizer();
        SkillEntry[] entries =
        [
            new("dotnet", ".NET", SkillCategory.Tool, [], [], ["csharp", "aspnet-core"], [], []),
            new("aspnet-core", "ASP.NET Core", SkillCategory.Tool, ["asp.net core"], ["dotnet"], ["csharp"], [], []),
            new("csharp", "C#", SkillCategory.HardSkill, ["c sharp"], ["dotnet"], ["dotnet"], [], ["c"]),
            new("postgresql", "PostgreSQL", SkillCategory.Tool, ["postgres"], ["sql"], ["sql"], ["sql"], []),
            new("sql", "SQL", SkillCategory.HardSkill, [], [], ["postgresql"], [], []),
            new("docker", "Docker", SkillCategory.Tool, [], [], [], [], []),
        ];

        var gazetteer = new SkillGazetteer("test", entries, normalizer);
        return new SkillMatcher(gazetteer, new SpanishLightStemmer(), normalizer, new ConfusableBlocklist());
    }

    private static Requirement Req(string id, string display) =>
        new(id, display, SkillCategory.Tool, RequirementSection.MustHave, Weight: 1.0);

    private static CvProfile Cv(
        Dictionary<string, Placement>? skills = null,
        IEnumerable<string>? tokens = null,
        IEnumerable<string>? stems = null) =>
        new(
            skills ?? [],
            (tokens ?? []).ToHashSet(),
            (stems ?? []).ToHashSet());
}
