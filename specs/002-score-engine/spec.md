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
| `SkillMatcher.cs` | 107 | Cascada T0–T4: exacto → implicación ascendente → relacionado/lugar → lema/stem → fuzzy blindado |
| `SkillScanner.cs` | 45 | Extracción de skills del CV y la vacante |
| `MatchResult.cs` | 30 | Tipo inmutable: `Tier` (MatchTier) + `Placement` + `Credit` |
| `KeywordAnalysis.cs` | 21 | Análisis de keywords cruzadas (present, missing, partial) |
| `ScoreResult.cs` | 52 | Sella `EngineVersion` + `LexiconVersion` + `ContextHash` + `Band` (enum Bajo/Medio/Bueno/Fuerte) + `Components` + `GatesApplied` + `FormatIssues` + `Disclaimer` |
| `Recommendation.cs` | 23 | Sugerencias priorizadas para el usuario |
| `CvProfile.cs` | 11 | Value object del CV parseado |
| `IScoringEngine.cs` | 13 | Puerto (interfaz) |
| `ISkillMatcher.cs` | 9 | Puerto (interfaz) |

**Total dominio:** 633 líneas.

## Capa Application (`src/BuildCv.Application/Features/Scoring/`)

- `ScoreCvCommand.cs` — comando inmutable (record)
- `ScoreCvHandler.cs` — handler con primary constructor
- `ScoreCvValidator.cs` — `MaximumLength(20_000)` para CV y job (Art. V, topes)

## Capa Api

- `Endpoints/ScoringEndpoints.cs` — Minimal API `MapPost /api/v1/score`
- `Security/RateLimiting.cs` — rate-limit por IP (60/min)
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
- **005-cv-pdf-docx-import** (M0.5 / v0.5) — carga de PDF/DOCX server-side (shipped, ver `specs/005-cv-pdf-docx-import/`)

---

## v2.0.0 Changes (delta from change 021-structured-cv-import-and-job-input, archived 2026-06-26)

> **Bumps**: `ScoringEngine.Version` 1.0.0 → **2.0.0** (breaking; backward-compat shim shipped)
> **Constitution**: Art. II (deterministic, pure), Art. VI (Domain purity), Art. VIII (test-first)
> **Source delta**: [`BuildCv-web/specs/021-structured-cv-import-and-job-input/specs/002-score-engine/spec.md`](../../BuildCv-web/specs/021-structured-cv-import-and-job-input/specs/002-score-engine/spec.md)
> **Archive report**: [`BuildCv-web/specs/021-structured-cv-import-and-job-input/archive-report.md`](../../BuildCv-web/specs/021-structured-cv-import-and-job-input/archive-report.md)

### ADDED Requirements

#### Requirement: ScoreCvCommand accepts discriminated union

The system MUST accept `ScoreCvCommand` whose payload is a discriminated union: `{ cv: CvDocument, job: JobSpec, engineVersion: "2.0.0" }` OR `{ cvText: string, jobText: string, engineVersion: "1.0.0" }`. The discriminator is `engineVersion`; mixing structured `cv` with `engineVersion: "1.0.0"` MUST be rejected with HTTP 422 `VERSION_MISMATCH`.

#### Scenario: Structured v2 request

- GIVEN `{ cv: CvDocument, job: JobSpec, engineVersion: "2.0.0" }`
- WHEN the handler runs
- THEN the structured path is taken, regex analyzers are bypassed, and `ScoreResponse` includes `perSection` + `redFlags`

#### Scenario: Legacy v1 request

- GIVEN `{ cvText, jobText, engineVersion: "1.0.0" }`
- WHEN the handler runs
- THEN the legacy path runs and returns the v1 response shape

#### Scenario: Mismatched version rejected

- GIVEN `{ cv: CvDocument, jobText, engineVersion: "1.0.0" }`
- WHEN validated
- THEN HTTP 422 with code `VERSION_MISMATCH`

#### Requirement: ScoreResponse gains perSection and redFlags

The system MUST return `ScoreResponse` containing `perSection: { experience, education, skills, certifications, contact }` (each `number | null`) and `redFlags: RedFlag[]`. Both fields MUST be absent on v1 responses and present on all v2 responses. See `specs/score-section-breakdown/spec.md` for the full subscore contract.

#### Scenario: Structured v2 response

- GIVEN a valid v2 request
- WHEN scored
- THEN `ScoreResponse.perSection.skills === <integer 0–100>` and `ScoreResponse.redFlags` is an array (possibly empty)

#### Scenario: Legacy v1 response unchanged

- GIVEN a v1 request
- WHEN scored
- THEN `ScoreResponse.perSection` and `ScoreResponse.redFlags` are absent

#### Requirement: ScoringEngine.Version is 2.0.0

The system MUST seal `ScoringEngine.Version === "2.0.0"` on every v2 response, exposed as `engineVersion` in the HTTP contract. The value MUST be a compile-time constant in `BuildCv.Domain.Scoring`.

