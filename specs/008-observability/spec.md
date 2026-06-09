# Feature Specification: 008-observability

**Feature Branch**: `008-observability`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "Observability: structured logging, Prometheus metrics, OpenTelemetry tracing, health checks"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Structured Logging (Priority: P1)

Como operador del sistema, necesito logs estructurados (JSON) con contexto consistente (traceId, requestId, durations) para poder diagnosticar problemas en producción sin inspeccionar el código fuente.

**Why this priority**: Logging es la base de toda observabilidad. Sin logs estructurados, no hay forma confiable de diagnosticar issues en producción. Es el prerequisite para métricas y tracing.

**Independent Test**: Puedo verificar que cada endpoint produce logs JSON con `traceId`, `requestId`, `durationMs`, y que los logs de error incluyen `exceptionType` y `errorCode`. Test unitario: `StructuredLoggingTests.Logs_Contain_Required_Fields`.

**Acceptance Scenarios**:

1. **Given** un request a cualquier endpoint, **When** el request completa, **Then** se emite un log JSON con `level`, `timestamp`, `message`, `traceId`, `requestId`, `durationMs`, `statusCode`.
2. **Given** un request que falla con 4xx/5xx, **When** se emite el log de error, **Then** incluye `exceptionType`, `errorCode`, `stackTrace` (solo en Development).
3. **Given** un request con body que contiene datos sensibles (CV text, job description), **When** se emite el log, **Then** NUNCA se incluye el contenido del body (solo metadatos: longitudes, conteos, modelo).

---

### User Story 2 - Prometheus Metrics (Priority: P2)

Como operador del sistema, necesito métricas en formato Prometheus para poder construir dashboards y alertas en Grafana/Prometheus sin código custom.

**Why this priority**: Las métricas permiten monitorear tendencias, detectar anomalías y configurar alertas. Son esenciales para SLA y capacity planning. Prioridad P2 porque depende de logging estructurado (P1).

**Independent Test**: Puedo hacer `GET /metrics` y ver métricas estándar de ASP.NET Core (requests, duration, errors) más métricas custom del dominio (score latency, adapt latency, import file types). Test: `PrometheusMetricsTests.Exposes_Standard_And_Custom_Metrics`.

**Acceptance Scenarios**:

1. **Given** el sistema ejecutándose, **When** hago `GET /metrics`, **Then** recibo respuesta con `Content-Type: text/plain; version=0.0.4; charset=utf-8` y métricas en formato Prometheus text exposition.
2. **Given** requests a `/api/v1/score`, **When** acumulo 10 requests, **Then** las métricas muestran `http_requests_total{endpoint="/api/v1/score",method="POST",status="200"}` incrementada en 10.
3. **Given** un request a `/api/v1/score`, **When** completa en 150ms, **Then** la métrica `http_request_duration_ms{endpoint="/api/v1/score"}` refleja la latencia.
4. **Given** el constitución Art. III (privacidad), **When** se emiten métricas, **Then** NUNCA se incluyen labels con contenido de CV o job description.

---

### User Story 3 - OpenTelemetry Tracing (Priority: P3)

Como operador del sistema, necesito distributed tracing con OpenTelemetry para poder seguir un request desde el frontend (BFF) a través del backend y identificar cuellos de botella.

**Why this priority**: El tracing es importante para debugging en arquitecturas BFF+backend, pero es P3 porque en v0.5 el BFF y backend están en la misma máquina (localhost). Se vuelve crítico cuando se despliegan separadamente.

**Independent Test**: Puedo hacer un request a `/api/v1/score` y ver que se crea un span con `http.method`, `http.url`, `http.status_code`, `duration.ms`. Test: `OpenTelemetryTests.Creates_Span_With_Required_Attributes`.

**Acceptance Scenarios**:

1. **Given** el sistema con OTLP exporter configurado, **When** hago un request a `/api/v1/score`, **Then** se crea un span con nombre `POST /api/v1/score` y atributos standard (HTTP semantic conventions).
2. **Given** un request que llama a `/api/v1/score` → `ScoreCvHandler` → `ScoringEngine`, **When** el request completa, **Then** el trace tiene 3 spans anidados con duración correcta.
3. **Given** el constitución Art. III (privacidad), **When** se crean spans, **Then** los atributos NUNCA incluyen contenido de CV o job description.

---

### User Story 4 - Enhanced Health Checks (Priority: P3)

