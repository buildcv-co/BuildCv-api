using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Text;

public sealed class ConfusableBlocklistTests
{
    private readonly ConfusableBlocklist _sut = new();

    [Theory]
    [InlineData("java", "javascript")]
    [InlineData("javascript", "java")]
    [InlineData("c", "c#")]
    [InlineData("c#", "c")]
    [InlineData("react", "react native")]
    [InlineData("go", "mongo")]
    [InlineData("r", "ruby")]
    public void Detecta_pares_confundibles_en_ambos_sentidos(string a, string b)
    {
        _sut.AreConfusable(a, b).Should().BeTrue();
    }

    [Theory]
    [InlineData("java", "c#")]
    [InlineData("python", "rust")]
    [InlineData("postgresql", "postgres")]
    public void No_marca_pares_no_confundibles(string a, string b)
    {
        _sut.AreConfusable(a, b).Should().BeFalse();
    }

    [Fact]
    public void Una_palabra_no_es_confundible_consigo_misma()
    {
        _sut.AreConfusable("java", "Java").Should().BeFalse();
    }
}
