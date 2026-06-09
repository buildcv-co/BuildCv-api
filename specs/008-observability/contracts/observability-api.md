# Contracts: 008-observability

> **Source of truth** para los endpoints de observability. No hay endpoints de negocio nuevos.

## HTTP Contract

### `GET /metrics`

Returns Prometheus metrics in text exposition format.

```http
GET /metrics HTTP/1.1
Host: localhost:5080
```

**Response (200 OK)**:
```
# HELP http_requests_total Total HTTP requests
# TYPE http_requests_total counter
http_requests_total{endpoint="/api/v1/score",method="POST",status="200"} 1
# HELP http_request_duration_ms HTTP request duration in milliseconds
# TYPE http_request_duration_ms histogram
http_request_duration_ms_bucket{endpoint="/api/v1/score",method="POST",le="100"} 1
http_request_duration_ms_bucket{endpoint="/api/v1/score",method="POST",le="+Inf"} 1
http_request_duration_ms_sum{endpoint="/api/v1/score",method="POST"} 150
http_request_duration_ms_count{endpoint="/api/v1/score",method="POST"} 1
```

**Content-Type**: `text/plain; version=0.0.4; charset=utf-8`

**Labels**:
- `endpoint`: request path (e.g., `/api/v1/score`)
- `method`: HTTP method (e.g., `POST`)
- `status`: HTTP status code (e.g., `200`)

**Privacy (Art. III)**: Labels NEVER include CV content, job description, or PII.

---

### `GET /health/ready`

Returns granular health status of all components.

```http
GET /health/ready HTTP/1.1
Host: localhost:5080
```

**Response (200 OK — Healthy)**:
```json
{
  "status": "Healthy",
  "totalDuration": 6,
  "results": {
    "parser": {
      "status": "Healthy",
      "duration": 2,
      "description": "PDF/DOCX parser is available"
    },
    "ai-client": {
      "status": "Healthy",
      "duration": 1,
      "description": "AI client is available"
    },
    "pdf-generator": {
      "status": "Healthy",
      "duration": 3,
      "description": "PDF generator is available"
    }
  }
}
```

**Response (503 Service Unhealthy)**:
```json
{
  "status": "Unhealthy",
  "totalDuration": 5002,
  "results": {
    "parser": {
      "status": "Unhealthy",
      "duration": 5001,
      "description": "PDF parser failed",
      "exception": "ParserEngineException: ..."
    }
  }
}
```

**Component statuses**:
- `Healthy`: component responds within 5s
- `Degraded`: component responds but slowly (>1s)
- `Unhealthy`: component fails or times out (>5s)

---

## Structured Log Format (JSON Console)

Every HTTP request produces two log entries:

**Request start**:
```json
{
  "timestamp": "2026-06-09T12:00:00.000Z",
  "level": "Information",
  "message": "Request started",
  "category": "BuildCv.Api.Middleware.RequestLoggingMiddleware",
  "requestId": "0HM...",
  "traceId": "0HM...",
  "method": "POST",
  "path": "/api/v1/score"
}
```

**Request completion**:
```json
{
  "timestamp": "2026-06-09T12:00:00.150Z",
  "level": "Information",
  "message": "Request completed",
  "category": "BuildCv.Api.Middleware.RequestLoggingMiddleware",
  "requestId": "0HM...",
  "traceId": "0HM...",
  "method": "POST",
  "path": "/api/v1/score",
  "statusCode": 200,
  "durationMs": 150
}
```

**Privacy (Art. III)**: Logs NEVER include CV content, job description, or PII. Only metadata.
