using System.Threading.RateLimiting;

namespace BuildCv.Api.Security;

/// <summary>Políticas de rate limiting por IP (anti-abuso v0, FR-036/038, Constitution Art. VII).</summary>
public static class RateLimiting
{
    public const string ScorePolicy = "score";
    public const string AiPolicy = "ai";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ScorePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Política "ai" — estricta: 5 adaptaciones por hora por IP (protege presupuesto).
            options.AddPolicy(AiPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string ClientKey(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
