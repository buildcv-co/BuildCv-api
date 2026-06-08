# Contracts: 005-cv-pdf-docx-import

> **Source of truth** para la implementación de `POST /api/v1/import` en el backend y para el BFF en `BuildCv-web`. Cualquier cambio en este archivo requiere actualizar `data-model.md` y los tipos TypeScript del frontend.

## HTTP Contract

### `POST /api/v1/import`

```http
POST /api/v1/import HTTP/1.1
Host: api.buildcv.app
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW
```

#### Form fields

| Nombre | Tipo | Required | Descripción |
|---|---|---|---|
| `file` | `binary` | Sí | Archivo PDF o DOCX. Tamaño máximo: 5 MB (5_242_880 bytes). |

#### Headers de respuesta

| Header | Valor | Descripción |
|---|---|---|
| `Content-Type` | `application/json` | Siempre JSON, incluso en errores (ProblemDetails). |
| `X-RateLimit-Limit` | `30` | Límite de la política `"import"` (informativo). |
| `X-RateLimit-Remaining` | `int` | Requests restantes en la ventana actual. |
| `X-RateLimit-Reset` | `ISO 8601 timestamp` | Cuándo se resetea el límite. |
| `Retry-After` | `int (seconds)` | Solo en 429. |

#### Response 200 OK

```http
HTTP/1.1 200 OK
Content-Type: application/json
X-RateLimit-Limit: 30
X-RateLimit-Remaining: 29
X-RateLimit-Reset: 2026-06-09T18:00:00Z
```

```json
{
  "text": "Juan Pérez\nBackend Developer con 5 años de experiencia en C# y .NET.\n\nEXPERIENCIA\n\nAcme Corp · Senior Developer · 2022-2026\n- Lideré migración de monolito a microservicios\n- Reduje latencia P95 en 40%\n\nEDUCACIÓN\n\nUniversidad Nacional · Ingeniería de Sistemas · 2014-2019",
  "sections": [
    {
      "heading": "EXPERIENCIA",
      "start": 76,
      "end": 245,
      "confidence": "High"
    },
    {
      "heading": "EDUCACIÓN",
      "start": 247,
      "end": 320,
      "confidence": "High"
    }
  ],
  "warnings": [
    {
      "code": "IMAGE_OMITTED",
      "message": "Se omitieron 1 imagen(es).",
      "severity": "Info"
    }
  ],
  "engineVersion": "1.0.0",
  "traceId": "0HMVD9F2E5Q2P:00000001"
}
```

**Descripción de campos** (ver `data-model.md` para definiciones C# / TS exactas):

| Campo | Tipo | Descripción |
|---|---|---|
| `text` | `string` | Texto extraído, normalizado a UTF-8 NFC, max 50.000 chars (truncado con warning si excede). |
| `sections[]` | `Section[]` | Secciones detectadas por regex sobre headers en MAYÚSCULAS. |
| `sections[].heading` | `string` | Header detectado (e.g. `"EXPERIENCIA"`). |
| `sections[].start` | `int` | Índice en `text` donde empieza la sección. |
| `sections[].end` | `int` | Índice en `text` donde termina la sección. |
| `sections[].confidence` | `"High"\|"Low"` | `High` si la línea solo tiene el header; `Low` si hay puntuación o más palabras. |
| `warnings[]` | `Warning[]` | Avisos no bloqueantes sobre el parseo. |
| `warnings[].code` | `string` | Código legible: `IMAGE_OMITTED`, `TEXT_TRUNCATED`, `NO_SECTIONS_DETECTED`, `ENCODING_NORMALIZED`, `SECTION_AMBIGUOUS`. |
| `warnings[].message` | `string` | Mensaje en español para mostrar al usuario. |
| `warnings[].severity` | `"Info"\|"Warning"\|"Error"` | `Info` no bloquea, `Warning` sugiere revisión, `Error` indica problema serio. |
| `engineVersion` | `string` | Versión del parser (SemVer). Se sella para reproducibilidad. |
| `traceId` | `string` | Identificador de traza (Activity.Id de ASP.NET). Útil para soporte. |

#### Response 400 Bad Request (validation)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "File": ["The file is required."]
  }
}
```

#### Response 413 Payload Too Large

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.14",
  "title": "Archivo demasiado grande",
  "status": 413,
  "detail": "El archivo supera el límite de 5 MB.",
  "instance": "/api/v1/import",
  "code": "IMPORT_TOO_LARGE",
  "sizeBytes": 6291456,
  "maxBytes": 5242880,
  "traceId": "0HMVD9F2E5Q2P:00000002"
}
```

