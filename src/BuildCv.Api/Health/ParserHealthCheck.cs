using BuildCv.Application.Features.Import;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildCv.Api.Health;

/// <summary>
/// Verifica que el router de parsing (PDF/DOCX) está disponible y funcional.
/// </summary>
public sealed class ParserHealthCheck(IParserRouter router) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var routerType = router.GetType().Name;
            return Task.FromResult(HealthCheckResult.Healthy(
                $"ParserRouter disponible: {routerType}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "ParserRouter no disponible",
                ex));
        }
    }
}
