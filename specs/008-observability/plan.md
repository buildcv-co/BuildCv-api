# Implementation Plan: 008-observability

**Branch**: `008-observability` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-observability/spec.md`

## Summary

Agregar observability al backend .NET 10: structured logging (JSON), Prometheus metrics, OpenTelemetry tracing, y health checks granulares. Sin nuevos endpoints de negocio. Sin cambios al domain. Solo infra + api layer.

## Technical Context

**Language/Version**: C# / .NET 10.0.100
**Primary Dependencies**: `Microsoft.Extensions.Logging`, `OpenTelemetry.*`, `prometheus-net`
**Storage**: N/A
**Testing**: xUnit + FluentAssertions (existing)
**Target Platform**: Linux container (Docker, Render)
**Project Type**: Web API (ASP.NET Core Minimal APIs)
**Performance Goals**: <1ms overhead per log entry, <10ms for `/metrics` endpoint
**Constraints**: Constitution Art. III (zero PII in logs/traces/metrics), zero new domain types
**Scale/Scope**: Single-instance v0.5.1, observability patch

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Art. | Status | Note |
|---|---|---|
| I (no invención) | ✅ | Observability no inventa nada — solo registra lo que ya pasa |
| II (determinismo) | ✅ | Logging/tracing/metrics no afectan el cálculo del score |
| III (privacidad) | ⚠️ CRITICAL | Logs, traces, y metrics NUNCA incluyen CV content, job description, o PII. Solo metadatos (longitudes, conteos, modelo, traceId) |
| IV (encuadre honesto) | ✅ | Health checks reportan estado real, no promesas |
| V (entrada como dato) | ✅ | Tracing registra que se procesó, no qué se procesó |
| VI (Clean Architecture) | ✅ | Observability vive en Infrastructure + Api, no en Domain |
| VII (rate-limit) | ✅ | Métricas incluyen rate-limit counters |
| VIII (TDD) | ✅ | Tests rojos antes de implementación |
| IX (gates v1) | N/A | No aplica a v0.5.1 |

## Project Structure

### Documentation (this feature)

```text
specs/008-observability/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technical decisions
├── data-model.md        # Types and structures
├── quickstart.md        # Local setup and verification
├── tasks.md             # Implementation tasks
└── contracts/
    └── observability-api.md  # /metrics and /health endpoints
```

### Source Code (repository root)

```text
src/
├── BuildCv.Api/
│   ├── Program.cs                          # MODIFIED: add OpenTelemetry, Prometheus, health checks
│   ├── HealthChecks/                       # NEW: component health checks
│   │   ├── ParserHealthCheck.cs
│   │   ├── AiClientHealthCheck.cs
│   │   └── PdfGeneratorHealthCheck.cs
│   └── Middleware/                         # NEW: request logging middleware
│       └── RequestLoggingMiddleware.cs
├── BuildCv.Infrastructure/
│   ├── Observability/                      # NEW: observability setup
│   │   └── ObservabilityExtensions.cs      # Extension method to configure all observability
│   └── DependencyInjection.cs             # MODIFIED: register observability services
tests/
├── BuildCv.Api.IntegrationTests/
│   └── Observability/                      # NEW: integration tests
│       ├── StructuredLoggingTests.cs
│       ├── PrometheusMetricsTests.cs
│       ├── OpenTelemetryTests.cs
│       └── HealthCheckTests.cs
```

**Structure Decision**: Observability is infrastructure concern. Extension method in Infrastructure wires up all three pillars (logging, metrics, traces). Health checks live in Api because they're endpoint-level. Middleware in Api for request logging.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| NuGet packages (OpenTelemetry, prometheus-net) | Industry-standard observability libraries | Rolling custom metrics = reinventing the wheel, more bugs, no ecosystem |