#### Response 415 Unsupported Media Type

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.16",
  "title": "Tipo de archivo no soportado",
  "status": 415,
  "detail": "Tipo de archivo no soportado. Sube un PDF o DOCX.",
  "instance": "/api/v1/import",
  "code": "IMPORT_UNSUPPORTED_MEDIA",
  "mimeDeclared": "text/plain",
  "mimeDetected": "text/plain",
  "traceId": "0HMVD9F2E5Q2P:00000003"
}
```

#### Response 422 Unprocessable Entity

PDF cifrado:
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "PDF protegido",
  "status": 422,
  "detail": "Este PDF está protegido con contraseña. Quítale la contraseña y vuelve a subirlo.",
  "instance": "/api/v1/import",
  "code": "IMPORT_PDF_ENCRYPTED",
  "traceId": "0HMVD9F2E5Q2P:00000004"
}
```

PDF escaneado:
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "PDF escaneado",
  "status": 422,
  "detail": "Este PDF parece un escaneo. No podemos extraer texto. Pega el contenido manualmente o usa un PDF con texto seleccionable.",
  "instance": "/api/v1/import",
  "code": "IMPORT_SCANNED_PDF",
  "traceId": "0HMVD9F2E5Q2P:00000005"
}
```

DOCX protegido:
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "DOCX protegido",
  "status": 422,
  "detail": "Este archivo de Word está protegido. Quítale la contraseña y vuelve a subirlo.",
  "instance": "/api/v1/import",
  "code": "IMPORT_DOCX_PROTECTED",
  "traceId": "0HMVD9F2E5Q2P:00000006"
}
```

DOCX sin texto:
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "DOCX sin texto",
  "status": 422,
  "detail": "Este archivo de Word no contiene texto extraíble.",
  "instance": "/api/v1/import",
  "code": "IMPORT_DOCX_NO_TEXT",
  "traceId": "0HMVD9F2E5Q2P:00000007"
}
```

Demasiadas páginas:
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Demasiadas páginas",
  "status": 422,
  "detail": "El documento tiene 250 páginas (máx. 100).",
  "instance": "/api/v1/import",
  "code": "IMPORT_TOO_MANY_PAGES",
  "pageCount": 250,
  "maxPages": 100,
  "traceId": "0HMVD9F2E5Q2P:00000008"
}
```

Archivo vacío:
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Archivo vacío",
  "status": 422,
  "detail": "El archivo está vacío.",
  "instance": "/api/v1/import",
  "code": "IMPORT_EMPTY_FILE",
  "traceId": "0HMVD9F2E5Q2P:00000009"
}
```

#### Response 429 Too Many Requests

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Has alcanzado el tope de importaciones (30/hora). El análisis determinista y la adaptación siguen disponibles.",
  "instance": "/api/v1/import",
  "code": "IMPORT_RATE_LIMIT_EXCEEDED",
  "retryAfter": "2026-06-09T18:30:00Z",
  "limit": 30,
  "window": "1 hour",
  "traceId": "0HMVD9F2E5Q2P:00000010"
}
```

Headers:
```
Retry-After: 1800
X-RateLimit-Limit: 30
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 2026-06-09T18:30:00Z
```

