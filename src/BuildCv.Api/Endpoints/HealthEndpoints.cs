using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace BuildCv.Api.Endpoints;

public static class HealthEndpoints
{
    /// <summary>
    /// Expone <c>GET /health/live</c> (¿el proceso está vivo?) y
    /// <c>GET /health/ready</c> (¿listo para servir tráfico real?).
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        return app;
    }
}
