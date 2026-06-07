using System.Net;
using System.Net.Http.Json;
using BuildCv.Api.Contracts;
using FluentAssertions;

namespace BuildCv.Api.IntegrationTests;

public sealed class ScoringEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string JobText =
        "Buscamos Ingeniero Backend .NET. Requisitos: C#, ASP.NET Core, SQL, Docker. " +
        "Deseable: Azure, Kubernetes. Valoramos trabajo en equipo.";

    private const string CvText = """
        Juan Pérez — juan.perez@example.com — +57 300 123 4567
        Desarrollador Backend .NET

        PERFIL
        Backend con cuatro años de experiencia construyendo servicios.

        EXPERIENCIA
        - Lideré la migración a contenedores con Docker.
        - Reduje la latencia de las APIs en un 30%.
        - Desarrollé servicios en C# con ASP.NET Core sobre SQL y PostgreSQL.

        HABILIDADES
        C#, ASP.NET Core, SQL, Docker, Azure
        """;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Score_devuelve_un_analisis_completo()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/score", new { cvText = CvText, jobText = JobText });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ScoreResponse>();
        body.Should().NotBeNull();
        body!.OverallScore.Should().BeInRange(0, 100);
        body.Components.Should().HaveCount(5);
        body.KeywordAnalysis.Present.Select(k => k.CanonicalTerm).Should().Contain("C#");
        body.ContextId.Should().NotBeNullOrWhiteSpace();
        body.HonestyNotice.Should().Contain("No es");
    }

    [Fact]
    public async Task Score_rechaza_un_cv_demasiado_corto_con_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/score", new { cvText = "muy corto", jobText = JobText });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
