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

    [Fact]
    public async Task Post_With_EngineVersion_Header_2_0_0_Returns_ScoreResponse_With_PerSection_And_RedFlags_And_EngineVersion_2_0_0()
    {
        var request = new
        {
            cv = new
            {
                basics = new
                {
                    name = "Ada Lovelace",
                    email = "ada@example.com",
                    profiles = Array.Empty<object>(),
                    confidence = new
                    {
                        name = "explicit",
                        email = "explicit",
                        phone = "inferred",
                        location = "inferred",
                        url = "inferred",
                        profiles = "inferred",
                        summary = "inferred",
                        datosPersonales = "inferred",
                    },
                },
                work = new[]
                {
                    new
                    {
                        entry = new
                        {
                            name = "Acme Corp",
                            position = "Senior Backend Developer",
                            startDate = "2020-01",
                            endDate = "2024-12",
                        },
                        confidence = new
                        {
                            name = "explicit",
                            position = "explicit",
                            startDate = "explicit",
                            endDate = "explicit",
                            summary = "inferred",
                            highlights = "inferred",
                        },
                    },
                },
                education = Array.Empty<object>(),
                skills = new[]
                {
                    new { entry = new { name = "C#", level = (string?)null }, confidence = new { name = "explicit", level = "inferred" } },
                    new { entry = new { name = ".NET", level = (string?)null }, confidence = new { name = "explicit", level = "inferred" } },
                    new { entry = new { name = "PostgreSQL", level = (string?)null }, confidence = new { name = "explicit", level = "inferred" } },
                },
                projects = Array.Empty<object>(),
                certificates = Array.Empty<object>(),
                languages = Array.Empty<object>(),
                meta = new { engineVersion = "2.0.0" },
            },
            job = new
            {
                title = "Senior Backend Developer",
                company = "Acme S.A.",
                description = "Buscamos ingeniero backend con experiencia en .NET.",
                location = "Bogotá, Colombia",
                employmentType = "FullTime",
                requirements = new[] { "C#", ".NET", "PostgreSQL" },
            },
            engineVersion = "2.0.0",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/score", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ScoreResponse>();
        body.Should().NotBeNull();
        body!.EngineVersion.Should().Be("2.0.0");
        body.PerSection.Should().NotBeNull();
        body.PerSection!.Experience.Should().NotBeNull();
        body.PerSection.Skills.Should().NotBeNull();
        body.RedFlags.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_With_EngineVersion_Header_1_0_0_Returns_Legacy_ScoreResponse()
    {
        var request = new
        {
            cvText = CvText,
            jobText = JobText,
            engineVersion = "1.0.0",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/score", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ScoreResponse>();
        body.Should().NotBeNull();
        body!.EngineVersion.Should().Be("1.0.0");
        body.Components.Should().NotBeEmpty();
        body.PerSection.Should().BeNull();
        body.RedFlags.Should().BeNull();
    }
}
