# BuildCv · API (.NET)

API del asistente de CV **BuildCv**: calcula un **puntaje determinista de coincidencia y legibilidad** entre un CV y una vacante — explicable, reproducible y **sin inventar nada** (el número no lo produce ningún LLM). Frontend en repo aparte: **[BuildCv-web](https://github.com/CristianMz21/BuildCv-web)**.

[![CI](https://github.com/CristianMz21/BuildCv-api/actions/workflows/ci.yml/badge.svg)](https://github.com/CristianMz21/BuildCv-api/actions/workflows/ci.yml)

## Arquitectura

Clean Architecture en 4 capas — regla de dependencias `Domain ← Application ← Infrastructure`; `Api` compone:

- **BuildCv.Domain** — núcleo **PURO** (sin IO/red/reloj/aleatoriedad): normalización en español, gazetteer de skills, **Jaro-Winkler / Levenshtein / stemmer escritos a mano**, matcher de cascada y el `ScoringEngine` (C1–C5 con fórmula renormalizada y compuertas).
- **BuildCv.Application** — casos de uso por feature + puertos (IA, parseo, export…).
- **BuildCv.Infrastructure** — adaptadores; carga del gazetteer (YAML embebido). IA/PDF/persistencia en hitos futuros.
- **BuildCv.Api** — Minimal APIs, ProblemDetails (RFC 9457), OpenAPI/Scalar, versionado, rate limiting nativo, health checks.

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/score` | Análisis determinista: puntaje + componentes + keywords + recomendaciones |
| `GET` | `/health/live` · `/health/ready` | Salud |

## Desarrollo

Requisitos: **.NET 10 SDK**.

```bash
dotnet build BuildCv.slnx -c Release        # compila con warnings-as-errors (0 warnings)
dotnet test                                  # 92 tests (xUnit + FluentAssertions)
dotnet test --filter "FullyQualifiedName~ScoringEngine"   # una sola clase/test
dotnet format                                # CI verifica con --verify-no-changes
dotnet run --project src/BuildCv.Api         # http://localhost:5080 · docs en /scalar/v1
```

## Docker / Deploy (Render)

```bash
docker build -t buildcv-api .
docker run -p 8080:8080 buildcv-api          # GET /health/live -> 200
```

Incluye **`render.yaml`** (blueprint Docker). El contenedor respeta `$PORT` (Render/Railway).

## Planeación (Spec-Driven Development)

`specs/001-mvp-cv-ats/` (spec · plan · research · data-model · contracts · tasks) y `.specify/memory/constitution.md`. Estrategia general en `PLANEACION.md`.

> .NET 10 · EF Core (v1) · xUnit + FluentAssertions (fijado en 7.x por licencia). Privacidad por diseño: no se persiste el CV; los logs nunca incluyen su contenido.