#### Response 503 Service Unavailable

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Motor de import no disponible",
  "status": 503,
  "detail": "El servicio de import no está disponible temporalmente. Intenta de nuevo en unos minutos.",
  "instance": "/api/v1/import",
  "code": "IMPORT_ENGINE_ERROR",
  "traceId": "0HMVD9F2E5Q2P:00000011"
}
```

## Códigos de error (catálogo completo)

| Código | HTTP | Severidad | Cuándo ocurre |
|---|---|---|---|
| `IMPORT_TOO_LARGE` | 413 | Error | Archivo > 5 MB. |
| `IMPORT_UNSUPPORTED_MEDIA` | 415 | Error | MIME declarado o magic bytes no coinciden con PDF/DOCX. |
| `IMPORT_PDF_ENCRYPTED` | 422 | Error | PDF protegido con contraseña. |
| `IMPORT_SCANNED_PDF` | 422 | Error | PDF sin texto extraíble (escaneado o basado en imágenes). |
| `IMPORT_DOCX_PROTECTED` | 422 | Error | DOCX con `DocumentProtection`. |
| `IMPORT_DOCX_NO_TEXT` | 422 | Error | DOCX sin texto (solo imágenes o vacío). |
| `IMPORT_TOO_MANY_PAGES` | 422 | Error | PDF con > 100 páginas. |
| `IMPORT_EMPTY_FILE` | 422 | Error | Archivo de 0 bytes. |
| `IMPORT_RATE_LIMIT_EXCEEDED` | 429 | Advertencia | Rate-limit `"import"` 30/h excedido. |
| `IMPORT_ENGINE_ERROR` | 503 | Error | PdfPig/OpenXml lanzaron excepción no esperada. |

## Códigos de warning (no bloqueantes)

| Código | Severidad | Cuándo ocurre |
|---|---|---|
| `IMAGE_OMITTED` | Info | DOCX con N imágenes (se omitieron todas). |
| `TEXT_TRUNCATED` | Warning | Texto extraído > 50.000 chars. |
| `NO_SECTIONS_DETECTED` | Info | Heurística no encontró headers en MAYÚSCULAS. |
| `SECTION_AMBIGUOUS` | Warning | Header detectado como `Low confidence` (palabra rodeada de puntuación o subcadena). |
| `ENCODING_NORMALIZED` | Info | Texto fue normalizado de Latin-1 u otro encoding a UTF-8. |

## Configuration Contract

```json
{
  "Import": {
    "MaxFileSizeBytes": 5242880,
    "MaxPdfPages": 100,
    "MaxTextLength": 50000,
    "RateLimit": {
      "PermitLimit": 30,
      "Window": "01:00:00"
    }
  }
}
```

Opciones validadas al arranque con `ValidateDataAnnotations().ValidateOnStart()`.

## Logging Contract (Serilog structured)

```csharp
// ✓ Allowed
Log.Information("Import request (fileSize={FileSize}, mimeDeclared={MimeDeclared}, mimeDetected={MimeDetected}, parseTimeMs={ParseMs}, sections={SectionCount}, warnings={WarningCount}, engineVersion={EngineVersion}, traceId={TraceId})",
    fileBytes.Length, mimeDeclared, mimeDetected, stopwatch.ElapsedMilliseconds, sections.Count, warnings.Count, engineVersion, traceId);

Log.Warning("Import rate limit exceeded (ip={Ip}, limit={Limit}, traceId={TraceId})",
    ip, limit, traceId);

// ✗ Prohibited (Constitution Art. III, NFR-002a)
Log.Information("CV text: {Text}", importResult.Text);  // NUNCA contenido
Log.Information("File bytes: {Bytes}", fileBytes);      // NUNCA bytes del archivo
Log.Information("User uploaded {FileName}", fileName);  // NUNCA nombre del archivo (PII)
```

## Ejemplo cURL (happy path)

```bash
# Subir un PDF de 2 páginas
curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@./samples/cv-2pages.pdf" \
  -H "Accept: application/json" \
  | jq .
