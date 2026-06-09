using BuildCv.Application.Features.Adapt;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildCv.Api.Health;

/// <summary>
/// Verifica que el cliente de IA (StubAiClient en v0) está disponible.
/// </summary>
public sealed class AiClientHealthCheck(IAiClient aiClient) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientType = aiClient.GetType().Name;
            return Task.FromResult(HealthCheckResult.Healthy(
                $"AI client disponible: {clientType}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "AI client no disponible",
                ex));
        }
    }
}
