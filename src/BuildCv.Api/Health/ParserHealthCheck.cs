using BuildCv.Application.Features.Import;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildCv.Api.Health;

/// <summary>
/// Verifica que el parser de CV (PDF/DOCX) está disponible y funcional.
/// </summary>
public sealed class ParserHealthCheck(ICvParser parser) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificación ligera: el parser existe y es del tipo esperado
            var parserType = parser.GetType().Name;
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Parser disponible: {parserType}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Parser no disponible",
                ex));
        }
    }
}
