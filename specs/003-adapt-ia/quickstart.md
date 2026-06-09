# Quickstart: 003-adapt-ia

**Date**: 2026-06-08 | **Status**: SHIPPED (commit `68baaf2`)

> **Reality check:** La implementación shipped NO usa Anthropic SDK, NO requiere API key, NO usa SSE. El "proveedor de IA" es un `StubAiClient` determinista (sin red, sin LLM real). El endpoint es **sincrónico** (`POST /api/v1/adapt`). El flujo de validación post-IA (cruce de entidades, severidad, bloques con nonce en el prompt) sí opera normalmente y se mantiene como en el spec.

## Pre-requisitos

- .NET 10 SDK
- M0 (002-score-engine) ya implementado y corriendo en `http://localhost:5080`
- **NO** se requiere cuenta de Anthropic ni API key para v0
- **NO** se requiere variable de entorno `Ai__ApiKey` (la implementación actual es un stub determinista)

## Setup local

```bash
# 1. Clonar e instalar
cd ~/Dev/portfolio/buildCV/BuildCv-api
dotnet restore

# 2. Levantar la API (no requiere secretos en v0)
dotnet run --project src/BuildCv.Api
# → http://localhost:5080
```

## Tests (TDD)

```bash
# 1. Tests del flujo de adaptación
dotnet test --filter "FullyQualifiedName~Adapt"
# Expected: 100% pass (ya implementado en M1)

# 2. Verificar purity del Domain
dotnet list src/BuildCv.Domain package references   # 0 paquetes
```

## Test end-to-end manual

```bash
# 1. Arrancar dev environment
cd ~/Dev/portfolio/buildCV
./scripts/dev.sh

# 2. Health check
curl http://localhost:5080/health/ready

# 3. Adaptar CV (síncrono)
curl -X POST http://localhost:5080/api/v1/adapt \
  -H "Content-Type: application/json" \
  -d '{
    "cvText": "Juan Pérez. Backend developer con 2 años de experiencia en C# y .NET. Trabajé en EmpresaXYZ como desarrollador junior.",
    "jobText": "Buscamos Senior Backend Developer con 5+ años de experiencia en C# y AWS certified. Empresa ABC Inc."
  }' | python3 -m json.tool
# Expected: HTTP 200 con adaptedCv (marco determinista del stub), validation con severity
# y lista de invenciones detectadas por el CrossEntityValidator. El stub NO agrega
# contenido, así que las invenciones deberían ser 0 en un CV legítimo.

# 4. Trampa: CV dice "2 años", vacante pide "5+" → el stub retorna un CV marco
# sin "5 años" inflados. La validación post-IA reporta severity=Warning o Critical
# si el stub hipotético introdujera entidades nuevas (en v0 no las introduce).
# (Este test valida que la defensa funciona end-to-end cuando se habilite un LLM real.)

# 5. Rate-limit
# Hacer 6 requests rápidos → la 6ª recibe 429 con ProblemDetails.
```

## Sin streaming en v0

El spec original proponía un endpoint SSE (`GET /api/v1/adapt/stream`) para mostrar
la adaptación progresivamente. La implementación shipped **NO** incluye ese endpoint:
el stub retorna el CV completo en una sola llamada y la latencia es <100ms. Cuando
se habilite un LLM real en v1, se reintroducirá SSE con el patrón `Results.ServerSentEvents`
de .NET 10. Por ahora, el BFF frontend debe mostrar un spinner hasta que la
respuesta sincrónica llegue.

## Golden set de CVs (validación post-IA)

La cobertura del golden set de CVs tech colombianos con trampas intencionales está
distribuida entre:
- `tests/BuildCv.Domain.Tests/Adapt/CrossEntityValidatorTests.cs` — cruces
  de entidades con trampa (skill inventada, empresa inventada, fecha fabricada, etc.).
- `tests/BuildCv.Domain.Tests/Adapt/EntityExtractorTests.cs` — extracción de
  skills, empresas, fechas, métricas, certificaciones, títulos.
- `tests/BuildCv.Domain.Tests/Adapt/SeverityPolicyTests.cs` — clasificación
  de severidad: `0 inventions → None`, `1-2 soft → Warning`, `≥3 soft o 1+ hard → Critical`.
- `tests/BuildCv.Application.Tests/Adapt/AdaptCvHandlerTests.cs` — flujo
  completo con el `IAiClient` mockeado, verificando extract → LLM → validate → result.

> **NO** existe `CrossEntityValidatorGoldenTests.cs` (el plan original lo listaba).
> Los casos de golden set están distribuidos en los tests de unidad nombrados arriba.

## Verificación pre-merge

```bash
# 1. Pre-flight
./scripts/preflight.sh
# Expected: all green, exit 0

# 2. Constitution check
bash /home/mackroph/Dev/portfolio/buildCV/scripts/constitution-check.sh
# Expected: 20/20 passes, 0 critical

# 3. Si ambos pasan, el commit 68baaf2 ya está merged. Para v1 (LLM real):
gh pr create --title "feat(003-adapt-ia): habilitar LLM real detrás de IAiClient" \
             --body "Reemplaza StubAiClient por AnthropicAiClient (Claude Sonnet 4). Mantiene IAiClient, PromptBuilder, CrossEntityValidator sin cambios. Cite Constitution Art. I, V, IX."
```

## Troubleshooting

- **ValidationReport siempre con severity=Critical en producción**: el stub no agrega contenido, así que esto solo debería pasar si hay un bug en `EntityExtractor` o `CrossEntityValidator`. Revisar logs estructurados (sin contenido, solo metadatos) y abrir issue.
- **Rate-limit 429 inmediato**: la política `"ai"` es 5/h por IP. Verificar que `Microsoft.AspNetCore.RateLimiting` está configurado en `Program.cs`.
- **503 con `AI_UNAVAILABLE`**: la excepción fue capturada por el handler (`AdaptCvHandler.cs:49-53`). Revisar `Console.Error` para el tipo de excepción exacta.
- **Endpoint muy lento**: con el stub, no debería pasar (>10s). Si pasa, probablemente hay un issue con el `IFormFile` upload upstream o con el rate-limit.
