using System.Net;
using FluentAssertions;

namespace BuildCv.Api.IntegrationTests;

public sealed class PrometheusMetricsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Metrics_devuelve_200_con_content_type_prometheus()
    {
        var response = await _client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString()
            .Should().StartWith("text/plain");
    }

    [Fact]
    public async Task Metrics_contiene_metricas_http()
    {
        // Hacemos un request primero para generar métricas
        await _client.GetAsync("/health/live");

        var response = await _client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // prometheus-net expone métricas HTTP (nombre puede variar según versión)
        content.Should().Contain("http_requests");
    }

    [Fact]
    public async Task Metrics_contiene_http_request_duration()
    {
        await _client.GetAsync("/health/live");

        var response = await _client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        content.Should().Contain("http_request_duration");
    }
}
