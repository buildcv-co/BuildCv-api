# Tasks: 008-observability

**Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Strict TDD**: tests rojos PRIMERO. Cero supresiones (Constitution Art. VIII). Hito **v0.5.1**.

## Phase 0 — Setup

- [ ] **T0.1** Agregar NuGet packages a `BuildCv.Infrastructure.csproj`: `prometheus-net.AspNetCore`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`.
- [ ] **T0.2** Crear `BuildCv.Infrastructure/Observability/ObservabilityExtensions.cs` con extension method `AddBuildCvObservability(this IServiceCollection, IConfiguration)`.

## Phase 1 — Structured Logging (US1, P1)

### Request Logging Middleware

- [ ] **T1.1** [TEST RED] `StructuredLoggingTests.Request_Logs_Contain_Required_Fields` — make a request, verify JSON log has `traceId`, `requestId`, `durationMs`, `statusCode`.
- [ ] **T1.2** [TEST RED] `StructuredLoggingTests.Error_Logs_Contain_Exception_Info` — trigger 500, verify log has `exceptionType`, `errorCode`.
- [ ] **T1.3** [TEST RED] `StructuredLoggingTests.Logs_Never_Contain_PII` — make request with CV content, verify log does NOT contain CV text.
- [ ] **T1.4** [IMPL] Crear `BuildCv.Api/Middleware/RequestLoggingMiddleware.cs` — intercepts request, measures duration, logs structured JSON.
- [ ] **T1.5** [IMPL] Configurar `AddJsonConsole()` en `Program.cs` con timestamp format ISO 8601.
- [ ] **T1.6** [GREEN] Tests T1.1–T1.3 pasan.

## Phase 2 — Prometheus Metrics (US2, P2)

### Metrics Endpoint

- [ ] **T2.1** [TEST RED] `PrometheusMetricsTests.Metrics_Endpoint_Returns_Prometheus_Format` — `GET /metrics` returns `text/plain; version=0.0.4`.
- [ ] **T2.2** [TEST RED] `PrometheusMetricsTests.Http Requests_Are_Counted` — make 5 requests, verify `http_requests_total` incremented.
- [ ] **T2.3** [TEST RED] `PrometheusMetricsTests.Request_Duration_Is_Recorded` — make request, verify `http_request_duration_ms` histogram updated.
- [ ] **T2.4** [TEST RED] `PrometheusMetricsTests.Metrics_Never_Contain_PII` — verify no labels with CV content.
- [ ] **T2.5** [IMPL] Configurar `prometheus-net` en `ObservabilityExtensions.cs`: counters + histograms with labels `endpoint`, `method`, `status`.
- [ ] **T2.6** [IMPL] Mapear `app.MapMetrics()` en `Program.cs` para `GET /metrics`.
- [ ] **T2.7** [GREEN] Tests T2.1–T2.4 pasan.

## Phase 3 — OpenTelemetry Tracing (US3, P3)

### Tracing Setup

- [ ] **T3.1** [TEST RED] `OpenTelemetryTests.Request_Creates_Span_With_Http_Attributes` — make request, verify span has `http.method`, `http.url`, `http.status_code`.
- [ ] **T3.2** [TEST RED] `OpenTelemetryTests.Trace_Never_Contains_PII` — verify span attributes don't include CV content.
- [ ] **T3.3** [TEST RED] `OpenTelemetryTests.NoOp_When_Otlp_Not_Configured` — verify no exceptions when `OTEL_EXPORTER_OTLP_ENDPOINT` is not set.
- [ ] **T3.4** [IMPL] Configurar OpenTelemetry en `ObservabilityExtensions.cs`: `AddSource("BuildCv.Api")`, ASP.NET Core instrumentation, OTLP exporter (conditional on env var).
- [ ] **T3.5** [GREEN] Tests T3.1–T3.3 pasan.

## Phase 4 — Health Checks (US4, P3)

### Component Health Checks

- [ ] **T4.1** [TEST RED] `HealthCheckTests.Ready_Endpoint_Returns_Component_Status` — `GET /health/ready` returns per-component status.
- [ ] **T4.2** [TEST RED] `HealthCheckTestshealthy_Component_Returns_Healthy` — parser health check returns Healthy when parser is available.
- [ ] **T4.3** [TEST RED] `HealthCheckTests.Degraded_Component_Returns_Degraded` — parser health check returns Degraded when parser is slow (>5s).
- [ ] **T4.4** [TEST RED] `HealthCheckTests.Health_Checks_Have_Timeout` — health checks complete within 5s even if component is slow.
- [ ] **T4.5** [IMPL] Crear `BuildCv.Api/HealthChecks/ParserHealthCheck.cs` — checks parser availability.
- [ ] **T4.6** [IMPL] Crear `BuildCv.Api/HealthChecks/AiClientHealthCheck.cs` — checks AI client availability.
- [ ] **T4.7** [IMPL] Crear `BuildCv.Api/HealthChecks/PdfGeneratorHealthCheck.cs` — checks PDF generator availability.
- [ ] **T4.8** [IMPL] Registrar health checks en `ObservabilityExtensions.cs` con tags y timeouts.
- [ ] **T4.9** [GREEN] Tests T4.1–T4.4 pasan.

## Phase 5 — Wire-up & Integration

- [ ] **T5.1** [IMPL] En `Program.cs`, llamar `builder.Services.AddBuildCvObservability(builder.Configuration)` después de otros servicios.
- [ ] **T5.2** [IMPL] En `Program.cs`, mapear `app.MapMetrics()` y `app.MapHealthChecks("/health/ready")` con tags.
- [ ] **T5.3** [IMPL] En `Program.cs`, registrar `RequestLoggingMiddleware` con `app.UseMiddleware<RequestLoggingMiddleware>()`.
- [ ] **T5.4** [TEST RED] `IntegrationTests.Healthy_System_Returns_Healthy_Status` — full integration test: start app, hit all endpoints, verify health is Healthy.
- [ ] **T5.5** [GREEN] Integration test passes.

## Phase 6 — Pre-merge verification

- [ ] **T6.1** `dotnet build BuildCv.slnx -c Release` → 0 warnings.
- [ ] **T6.2** `dotnet test` → all tests pass (existing 189 + new observability tests).
- [ ] **T6.3** `dotnet format --verify-no-changes` → 0 formatting issues.
- [ ] **T6.4** `./scripts/constitution-check.sh` → exit 0 (cite Art. III for privacy).
- [ ] **T6.5** Manual test: `curl /health/ready`, `curl /metrics`, check structured logs in console.
- [ ] **T6.6** Verify no PII in logs/metrics/traces (manual review).

## Critical Path (TDD ordering)

```
T0 (setup: packages + extension method)
  ↓
T1 (structured logging: middleware + JSON console)
  ↓
T2 (prometheus metrics: counters + histograms + /metrics endpoint)
  ↓
T3 (opentelemetry tracing: spans + OTLP export)
  ↓
T4 (health checks: per-component IHealthCheck implementations)
  ↓
T5 (wire-up: Program.cs integration)
  ↓
T6 (pre-merge verification)
```

## Risks Per Phase

| Phase | Risk | Mitigation |
|---|---|---|
| T1 | JSON logging overhead on high traffic | `AddJsonConsole()` is async + buffered; benchmark with `dotnet-trace` |
| T2 | `/metrics` endpoint slow under load | `prometheus-net` uses atomic counters, no locking; benchmark with k6 |
| T3 | OTLP exporter blocks if collector is down | Configure with `ExportProcessorType.Batch` + timeout |
| T4 | Health check timeout too aggressive | 5s default timeout; configurable per component |
| T5 | Breaking existing `/health` endpoint | Backward compatible: add new checks, keep existing endpoint |