```

Output esperado:
```json
{
  "text": "Juan Pérez\nBackend Developer con 5 años de experiencia...",
  "sections": [
    { "heading": "EXPERIENCIA", "start": 76, "end": 245, "confidence": "High" },
    { "heading": "EDUCACIÓN", "start": 247, "end": 320, "confidence": "High" }
  ],
  "warnings": [],
  "engineVersion": "1.0.0",
  "traceId": "0HMVD9F2E5Q2P:00000001"
}
```

## Ejemplo cURL (error 415)

```bash
curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@./samples/not-a-cv.txt" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n"
```

Output esperado:
```
{"type":"...","title":"Tipo de archivo no soportado","status":415,"detail":"Tipo de archivo no soportado. Sube un PDF o DOCX.","code":"IMPORT_UNSUPPORTED_MEDIA","mimeDeclared":"text/plain","traceId":"..."}
HTTP 415
```

## OpenAPI 3.1 (extracto YAML)

```yaml
openapi: 3.1.0
info:
  title: BuildCv API
  version: 1.0.0
  description: API de BuildCv — análisis de CV con IA (cero invención).
paths:
  /api/v1/import:
    post:
      operationId: importCv
      summary: Importa un CV desde PDF o DOCX.
      description: |
        Recibe un archivo PDF o DOCX vía multipart/form-data, valida MIME
        y magic bytes, extrae texto y secciones heurísticas, y devuelve
        un ImportResult JSON. Rate-limited 30/h por IP.
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              required: [file]
              properties:
                file:
                  type: string
                  format: binary
                  description: Archivo PDF o DOCX, máximo 5 MB.
      responses:
        '200':
          description: Import exitoso.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ImportResult'
        '400':
          description: Validación fallida.
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ValidationProblemDetails'
        '413':
          description: Archivo demasiado grande.
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ProblemDetails'
        '415':
          description: Tipo de archivo no soportado.
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ProblemDetails'
        '422':
          description: Archivo no procesable (cifrado, escaneado, protegido, etc.).
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ProblemDetails'
        '429':
          description: Rate-limit excedido.
          headers:
            Retry-After:
              schema:
                type: integer
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ProblemDetails'
        '503':
          description: Motor de import no disponible.
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ProblemDetails'
components:
  schemas:
    ImportResult:
      type: object
      required: [text, sections, warnings, engineVersion, traceId]
      properties:
        text:
          type: string
          maxLength: 50000
          description: Texto extraído, normalizado a UTF-8 NFC.
        sections:
          type: array
          maxItems: 50
          items:
            $ref: '#/components/schemas/DetectedSection'
        warnings:
          type: array
          maxItems: 20
          items:
            $ref: '#/components/schemas/ImportWarning'
        engineVersion:
          type: string
          pattern: '^\d+\.\d+\.\d+$'
          description: Versión del parser (SemVer).
        traceId:
          type: string
          minLength: 1
          maxLength: 100
    DetectedSection:
      type: object
      required: [heading, start, end, confidence]
      properties:
        heading:
          type: string
          minLength: 1
          maxLength: 100
        start:
          type: integer
          minimum: 0
        end:
          type: integer
          minimum: 0
        confidence:
          type: string
          enum: [High, Low]
    ImportWarning:
      type: object
      required: [code, message, severity]
      properties:
        code:
          type: string
          minLength: 1
          maxLength: 50
        message:
          type: string
          minLength: 1
          maxLength: 500
        severity:
          type: string
          enum: [Info, Warning, Error]
```

## Versionado

- `engineVersion` se incrementa con cada cambio en la heurística o en el formato del parser (SemVer: MAJOR para breaking, MINOR para nueva sección detectable, PATCH para fixes de bugs).
- `traceId` es por request, no cambia entre versiones.
- La estructura JSON del `ImportResult` es estable dentro de un MAJOR.
