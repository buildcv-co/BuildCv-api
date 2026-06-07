using BuildCv.Domain.Lexicon;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BuildCv.Api.IntegrationTests;

/// <summary>
/// Verifica que el YAML embebido se carga y se resuelve vía el contenedor de DI real.
/// </summary>
public sealed class GazetteerWiringTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly ISkillGazetteer _gazetteer =
        factory.Services.GetRequiredService<ISkillGazetteer>();

    [Fact]
    public void El_gazetteer_embebido_se_carga_y_sella_su_version()
    {
        _gazetteer.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("c#", "csharp")]
    [InlineData("postgres", "postgresql")]
    [InlineData("asp.net core", "aspnet-core")]
    [InlineData("k8s", "kubernetes")]
    public void Resuelve_skills_conocidas_del_yaml(string normalizedToken, string expectedId)
    {
        _gazetteer.TryResolve(normalizedToken, out var entry).Should().BeTrue();
        entry!.Id.Should().Be(expectedId);
    }

    [Fact]
    public void Mantiene_los_confundibles_definidos_en_el_yaml()
    {
        _gazetteer.AreConfusable("java", "javascript").Should().BeTrue();
    }
}
