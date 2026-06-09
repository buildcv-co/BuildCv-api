# Tasks: 008-observability

**Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Strict TDD**: tests rojos PRIMERO. Cero supresiones (Constitution Art. VIII). Hito **v0.5.1**.
> **Status**: ✅ ALL TASKS COMPLETE (verified 2026-06-09, commit `4975966`)

## Phase 0 — Setup

- [x] **T0.1** Agregar NuGet packages a `BuildCv.Api.csproj`: `prometheus-net.AspNetCore`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`.
- [x] **T0.2** ~~Crear `BuildCv.Infrastructure/Observability/ObservabilityExtensions.cs`~~ → **Deviation**: wired directly in `Program.cs` (simpler, no extra abstraction needed for 3 packages).

## Phase 1 — Structured Logging (US1, P1)

> **Deviation**: Structured logging was ALREADY configured via Serilog (`Serilog.AspNetCore` + `Serilog.Sinks.Console`). No custom `RequestLoggingMiddleware` needed. Serilog's `UseSerilogRequestLogging()` provides request logging with traceId, duration, statusCode.

- [x] **T1.1** [IMPL] Structured logging already exists via Serilog — `Program.cs` line 23-27.
- [x] **T1.2** [IMPL] `UseSerilogRequestLogging()` already configured — `Program.cs` line 76.
- [x] **T1.3** [IMPL] Privacy: Serilog only logs metadata (cvLength, jobLength, model, traceId) — Constitution Art. III.

## Phase 2 — Prometheus Metrics (US2, P2)

### Metrics Endpoint

- [x] **T2.1** [TEST RED] `PrometheusMetricsTests.Metrics_devuelve_200_con_content_type_prometheus` — `GET /metrics` returns 200 with text/plain.
- [x] **T2.2** [TEST RED] `PrometheusMetricsTests.Metrics_contiene_metricas_http` — make request, verify `http_requests` in metrics output.
- [x] **T2.3** [TEST RED] `PrometheusMetricsTests.Metrics_contiene_http_request_duration` — verify `http_request_duration` histogram.
- [x] **T2.4** [IMPL] Configurar `prometheus-net` en `Program.cs`: `builder.Services.AddMetrics()` + `app.UseHttpMetrics()`.
- [x] **T2.5** [IMPL] Mapear `app.MapMetrics()` en `Program.cs` para `GET /metrics`.
- [x] **T2.6** [GREEN] Tests T2.1–T2.3 pasan.

## Phase 3 — OpenTelemetry Tracing (US3, P3)

### Tracing Setup

- [x] **T3.1** [IMPL] Configurar OpenTelemetry en `Program.cs`: `AddAspNetCoreInstrumentation()` + `AddOtlpExporter()`.
- [x] **T3.2** [IMPL] OTLP exporter is no-op when `OTEL_EXPORTER_OTLP_ENDPOINT` not set (no exceptions).
- [x] **T3.3** [IMPL] Privacy: span attributes only include HTTP semantic conventions (method, url, status_code).

## Phase 4 — Health Checks (US4, P3)

### Component Health Checks

- [x] **T4.1** [TEST RED] `ObservabilityHealthCheckTests.Ready_devuelve_status_con_componentes` — `GET /health/ready` returns status "Healthy".
- [x] **T4.2** [TEST RED] `ObservabilityHealthCheckTests.Ready_contiene_results_con_componentes` — response has results array with components.
- [x] **T4.3** [IMPL] Crear `BuildCv.Api/Health/ParserHealthCheck.cs` — checks parser availability.
- [x] **T4.4** [IMPL] Crear `BuildCv.Api/Health/AiClientHealthCheck.cs` — checks AI client availability.
- [x] **T4.5** [IMPL] Crear `BuildCv.Api/Health/PdfGeneratorHealthCheck.cs` — checks PDF generator availability.
- [x] **T4.6** [IMPL] Registrar health checks en `Program.cs` con tags `["ready"]`.
- [x] **T4.7** [IMPL] Actualizar `HealthEndpoints.cs` para devolver JSON detallado con per-component status.
- [x] **T4.8** [GREEN] Tests T4.1–T4.2 pasan.

## Phase 5 — Wire-up & Integration

- [x] **T5.1** [IMPL] En `Program.cs`: `builder.Services.AddMetrics()`, `AddOpenTelemetry()`, health checks registrados.
- [x] **T5.2** [IMPL] En `Program.cs`: `app.UseHttpMetrics()`, `app.MapMetrics()`, health endpoints actualizados.
- [x] **T5.3** [IMPL] OpenTelemetry tracing configurado con ASP.NET Core instrumentation + OTLP exporter.

## Phase 6 — Pre-merge verification

- [x] **T6.1** `dotnet build BuildCv.slnx -c Release` → 0 warnings.
- [x] **T6.2** `dotnet test` → 194/194 tests pass (189 existing + 5 new observability tests).
- [x] **T6.3** `dotnet format --verify-no-changes` → 0 formatting issues.
- [x] **T6.4** Constitution check: no PII in logs/metrics/traces (manual review).
- [x] **T6.5** Domain purity: `dotnet list src/BuildCv.Domain package` → 0 packages.

## Critical Path (TDD ordering)

```
T0 (setup: packages)
  ↓
T1 (structured logging: already existed via Serilog)
  ↓
T2 (prometheus metrics: UseHttpMetrics + MapMetrics)
  ↓
T3 (opentelemetry tracing: ASP.NET Core + OTLP)
  ↓
T4 (health checks: Parser, AiClient, PdfGenerator)
  ↓
T5 (wire-up: Program.cs integration)
  ↓
T6 (pre-merge verification)
```

## Deviations from original plan

| Task | Original | Shipped | Reason |
|---|---|---|---|
| T0.2 | ObservabilityExtensions.cs | Wired in Program.cs | Simpler for 3 packages, no abstraction needed |
| Phase 1 | Custom RequestLoggingMiddleware + AddJsonConsole | Serilog (already existed) | Structured logging was already configured |
| T2.5 | ObservabilityExtensions.cs | Program.cs directly | Consistent with no-extensions pattern |
| T4.3-T4.5 | BuildCv.Api/HealthChecks/ | BuildCv.Api/Health/ | Consistent with existing AiConfigHealthCheck location |

## Risks Per Phase

| Phase | Risk | Mitigation |
|---|---|---|
| T1 | JSON logging overhead on high traffic | Serilog is async + buffered; already battle-tested |
| T2 | `/metrics` endpoint slow under load | `prometheus-net` uses atomic counters, no locking |
| T3 | OTLP exporter blocks if collector is down | No-op when OTEL_EXPORTER_OTLP_ENDPOINT not set |
| T4 | Health check timeout too aggressive | Light checks (type inspection), no I/O |
| T5 | Breaking existing `/health` endpoint | Backward compatible: added new checks, kept existing endpoint |
