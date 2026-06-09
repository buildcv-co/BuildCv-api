# Research: 008-observability

**Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Audiencia:** sub-agente `sdd-apply` y revisores. Documenta las decisiones de fondo y las alternativas descartadas, con evidencia.

---

## D01 — ¿Qué librería de structured logging usar?

**Decisión**: `Microsoft.Extensions.Logging` (ya en ASP.NET Core) + `Serilog` o JSON formatter nativo.

**Alternativas descartadas**:
- **NLog**: Más configuración, menos integración nativa con ASP.NET Core.
- **log4net**: Legacy, sin soporte activo.

**Justificación**: ASP.NET Core ya tiene `ILogger<T>` integrado. El formatter JSON se configura con `AddJsonConsole()` en `ConfigureLogging`. Sin nuevas dependencias para logging básico.

---

## D02 — ¿Prometheus o Metrics.NET para métricas?

**Decisión**: `prometheus-net` (NuGet: `prometheus-net.AspNetCore`)

**Alternativas descartadas**:
- **Metrics.NET**: Menos popular, menor ecosistema de integración.
- **OpenTelemetry Metrics**: Aún en preview para .NET, menor madurez que prometheus-net.
- **Custom `/metrics` endpoint**: Reinventar la rueda, sin soporte de formato Prometheus text exposition.

**Justificación**: `prometheus-net` es la librería estándar para Prometheus en .NET. Soporta counters, histograms, gauges. Se integra con `app.MapMetrics()` en un endpoint. Comunidad activa, documentación clara.

---

## D03 — ¿OpenTelemetry para tracing?

**Decisión**: `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.AspNetCore` + `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP)

**Alternativas descartadas**:
- **Zipkin direct**: Menor estandarización, lock-in a Zipkin.
- **App Insights**: Lock-in a Azure.
- **Custom ActivitySource**: Más trabajo, sin ecosistema de instrumentación automática.

**Justificación**: OpenTelemetry es el estándar CNCF para tracing. El SDK de .NET es maduro (GA desde 2023). La instrumentación automática de ASP.NET Core captura HTTP requests sin código custom. OTLP es el formato estándar para exportar traces.

---

## D04 — ¿Cómo manejar health checks granulares?

**Decisión**: `Microsoft.Extensions.Diagnostics.HealthChecks` (ya en ASP.NET Core) + custom `IHealthCheck` implementations por componente.

**Alternativas descartadas**:
- **AspNetCore.Diagnostics.HealthChecks** (paquete de comunidad): Over-engineering para nuestro caso.
- **Custom `/health` endpoint**: Reinventar la rueda.

**Justificación**: ASP.NET Core ya tiene health checks integrados. Se registran con `services.AddHealthChecks()` y se mapean con `app.MapHealthChecks()`. Cada componente (parser, AI stub, PDF generator) tiene un `IHealthCheck` custom que verifica su estado.

---

## D05 — ¿Cómo proteger la privacidad en observability? (Constitution Art. III)

**Decisión**: 
1. **Logs**: Nunca loguear CV content, job description, o PII. Solo metadatos: `cvLength`, `jobLength`, `model`, `traceId`.
2. **Traces**: Span attributes solo incluyen `http.method`, `http.url`, `http.status_code`, `duration.ms`. Nunca body content.
3. **Metrics**: Labels solo incluyen `endpoint`, `method`, `status`. Nunca contenido del request.

**Implementación**: 
- Request logging middleware extrae metadatos del request, NO el body.
- OpenTelemetry instrumentation configura `Enrich` para agregar solo metadatos seguros.
- Prometheus metrics usan solo labels estándar HTTP.

---

## D06 — ¿Impacto en performance?

**Decisión**: Logging overhead <1ms, metrics overhead <10ms, tracing overhead <2ms.

**Evidencia**: 
- `AddJsonConsole()` en ASP.NET Core es asíncrono y bufferizado.
- `prometheus-net` usa contadores atómicos, sin locking.
- OpenTelemetry SDK usa `ActivitySource` que es no-op cuando no hay listener.

**Riesgo**: Bajo. Estas son librerías battle-tested en producción por miles de empresas.
