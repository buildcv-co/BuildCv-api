using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Scoring;

public sealed class SkillScannerTests
{
    private readonly SkillScanner _sut = BuildScanner();

    [Fact]
    public void Detecta_skills_por_unigrama_bigrama_y_alias()
    {
        var hits = _sut.Scan("Experiencia en C#, ASP.NET Core y PostgreSQL.");

        hits.Should().ContainKey("csharp");
        hits.Should().ContainKey("aspnet-core");
        hits.Should().ContainKey("postgresql");
    }

    [Fact]
    public void Cuenta_la_frecuencia_sumando_canonico_y_alias()
    {
        var hits = _sut.Scan("Uso PostgreSQL en producción; administro postgres a diario.");

        hits["postgresql"].Should().Be(2);
    }

    [Fact]
    public void Consume_el_ngrama_mas_largo_sin_doble_conteo()
    {
        // "ASP.NET Core" se resuelve como un solo skill; no debe contar "core" aparte.
        var hits = _sut.Scan("ASP.NET Core");

        hits.Should().ContainSingle().Which.Key.Should().Be("aspnet-core");
    }

    [Fact]
    public void Texto_sin_skills_no_devuelve_nada()
    {
        _sut.Scan("Persona responsable y puntual.").Should().BeEmpty();
    }

    private static SkillScanner BuildScanner()
    {
        var normalizer = new SpanishTextNormalizer();
        SkillEntry[] entries =
        [
            new("csharp", "C#", SkillCategory.HardSkill, ["c sharp"], [], [], [], []),
            new("aspnet-core", "ASP.NET Core", SkillCategory.Tool, ["asp.net core"], ["dotnet"], [], [], []),
            new("postgresql", "PostgreSQL", SkillCategory.Tool, ["postgres"], ["sql"], [], [], []),
            new("dotnet", ".NET", SkillCategory.Tool, [], [], [], [], []),
        ];

        var gazetteer = new SkillGazetteer("test", entries, normalizer);
        return new SkillScanner(gazetteer, normalizer);
    }
}
