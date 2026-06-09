# Data Model: 008-observability

> **Source of truth:** Observability types are configuration-level, not domain entities. No new domain types.

## Observability Configuration Types

### StructuredLogEntry (configured via `AddJsonConsole`, not a custom type)

ASP.NET Core's JSON console formatter produces structured logs automatically. No custom type needed.

```csharp
// Configuration in Program.cs:
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});
```

Standard fields emitted by ASP.NET Core:
- `timestamp`, `level`, `message`, `category`, `eventId`, `exception`
- Custom fields added via `LogInformation("Score request (cvLength={CvLen}, jobLength={JobLen})", cvLen, jobLen)`

### Prometheus Metrics (configured via `prometheus-net`, not custom types)

```csharp
// Counter: total HTTP requests by endpoint, method, status
private static readonly Counter HttpRequestsTotal = Metrics
    .CreateCounter("http_requests_total", "Total HTTP requests",
        new[] { "endpoint", "method", "status" });

// Histogram: request duration in milliseconds
private static readonly Histogram HttpRequestDurationMs = Metrics
    .CreateHistogram("http_request_duration_ms", "HTTP request duration in milliseconds",
        new[] { "endpoint", "method" });
```

### OpenTelemetry Spans (configured via SDK, not custom types)

```csharp
// Span attributes (standard HTTP semantic conventions):
// - http.method: "GET", "POST"
// - http.url: "/api/v1/score"
// - http.status_code: 200, 400, 500
// - duration.ms: 150
```

### ComponentHealth (returned by health check endpoint)

```csharp
// ASP.NET Core HealthCheckRegistration already provides this structure.
// No custom type needed — the health check response format is standard.
```

## Observability Extension Method

```csharp
// BuildCv.Infrastructure/Observability/ObservabilityExtensions.cs
public static class ObservabilityExtensions
{
    public static IServiceCollection AddBuildCvObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Structured logging (JSON console)
        // 2. Prometheus metrics endpoint
        // 3. OpenTelemetry tracing (OTLP export)
        // 4. Health checks (per-component)
        return services;
    }
}
```

## Files Affected

| File | Action | Notes |
|---|---|---|
| `BuildCv.Api/Program.cs` | MODIFIED | Add `AddBuildCvObservability()` call, `MapMetrics()`, enhanced `MapHealthChecks()` |
| `BuildCv.Infrastructure/Observability/ObservabilityExtensions.cs` | NEW | Extension method wiring all observability |
| `BuildCv.Infrastructure/DependencyInjection.cs` | MODIFIED | Register observability services |
| `BuildCv.Api/HealthChecks/ParserHealthCheck.cs` | NEW | Checks parser availability |
| `BuildCv.Api/HealthChecks/AiClientHealthCheck.cs` | NEW | Checks AI client availability |
| `BuildCv.Api/HealthChecks/PdfGeneratorHealthCheck.cs` | NEW | Checks PDF generator availability |
| `BuildCv.Api/Middleware/RequestLoggingMiddleware.cs` | NEW | Structured request logging |

## Constitution Compliance (Art. III)

**Privacy filter**: All observability output is filtered to exclude PII:
- Logs: only metadata (cvLength, jobLength, model, traceId)
- Traces: only HTTP attributes (method, url, status_code)
- Metrics: only HTTP labels (endpoint, method, status)
- Health checks: only component status (Healthy/Degraded/Unhealthy)