Como operador del sistema, necesito health checks detallados que reporten el estado de componentes individuales (parser, AI stub, PDF generator) para detectar degradación antes de que afecte usuarios.

**Why this priority**: Health checks básicos ya existen (`/health/ready`). Esta historia agrega granularidad por componente. Es P3 porque los health checks básicos ya cubren el caso más crítico (el sistema responde).

**Independent Test**: Puedo hacer `GET /health/ready` y ver el estado de cada componente (Healthy/Degraded/Unhealthy) con tiempos de respuesta. Test: `HealthCheckTests.Component_Health_Checks_Return_Individual_Status`.

**Acceptance Scenarios**:

1. **Given** el sistema funcionando correctamente, **When** hago `GET /health/ready`, **Then** la respuesta incluye `status: "Healthy"` y cada componente reporta `Healthy` con su tiempo de respuesta en ms.
2. **Given** un componente degradado (ej: parser lento), **When** hago `GET /health/ready`, **Then** la respuesta incluye `status: "Degraded"` y el componente degradado reporta `Degraded` con el tiempo de respuesta.
3. **Given** un componente caído, **When** hago `GET /health/ready`, **Then** la respuesta incluye `status: "Unhealthy"` y el componente caído reporta `Unhealthy` con el error.

---

### Edge Cases

- ¿Qué pasa cuando Prometheus scrapea `/metrics` mientras el sistema está bajo alta carga? → El endpoint de métricas debe ser rápido (<10ms) y no bloquear requests normales.
- ¿Qué pasa cuando OTLP exporter no está configurado? → El tracing debe ser un no-op (no falla, solo no emite spans).
- ¿Qué pasa cuando el log buffer está lleno? → Los logs se descartan con un warning, no bloquean el request.
- ¿Qué pasa cuando un componente de health check tarda más de 5s? → Se marca como `Degraded` con timeout explícito.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST emit structured logs in JSON format for every HTTP request (start + completion).
- **FR-002**: System MUST include `traceId`, `requestId`, `durationMs`, `statusCode` in every request log.
- **FR-003**: System MUST NEVER log CV content, job description content, or any PII (Constitution Art. III).
- **FR-004**: System MUST expose Prometheus metrics at `GET /metrics` with standard HTTP metrics + custom domain metrics.
- **FR-005**: System MUST create OpenTelemetry spans for each HTTP request with standard HTTP semantic conventions.
- **FR-006**: System MUST provide granular health checks at `GET /health/ready` with per-component status.
- **FR-007**: System MUST support OTLP export for traces (configurable via environment variables).
- **FR-008**: System MUST maintain zero new external dependencies beyond what's already in the project (OpenTelemetry and Prometheus libraries are NuGet packages).
- **FR-009**: System MUST keep existing `/health` and `/health/ready` endpoints working (backward compatible).
- **FR-010**: System MUST NOT expose health check details (component names, response times) to unauthenticated users in production.

### Key Entities

- **StructuredLogEntry**: JSON log record with fields: level, timestamp, message, traceId, requestId, durationMs, statusCode, exceptionType (optional), errorCode (optional).
- **MetricEntry**: Prometheus metric with name, labels (endpoint, method, status), and value (counter or histogram).
- **SpanEntry**: OpenTelemetry span with name, attributes (http.method, http.url, http.status_code, duration.ms), and parent span ID.
- **ComponentHealth**: Health status of individual component: name, status (Healthy/Degraded/Unhealthy), durationMs, exception (optional).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every HTTP request produces a structured log entry with all required fields within 1ms overhead.
- **SC-002**: `GET /metrics` responds in <10ms with correct Prometheus format.
- **SC-003**: OpenTelemetry spans capture 100% of HTTP requests with correct attributes.
- **SC-004**: `GET /health/ready` reports per-component status within 5s total timeout.
- **SC-005**: Zero PII in logs, metrics, or traces (verified by constitution-check script).
- **SC-006**: All existing tests continue passing (189 backend + constitution tests).

## Assumptions

- Prometheus/Grafana stack is available for metric visualization (not part of this feature scope).
- OTLP collector is available for trace ingestion (not part of this feature scope).
- The existing `/health` and `/health/ready` endpoints are enhanced, not replaced.
- OpenTelemetry SDK and Prometheus exporter NuGet packages are acceptable dependencies.
- v0.5.1 is a minor observability patch; no new endpoints or domain changes.