#### Scenario: Version seal on response

- GIVEN any v2 request
- WHEN scored
- THEN `ScoreResponse.engineVersion === "2.0.0"`

#### Scenario: Domain constant is the single source of truth

- GIVEN `BuildCv.Domain/Scoring/ScoringEngine.cs`
- WHEN a reader inspects the source
- THEN the literal string `"2.0.0"` appears exactly once (referenced from a single `public const string Version`)

#### Requirement: Structured input bypasses regex analyzers

The system MUST skip `CvAnalyzer` and `JobAnalyzer` regex passes when the input is structured (`cv: CvDocument` + `job: JobSpec`). The Domain MUST consume the typed inputs directly; the regex code path is reachable only via the v1 shim.

#### Scenario: No regex on structured path

- GIVEN a v2 request with `cv: CvDocument`
- WHEN scored
- THEN `CvAnalyzer.Analyze(text)` is NOT invoked

#### Scenario: Regex on legacy path

- GIVEN a v1 request with `cvText`
- WHEN scored
- THEN `CvAnalyzer.Analyze(cvText)` IS invoked

#### Requirement: Determinism over structured input (Art. II property)

The system MUST satisfy: for any `cv: CvDocument`, `job: JobSpec`, and constant `engineVersion="2.0.0"`, calling `ScoringEngine.Score` 1000 times in parallel returns byte-identical `ScoreResponse`. Verified via a property-based test in `BuildCv.Domain.Tests/Scoring/ScoringEngineDeterminismPropertyTests.cs`.

#### Scenario: Same input → identical output

- GIVEN a fixed `cv` and `job`
- WHEN `Score` is called 1000 times
- THEN all 1000 responses are byte-equal and contain the same `engineVersion: "2.0.0"`

#### Scenario: No clock on calc path

- GIVEN the `ScoringEngine.Score` source
- WHEN a static analyzer (regex `DateTime.UtcNow|Guid.NewGuid|Random`) is run
- THEN zero matches exist on the calc path (excluding the `traceId` factory which lives in the handler, not the engine)

### MODIFIED Requirements

#### Requirement: ScoreCvValidator — JobSpec validation caps

The system MUST apply validation rules: `cvText`/`jobText` length ≤ 20_000 chars when present; `JobSpec` shape when `engineVersion="2.0.0"` (title ≤ 200, company ≤ 200, location ≤ 200, description ≤ 20_000, requirements ≤ 500 × 100); `engineVersion` MUST be a known value.
(Previously: ScoreCvValidator only checked `MaximumLength(20_000)` on `cvText`/`jobText`.)

#### Scenario: JobSpec required on v2

- GIVEN `{ cv: CvDocument, jobText: "..." , engineVersion: "2.0.0" }`
- WHEN validated
- THEN HTTP 422 with code `JOB_SPEC_REQUIRED`

#### Scenario: Length cap preserved on v1

- GIVEN `{ cvText: <30k chars>, engineVersion: "1.0.0" }`
- WHEN validated
- THEN HTTP 422 with code `CV_TEXT_TOO_LONG`

#### Requirement: ScoreResponse — engineVersion seal field

The system MUST include `engineVersion` on every `ScoreResponse`, mapping to `ScoringEngine.Version`.
(Previously: v1 sealed `engineVersion: "1.0.0"`.)

#### Scenario: v1 seal unchanged on legacy path

- GIVEN a v1 request
- WHEN scored
- THEN `engineVersion === "1.0.0"`

#### Scenario: v2 seal on structured path

- GIVEN a v2 request
- WHEN scored
- THEN `engineVersion === "2.0.0"`

### REMOVED Requirements

#### Requirement: ScoreResponse requires cvText/jobText on v2

(Reason: v2 is structured-only; legacy fields are removed from the contract on the v2 path.)
(Migration: clients on the v2 path migrate to `cv`/`job`; clients still on v1 continue to use `cvText`/`jobText` and pin `engineVersion: "1.0.0"`.)

### Rollback Plan

#### Requirement: Backward-compat shim is feature-flagged

The system MUST allow clients to pin `engineVersion: "1.0.0"` on `/api/v1/score` and receive the v1 response shape, regardless of the default `engineVersion` configured on the server. The shim MUST be available for at least one release cycle after the v2 GA.

#### Scenario: v1 client served by v2 server

- GIVEN a v2 server is deployed
- WHEN a v1 client requests with `engineVersion: "1.0.0"`
- THEN the v1 response shape is returned and the legacy analyzers run

#### Requirement: Zero server-side persistence preserves rollback simplicity

The system MUST NOT introduce persistence of CV, job, or score responses on the v2 path (Constitution Art. III). Rollback therefore requires no data migration — only a server build swap.

#### Scenario: Rollback has no data impact

- GIVEN the v2 server is in production
- WHEN the owner rolls back to v1
- THEN no migrations, no ETL, no cache flushes are required; clients on `engineVersion: "1.0.0"` continue working immediately
