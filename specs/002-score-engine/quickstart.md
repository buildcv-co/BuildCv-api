# Quickstart: 002-score-engine

**Date**: 2026-06-06 (orig) | **Status**: M0 cerrado

## Pre-requisitos

- .NET 10 SDK
- spec-kit + gentle-ai corriendo (no requerido para el score en sí)

## Setup local

```bash
cd ~/Dev/portfolio/buildCV/BuildCv-api
dotnet restore
dotnet build
```

## Tests (TDD)

```bash
# 1. Tests rojos PRIMERO
dotnet test --filter "FullyQualifiedName~Scoring"
# Expected: 100% pass (ya implementado en M0)

# 2. Verificar purity
dotnet list src/BuildCv.Domain package references   # 0 paquetes
dotnet list src/BuildCv.Domain reference   # 0 project refs
```

## Test end-to-end manual

```bash
# 1. Arrancar dev environment
cd ~/Dev/portfolio/buildCV
./scripts/dev.sh

# 2. Health check
curl http://localhost:5080/health/ready

# 3. Score happy path
curl -X POST http://localhost:5080/api/v1/score \
  -H "Content-Type: application/json" \
  -d '{
    "cvText": "Juan Pérez. Backend developer con 2 años de experiencia en C# y .NET. Trabajé en AcmeCorp como developer junior.",
    "jobText": "Buscamos developer backend con experiencia en C# y .NET."
  }' | python3 -m json.tool
# Expected: HTTP 200 con score, band, components, present, missing, engineVersion

# 4. Test con trampa: confundibles
curl -X POST http://localhost:5080/api/v1/score \
  -H "Content-Type: application/json" \
  -d '{
    "cvText": "Java developer con 3 años.",
    "jobText": "Buscamos JavaScript developer."
  }' | python3 -m json.tool
# Expected: score bajo porque "java" NO matchea "javascript" (blocklist Art. II)
```

## Verificación pre-merge

```bash
./scripts/preflight.sh
./scripts/constitution-check.sh
```

## Troubleshooting

- **Score siempre 0**: verificar que el CV tenga skills reconocibles por el gazetteer.
- **False positive entre java/javascript**: el blocklist debería bloquear. Ver `ConfusableBlocklist.cs`.
- **Performance <200ms p95**: si baja, considerar cachear el gazetteer en `ConcurrentDictionary`.
