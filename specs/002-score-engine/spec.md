# Feature 002 — Score Engine (M0 — Scoring determinista)

> **Status:** ✅ SHIPPED · **Cerrada:** 2026-06-08 · **Versión del motor:** 1.0.0
> **Frontend counterpart:** [../../../BuildCv-web/specs/002-web-score-ui/](../../../BuildCv-web/specs/002-web-score-ui/)
> **INDEX global:** [../000-INDEX.md](../000-INDEX.md)

## Resumen

Implementación del **motor de puntaje determinista y explicable** que calcula coincidencia entre un CV y una vacante (0-100) sin usar LLM en el cálculo del número.

**Constitución cumplimiento:**
- Art. II (Puntaje determinista y explicable) — motor en C# puro, función pura
- Art. VI (Clean Architecture) — Domain PURO, sin IO/red/reloj/aleatoriedad
- Art. VIII (TDD) — tests rojos antes de implementación
- Art. IV (Encuadre honesto) — "coincidencia con la vacante + legibilidad"

## Capa Domain (`src/BuildCv.Domain/Scoring/`)

| Tipo | Líneas | Responsabilidad |
|---|---|---|
| `ScoringEngine.cs` | 322 | Orquestación: tokeniza, matchea, pondera, renormaliza |
| `SkillMatcher.cs` | 107 | Cascada C1-C5: match exacto, sinonimia, fuzzy, relacionado, crédito parcial |
| `SkillScanner.cs` | 45 | Extracción de skills del CV y la vacante |
| `MatchResult.cs` | 30 | Tipo inmutable con `Present` + `Missing` |
| `KeywordAnalysis.cs` | 21 | Análisis de keywords cruzadas |
| `ScoreResult.cs` | 52 | Sella `EngineVersion` + `Components` + bandas cualitativas |
| `Recommendation.cs` | 23 | Sugerencias priorizadas para el usuario |
| `CvProfile.cs` | 11 | Value object del CV parseado |
| `IScoringEngine.cs` | 13 | Puerto (interfaz) |
| `ISkillMatcher.cs` | 9 | Puerto (interfaz) |

**Total dominio:** 681 líneas.

## Capa Application (`src/BuildCv.Application/Features/Scoring/`)

- `ScoreCvCommand.cs` — comando inmutable (record)
- `ScoreCvHandler.cs` — handler con primary constructor
- `ScoreCvValidator.cs` — `MaximumLength(20_000)` para CV y job (Art. V, topes)

## Capa Api

- `Endpoints/ScoringEndpoints.cs` — Minimal API `MapPost /api/v1/score`
- `Security/RateLimiting.cs` — rate-limit por IP (20/min)
- `Contracts/ScoreResponse.cs` — DTO con `EngineVersion` sellado
- `Contracts/ScoreResponseMapper.cs` — Domain → HTTP
- `Health/AiConfigHealthCheck.cs` — health check del provider IA
- `Errors/GlobalExceptionHandler.cs` — RFC 9457 ProblemDetails

## Tests

- `BuildCv.Domain.Tests/Scoring/ScoringEngineTests.cs` — cobertura del motor
- `BuildCv.Domain.Tests/Scoring/SkillMatcherTests.cs` — cascada + blocklist
- `BuildCv.Api.IntegrationTests/ScoringEndpointTests.cs` — wire-up HTTP

## Verificación

```bash
cd BuildCv-api
dotnet build BuildCv.slnx -c Release       # 0 warnings
dotnet test                                 # 100% verde
dotnet list src/BuildCv.Domain package references   # 0 paquetes
dotnet list src/BuildCv.Domain reference   # 0 project refs
```

## Próximas features

- **003-adapt-ia** (M1) — adaptación con IA, bloques con nonce, validación post-IA
- **004-export-pdf** (M2) — exportación del CV adaptado
- **005-ui-analizador** (M0.1) — UI web del score (paralela a 003)
