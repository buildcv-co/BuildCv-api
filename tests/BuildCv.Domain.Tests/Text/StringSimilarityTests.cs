using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Text;

public sealed class StringSimilarityTests
{
    [Fact]
    public void Levenshtein_cuenta_ediciones()
    {
        StringSimilarity.Levenshtein("postgres", "postgresql").Should().Be(2);
        StringSimilarity.Levenshtein("abc", "abc").Should().Be(0);
    }

    [Fact]
    public void NormalizedLevenshtein_en_rango_y_extremos()
    {
        StringSimilarity.NormalizedLevenshtein("", "").Should().Be(1.0);
        StringSimilarity.NormalizedLevenshtein("kubernetes", "kubernetes").Should().Be(1.0);
        StringSimilarity.NormalizedLevenshtein("kubernetes", "kubernets").Should().BeGreaterThan(0.85);
    }

    [Fact]
    public void JaroWinkler_identicas_es_uno()
    {
        StringSimilarity.JaroWinkler("desarrollador", "desarrollador").Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void JaroWinkler_premia_prefijo_compartido()
    {
        StringSimilarity.JaroWinkler("postgresql", "postgres").Should().BeGreaterThan(0.9);
        StringSimilarity.JaroWinkler("javascript", "java").Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void JaroWinkler_sin_solape_es_cero()
    {
        StringSimilarity.JaroWinkler("", "abc").Should().Be(0.0);
        StringSimilarity.JaroWinkler("abc", "xyz").Should().Be(0.0);
    }
}
