using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildCv.Api.Health;

/// <summary>
/// Comprobación de preparación: el servicio solo está 'ready' si la clave de IA está
/// configurada (de lo contrario la adaptación de CV no podría funcionar).
/// </summary>
public sealed class AiConfigHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Ai:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        var result = string.IsNullOrWhiteSpace(apiKey)
            ? HealthCheckResult.Unhealthy("Falta la clave de IA (Ai:ApiKey o ANTHROPIC_API_KEY).")
            : HealthCheckResult.Healthy("Clave de IA configurada.");

        return Task.FromResult(result);
    }
}
