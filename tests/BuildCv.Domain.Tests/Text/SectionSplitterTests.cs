using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Text;

public sealed class SectionSplitterTests
{
    private readonly SectionSplitter _sut = new(new SpanishTextNormalizer());

    private static readonly Dictionary<string, string> Headers = new()
    {
        ["experiencia"] = "experience",
        ["habilidades"] = "skills",
        ["educacion"] = "education",
    };

    [Fact]
    public void Separa_secciones_por_encabezados_y_conserva_el_preambulo()
    {
        const string cv = """
            Juan Pérez
            Desarrollador Backend

            EXPERIENCIA
            Trabajé en una fintech construyendo APIs.

            HABILIDADES
            C#, .NET, PostgreSQL
            """;

        var sections = _sut.Split(cv, Headers, preambleLabel: "header");

        sections.Select(s => s.Label).Should().Equal("header", "experience", "skills");
        sections.Single(s => s.Label == "skills").Body.Should().Contain("PostgreSQL");
        sections.Single(s => s.Label == "header").Body.Should().Contain("Juan Pérez");
    }

    [Fact]
    public void Reconoce_encabezados_con_variantes_y_acentos()
    {
        const string cv = "EDUCACIÓN\nIngeniería de Sistemas";

        var sections = _sut.Split(cv, Headers, preambleLabel: "header");

        sections.Should().Contain(s => s.Label == "education");
    }

    [Fact]
    public void Una_linea_larga_no_se_confunde_con_encabezado()
    {
        const string cv = "Tengo experiencia en habilidades de comunicación y trabajo en equipo siempre";

        var sections = _sut.Split(cv, Headers, preambleLabel: "header");

        sections.Should().ContainSingle().Which.Label.Should().Be("header");
    }
}
