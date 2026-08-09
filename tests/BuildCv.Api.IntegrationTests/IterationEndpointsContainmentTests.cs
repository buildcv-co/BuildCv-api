using System.Net;
using System.Text;
using BuildCv.Application.Features.Iterations;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Api.IntegrationTests;

public sealed class IterationEndpointsContainmentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task Routes_are_not_mapped_when_public_api_is_not_enabled(string? gateValue)
    {
        using var factory = new IterationContainmentWebApplicationFactory(gateValue);
        using var client = factory.CreateClient();
        using var malformedBody = new StringContent("not-json", Encoding.UTF8, "application/json");

        var postResponse = await client.PostAsync("/api/v1/adapt/iterate", malformedBody);
        var getResponse = await client.GetAsync($"/api/v1/adapt/iterate/{Guid.NewGuid()}");

        postResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        getResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }
}

public sealed class IterationContainmentWebApplicationFactory(string? gateValue) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = "test-key",
            ["Ai:Provider"] = "Stub",
            ["LocalAuth:Enabled"] = "false",
        };

        if (gateValue is not null)
        {
            settings["Iteration:PublicApiEnabled"] = gateValue;
        }

        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIterationService>();
            services.AddSingleton<IIterationService>(_ =>
                throw new InvalidOperationException("Iteration service must not be resolved while public routes are disabled."));
        });

        return base.CreateHost(builder);
    }
}
