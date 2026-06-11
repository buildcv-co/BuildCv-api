using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Api.IntegrationTests.Payments;

public sealed class PaymentEndpointsDisabledTests : IDisposable
{
    private readonly DisabledPaymentWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public PaymentEndpointsDisabledTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Checkout_returns_404_when_Wompi_disabled()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/payments/checkout",
            new { packageId = "starter" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_returns_404_when_Wompi_disabled()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/payments/webhook",
            new { data = new { id = "tx-1", status = "APPROVED" } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPayment_returns_404_when_Wompi_disabled()
    {
        var response = await _client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListPayments_returns_404_when_Wompi_disabled()
    {
        var response = await _client.GetAsync("/api/v1/payments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public sealed class DisabledPaymentWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key",
                ["Wompi:Enabled"] = "false",
            }));

        return base.CreateHost(builder);
    }
}
