# Quickstart: 008-observability

## Local Setup

```bash
cd BuildCv-api

# 1. Install new NuGet packages
dotnet add src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj package prometheus-net.AspNetCore
dotnet add src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj package OpenTelemetry.Extensions.Hosting
dotnet add src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj package OpenTelemetry.Exporter.OpenTelemetryProtocol

# 2. Build
dotnet build BuildCv.slnx -c Release

# 3. Run tests
dotnet test

# 4. Start the API
dotnet run --project src/BuildCv.Api

# 5. Verify observability endpoints
curl http://localhost:5080/health/ready
curl http://localhost:5080/metrics
```

## Verification Commands

### Structured Logging

```bash
# Make a request and check logs (should see JSON log entry)
curl -X POST http://localhost:5080/api/v1/score \
  -H "Content-Type: application/json" \
  -d '{"cvText":"Experiencia en Python","jobDescription":"Busco desarrollador"}'

# Expected log output (JSON):
# {"timestamp":"...","level":"Information","message":"Score request completed",
#  "cvLength":20,"jobLength":30,"model":"1.0.0","durationMs":15,"statusCode":200,
#  "traceId":"...","requestId":"..."}
```

### Prometheus Metrics

```bash
# Check metrics endpoint
curl http://localhost:5080/metrics

# Expected: Prometheus text format with:
# - http_requests_total{endpoint="/api/v1/score",method="POST",status="200"} 1
# - http_request_duration_ms_bucket{endpoint="/api/v1/score",method="POST",le="100"} 1
```

### OpenTelemetry Tracing

```bash
# With OTLP exporter configured (env vars):
# OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
# OTEL_SERVICE_NAME=buildcv-api

# Make a request and check trace in Jaeger/Zipkin
curl -X POST http://localhost:5080/api/v1/score \
  -H "Content-Type: application/json" \
  -d '{"cvText":"Experiencia en Python","jobDescription":"Busco desarrollador"}'
```

### Health Checks

```bash
# Check granular health
curl http://localhost:5080/health/ready

# Expected response:
# {
#   "status": "Healthy",
#   "results": {
#     "parser": { "status": "Healthy", "durationMs": 2 },
#     "ai-client": { "status": "Healthy", "durationMs": 1 },
#     "pdf-generator": { "status": "Healthy", "durationMs": 3 }
#   }
# }
```

## Rollback

If observability causes issues, remove the `AddBuildCvObservability()` call from `Program.cs` and the health check registrations. The rest of the application continues working without observability.
