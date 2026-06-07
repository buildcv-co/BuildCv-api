using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Api.IntegrationTests;

/// <summary>
/// Arranca la API en memoria para pruebas de integración. En M1 aquí se sustituye
/// <c>IAiClient</c> por un <c>FakeAiClient</c> (sin red ni tokens).
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key",
            }));

        return base.CreateHost(builder);
    }
}
