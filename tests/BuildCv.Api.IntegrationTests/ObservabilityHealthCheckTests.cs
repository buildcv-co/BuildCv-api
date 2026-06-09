using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace BuildCv.Api.IntegrationTests;

public sealed class ObservabilityHealthCheckTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Ready_devuelve_status_con_componentes()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_contiene_results_con_componentes()
    {
        var response = await _client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.TryGetProperty("results", out var results).Should().BeTrue();
        results.GetArrayLength().Should().BeGreaterThan(0);
    }
}
