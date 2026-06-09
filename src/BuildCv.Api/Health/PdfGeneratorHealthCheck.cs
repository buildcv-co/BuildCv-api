using BuildCv.Application.Features.Export;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildCv.Api.Health;

/// <summary>
/// Verifica que el generador de PDF (QuestPDF) está disponible.
/// </summary>
public sealed class PdfGeneratorHealthCheck(IPdfGenerator pdfGenerator) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var generatorType = pdfGenerator.GetType().Name;
            return Task.FromResult(HealthCheckResult.Healthy(
                $"PDF generator disponible: {generatorType}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "PDF generator no disponible",
                ex));
        }
    }
}
