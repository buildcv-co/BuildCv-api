using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Text;

public sealed class SpanishLightStemmerTests
{
    private readonly SpanishLightStemmer _sut = new();

    [Theory]
    [InlineData("gestionar", "gestiono")]
    [InlineData("desarrollo", "desarrolle")]
    [InlineData("implementado", "implementados")]
    [InlineData("desarrollador", "desarrolladores")]
    public void Variantes_morfologicas_comparten_la_raiz(string a, string b)
    {
        _sut.Stem(a).Should().Be(_sut.Stem(b));
    }

    [Theory]
    [InlineData("sql")]
    [InlineData("api")]
    [InlineData("css")]
    public void No_estimiza_palabras_cortas(string word)
    {
        _sut.Stem(word).Should().Be(word);
    }

    [Fact]
    public void Cadena_vacia_se_devuelve_igual()
    {
        _sut.Stem("").Should().BeEmpty();
    }
}
