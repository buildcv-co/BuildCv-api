# Quickstart: 005-cv-pdf-docx-import

**Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Pre-requisitos

- .NET 10 SDK
- Spec-kit + gentle-ai corriendo
- M0, M1 y M2 ya implementados (score, adapt, export)

## Setup local

```bash
cd ~/Dev/portfolio/buildCV/BuildCv-api
dotnet restore
dotnet build
```

> Las nuevas dependencias `UglyToad.PdfPig` y `DocumentFormat.OpenXml` se añaden en `BuildCv.Infrastructure.csproj` durante la tarea T0.1.

## Tests TDD (rojo → verde → refactor)

```bash
# 1. Tests rojos PRIMERO (deben fallar al inicio)
dotnet test --filter "FullyQualifiedName~Import"
# Expected: 100% fail (la feature no existe aún)

# 2. Implementar lo mínimo para verde
# ... escribir Domain/Import/ + Application/Features/Import/ + Infrastructure/Parsing/ + Api/Endpoints/ImportEndpoints.cs ...

# 3. Re-ejecutar tests
dotnet test --filter "FullyQualifiedName~Import"
# Expected: 100% pass

# 4. Refactor + verificación final
dotnet test
dotnet build BuildCv.slnx -c Release    # 0 warnings
dotnet format --verify-no-changes       # limpio
```

## Test end-to-end manual con curl

### 1. Arrancar dev environment

```bash
cd ~/Dev/portfolio/buildCV
# Terminal 1: backend
dotnet run --project BuildCv-api/src/BuildCv.Api
# → http://localhost:5080

# Terminal 2: frontend
cd BuildCv-web
pnpm dev
# → http://localhost:3000
```

### 2. Health check

```bash
curl http://localhost:5080/health/ready
# Expected: 200 OK con {"status":"Healthy",...}
```

### 3. Importar PDF (happy path)

```bash
# Asume que tienes un PDF de 2 páginas en ~/samples/cv.pdf
curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@~/samples/cv.pdf" \
  -H "Accept: application/json" \
  | jq .
# Expected: HTTP 200 con body:
# {
#   "text": "Juan Pérez\nBackend Developer con 5 años...",
#   "sections": [
#     { "heading": "Experiencia", "start": 245, "end": 612, "confidence": "High" },
#     { "heading": "Educación", "start": 614, "end": 780, "confidence": "High" }
#   ],
#   "warnings": [],
#   "engineVersion": "1.0.0",
#   "traceId": "0HMVD9F2E5Q2P:00000001"
# }
```

### 4. Importar DOCX (happy path)

```bash
curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@~/samples/cv.docx" \
  -H "Accept: application/json" \
  | jq .
# Expected: HTTP 200 con shape idéntico al PDF
```

### 5. Verificar el flujo web (BFF + UI)

```bash
# Abrir http://localhost:3000/importar (cuando exista la página en 005-web)
# Arrastrar un PDF al componente FileUpload
# Ver el ImportResultPanel con text + sections + warnings
# Click "Usar este texto en el editor" → navega a /editor (006)
```

## Tests de error (cURL)

### 6. Test 415 — MIME no soportado

```bash
# Crear un .txt y subirlo
echo "hola mundo" > /tmp/not-a-cv.txt

curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@/tmp/not-a-cv.txt" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n" | tail -10
# Expected: HTTP 415
# Body:
# {
#   "type": "...",
#   "title": "Tipo de archivo no soportado",
#   "status": 415,
#   "detail": "Tipo de archivo no soportado. Sube un PDF o DOCX.",
#   "code": "IMPORT_UNSUPPORTED_MEDIA"
# }
```

### 7. Test 413 — Archivo demasiado grande (>5 MB)

```bash
# Generar un PDF de 6 MB con texto random (requiere un script o un PDF existente)
dd if=/dev/urandom of=/tmp/big.pdf bs=1M count=6 2>/dev/null
echo "%PDF-1.4" > /tmp/big.pdf   # magic bytes

curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@/tmp/big.pdf" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n" | tail -10
# Expected: HTTP 413
# Body:
# {
#   "type": "...",
#   "title": "Archivo demasiado grande",
#   "status": 413,
#   "detail": "El archivo supera el límite de 5 MB.",
#   "code": "IMPORT_TOO_LARGE"
# }
```

### 8. Test 422 — PDF cifrado

```bash
# Necesitas un PDF con contraseña (puedes crear uno con qpdf o similar)
# Asume ~/samples/cv-encrypted.pdf

curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@~/samples/cv-encrypted.pdf" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n" | tail -10
# Expected: HTTP 422
# Body:
# {
#   "type": "...",
#   "title": "PDF protegido",
#   "status": 422,
#   "detail": "Este PDF está protegido con contraseña. Quítale la contraseña y vuelve a subirlo.",
#   "code": "IMPORT_PDF_ENCRYPTED"
# }
```

### 9. Test 422 — PDF escaneado (sin texto)

```bash
# Un PDF con solo imágenes, sin capa de texto
# Asume ~/samples/cv-scanned.pdf

curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@~/samples/cv-scanned.pdf" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n" | tail -10
# Expected: HTTP 422
# Body:
# {
#   "type": "...",
#   "title": "PDF escaneado",
#   "status": 422,
#   "detail": "Este PDF parece un escaneo. No podemos extraer texto. Pega el contenido manualmente o usa un PDF con texto seleccionable.",
#   "code": "IMPORT_SCANNED_PDF"
# }
```

### 10. Test 422 — DOCX protegido con contraseña

