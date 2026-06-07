using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Text;

public sealed class SpanishTextNormalizerTests
{
    private readonly SpanishTextNormalizer _sut = new();

    [Fact]
    public void Preserva_la_enye_distinguiendo_año_de_ano()
    {
        _sut.Normalize("Año").Should().Be("año");
        _sut.Normalize("ano").Should().Be("ano");
        _sut.Normalize("Año").Should().NotBe(_sut.Normalize("ano"));
    }

    [Theory]
    [InlineData("Sé programar en C#", "se programar en c#")]
    [InlineData("Experiencia con .NET y Node.js", "experiencia con .net y node.js")]
    [InlineData("Pipelines de CI/CD", "pipelines de ci/cd")]
    [InlineData("ASP.NET Core y EF Core", "asp.net core y ef core")]
    [InlineData("Programo en C++ y C#", "programo en c++ y c#")]
    public void Protege_tokens_tecnicos_y_quita_acentos(string input, string expected)
    {
        _sut.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Aplica_minusculas_invariantes_y_colapsa_espacios()
    {
        _sut.Normalize("  DESARROLLADOR   Backend  ").Should().Be("desarrollador backend");
    }

    [Fact]
    public void Quita_puntuacion_no_significativa()
    {
        _sut.Normalize("Python, Django; FastAPI.").Should().Be("python django fastapi");
    }

    [Fact]
    public void Entrada_vacia_o_blanca_devuelve_cadena_vacia()
    {
        _sut.Normalize("   ").Should().BeEmpty();
        _sut.Normalize("").Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_separa_en_palabras_normalizadas()
    {
        _sut.Tokenize("C# y PostgreSQL").Should().Equal("c#", "y", "postgresql");
    }
}
