# BuildCv · API (.NET)

API del asistente de CV **BuildCv**: calcula un **puntaje determinista de coincidencia y legibilidad** entre un CV y una vacante — explicable, reproducible y **sin inventar nada** (el número no lo produce ningún LLM). Frontend en directorio hermano: **[`../BuildCv-web/`](../BuildCv-web/)** (repositorio independiente).

[![CI](https://github.com/buildcv-co/BuildCv-api/actions/workflows/ci.yml/badge.svg)](https://github.com/buildcv-co/BuildCv-api/actions/workflows/ci.yml) [![License: FSL-1.1-ALv2](https://img.shields.io/badge/license-FSL--1.1--ALv2-2ea44f)](LICENSE.md)

## Arquitectura

Clean Architecture en 4 capas — regla de dependencias `Domain ← Application ← Infrastructure`; `Api` compone:

- **BuildCv.Domain** — núcleo **PURO** (sin IO/red/reloj/aleatoriedad): normalización en español, gazetteer de skills, **Jaro-Winkler / Levenshtein / stemmer escritos a mano**, matcher de cascada y el `ScoringEngine` (C1–C5 con fórmula renormalizada y compuertas).
- **BuildCv.Application** — casos de uso por feature + puertos (IA, parseo, export…).
- **BuildCv.Infrastructure** — adaptadores; StubAiClient (v0), QuestPDF (export), PdfPig/OpenXML (import), carga del gazetteer (YAML embebido).
- **BuildCv.Api** — Minimal APIs, ProblemDetails (RFC 9457), OpenAPI/Scalar, versionado, rate limiting nativo, health checks.

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/score` | Análisis determinista: puntaje + componentes + keywords + recomendaciones |
| `POST` | `/api/v1/adapt` | Adaptación del CV con IA (stub v0), validación de invenciones |
| `POST` | `/api/v1/export` | Generación de PDF (QuestPDF) con validación de invenciones duras |
| `POST` | `/api/v1/import` | Importación de CV en PDF/DOCX (PdfPig, OpenXML) |
| `GET` | `/health/live` · `/health/ready` | Salud |

## Desarrollo

Requisitos: **.NET 10 SDK**.

```bash
dotnet build BuildCv.slnx -c Release        # compila con warnings-as-errors (0 warnings)
dotnet test                                  # 189 tests (xUnit + FluentAssertions)
dotnet test --filter "FullyQualifiedName~ScoringEngine"   # una sola clase/test
dotnet format                                # CI verifica con --verify-no-changes
dotnet run --project src/BuildCv.Api         # http://localhost:5080 · docs en /scalar/v1
```

### Iteration public API containment

The public iteration POST and GET routes are fail-closed. They are mapped only when
`Iteration__PublicApiEnabled=true` is explicitly configured for controlled compatibility
or development testing. Keep the key absent or set it to `false` in production.

To deactivate the routes, remove the key or set it to `false`, then restart the service.
Enabling the routes is not a rollback strategy and does not remediate iteration persistence
or ownership risks.

## Docker / Deploy (Render)

```bash
docker build -t buildcv-api .
docker run -p 8080:8080 buildcv-api          # GET /health/live -> 200
```

Incluye **`render.yaml`** (blueprint Docker). El contenedor respeta `$PORT` (Render/Railway).

## Planeación (Spec-Driven Development)

`specs/000-INDEX.md` (master registry) y `.specify/memory/constitution.md`. Estrategia general en `PLANEACION.md`.

> .NET 10 · EF Core (v1) · xUnit + FluentAssertions (fijado en 7.x por licencia). Privacidad por diseño: no se persiste el CV; los logs nunca incluyen su contenido.

## Licencia

**FSL-1.1-ALv2** ([Functional Source License](https://fsl.software)) — código a la vista: puedes usar, modificar y redistribuir el software para cualquier propósito **que no compita** con BuildCv. Se convierte automáticamente a **Apache 2.0 a los 2 años**. Ver [`LICENSE.md`](LICENSE.md).