```bash
# Asume ~/samples/cv-protected.docx

curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@~/samples/cv-protected.docx" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n" | tail -10
# Expected: HTTP 422
# Body:
# {
#   "type": "...",
#   "title": "DOCX protegido",
#   "status": 422,
#   "detail": "Este archivo de Word está protegido. Quítale la contraseña y vuelve a subirlo.",
#   "code": "IMPORT_DOCX_PROTECTED"
# }
```

### 11. Test 429 — Rate-limit "import" (30/h por IP)

```bash
# Hacer 31 requests de un PDF pequeño en menos de 1h
for i in {1..31}; do
  HTTP=$(curl -sS -o /tmp/import-resp.json -w "%{http_code}" -X POST http://localhost:5080/api/v1/import \
    -F "file=@~/samples/cv-tiny.pdf")
  echo "Req $i: HTTP $HTTP"
done
# Expected: req 1-30 → 200, req 31 → 429 con Retry-After
```

### 12. Test 503 — Engine error (parser lanza excepción)

```bash
# Forzar un PDF malformado (bytes random con magic header)
dd if=/dev/urandom of=/tmp/corrupt.pdf bs=1024 count=100 2>/dev/null
echo "%PDF-1.4" > /tmp/corrupt.pdf

curl -X POST http://localhost:5080/api/v1/import \
  -F "file=@/tmp/corrupt.pdf" \
  -H "Accept: application/json" \
  -w "\nHTTP %{http_code}\n" | tail -10
# Expected: HTTP 503 (o 422 con IMPORT_PDF_INVALID si la excepción es mapeable)
# Body: ProblemDetails con código IMPORT_ENGINE_ERROR
```

## Verificación de logs (privacidad, NFR-002a)

```bash
# Después de hacer varios imports, inspeccionar los logs
cd ~/Dev/portfolio/buildCV
tail -100 BuildCv-api/src/BuildCv.Api/logs/*.log 2>/dev/null || journalctl -u buildcv-api -n 100

# Expected: las líneas deben tener solo metadatos, no texto del CV:
#   ✓ "Import request (fileSize=12345, mimeDeclared=application/pdf, mimeDetected=application/pdf,
#       parseTimeMs=342, sections=2, warnings=0, traceId=...)"
#   ✗ NUNCA "CV text: Juan Pérez Backend Developer..."
```

## Verificación pre-merge

```bash
# 1. Pre-flight (build + format + tests + coverage)
cd BuildCv-api
./scripts/preflight.sh
# Expected: all green, exit 0

# 2. Constitution check
./scripts/constitution-check.sh
# Expected: 19/19 passes (o más si se añadieron checks), 0 critical

# 3. Verificar pureza del Domain
dotnet list src/BuildCv.Domain package references
# Expected: 0 paquetes externos
```

## Crear PR

```bash
gh pr create \
  --title "feat(005-cv-pdf-docx-import): import CV desde PDF/DOCX" \
  --body "Implements FR-039..FR-039i, NFR-001a, NFR-002a, NFR-005a, NFR-007a, NFR-009a, NFR-013a, NFR-018a, NFR-019a, NFR-022a. Cite Constitution Art. III, V, VI, VII, VIII, IX."
```

## Troubleshooting

- **`PdfDocumentEncryptedException` no se mapea a 422**: verificar que el catch en `PdfPigCvParser.Parse` envuelva TODA la iteración de páginas, no solo `Open`. PdfPig valida la contraseña al abrir.
- **PDF escaneado devuelve texto vacío sin lanzar excepción**: el chequeo `textLengthAcrossPages == 0` debe ir **después** de iterar todas las páginas. Si se hace antes, se confunde con un PDF de 0 páginas.
- **DOCX con password no se detecta**: `OpenXmlPackageException` puede tener la password protection dentro de `DocumentProtection`. Verificar que el catch filtre por la presencia de `DocumentProtection` con `Enforcement = true`.
- **Rate-limit consume cupo en errores 422**: la política `RateLimiting.cs` cuenta **todas** las requests (incluso las que retornan 4xx). Esto es intencional para defender CPU; documentar en `plan.md` si el equipo decide lo contrario.
- **Heurística de secciones marca falsos positivos** (palabra "Skills" en un párrafo): revisar `confidence: Low` y considerar agregar `SECTION_AMBIGUOUS` al warning en v0.5.1.
- **MemoryStream no se libera**: usar `using` siempre; PdfPig tiene una sobrecarga que acepta `Stream`, también usar `using`.
- **Encoding del texto extraído muestra caracteres raros**: PdfPig y OpenXml devuelven `string` .NET (UTF-16). El JSON se serializa con UTF-8 por defecto en `System.Text.Json`. Verificar con `Encoding.UTF8.GetBytes(text).Length` que el JSON no se rompa en caracteres como "ñ" o "á".

## Tareas OpenSpec

Las tareas TDD-ordered están en `tasks.md`. Cada task es independiente y testeable.

## Handoff a features downstream

- **006-cv-editor**: el editor consume `ImportResult` (Zod-validado en cliente) y lo usa como semilla del textarea de edición. El usuario puede editar el texto extraído antes de mandarlo al score (002) o adapt (003).
- **002-score-engine**: sin cambios. El score opera sobre el texto que el editor le pase.
- **003-adapt-ia**: sin cambios. La adaptación opera sobre el texto del editor.

## Métricas a observar en producción (post-launch)

- Distribución de MIMEs (PDF vs DOCX).
- Distribución de tamaños de archivo.
- Tasa de errores 415/422 (cifrado, escaneado, MIME inválido).
- Tiempo de parseo P50, P95, P99.
- Tasa de truncado por >50k chars.
- Tasa de secciones detectadas vs CVs sin secciones detectables.
