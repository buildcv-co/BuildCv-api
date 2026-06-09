# Implementation Plan: 002-score-engine

**Branch**: `002-score-engine` | **Date**: 2026-06-06 (orig), formalized 2026-06-08 | **Spec**: [spec.md](./spec.md)

> **Nota histórica:** Este plan fue creado retroactivamente para mantener consistencia con el resto del proyecto (003, 004 ya tienen planes completos). El código fue implementado en el commit `eded372` (M0 inicial).

## Summary

Motor de puntaje determinista y explicable que calcula coincidencia entre un CV y una vacante (0-100) sin usar LLM. Implementa dos nociones distintas, ambas con prefijo "C":

- **Cascada de matching** (`SkillMatcher.cs:32-93`): 5 niveles aplicados a CADA requisito de la vacante contra el CV. T0 Exacto → T1 Implicación ascendente → T2 Lema/stem → T3 Relacionado/implicación descendente → T4 Fuzzy blindado. Produce `MatchResult(Tier, Placement, Credit)`.
- **Componentes de score** (`ScoringEngine.cs:8-24`): 5 dimensiones ponderadas que se agregan al número final. Match 45% · Structure 20% · Achievements 20% · Format 10% · Length 5%.

Estos dos "C" no se confunden: la cascada es la regla de matching por requisito; los componentes son las dimensiones del score final.

Renormalización: cada componente tiene `Measurability` (0..1) que pondera su contribución; el global se renormaliza sobre el peso efectivamente medible (Art. II).

**Decisiones técnicas:**

- **C# puro (.NET 10)**, dominio PURO (0 packages, 0 project refs)
- **Función pura**: `Score(JobRequirementSet, CvAnalysis)` no hace IO, no lee reloj, no consulta red.
- **EngineVersion sellada** (constante `ScoringEngine.Version = "1.0.0"`): en cada `ScoreResult`, garantiza comparabilidad temporal.
- **LexiconVersion** + **ContextHash**: también se sella en cada `ScoreResult` para reproducibilidad.
- **Gazetteer YAML embebido**: `BuildCv.Infrastructure/Lexicon/skills.gazetteer.v1.yaml`, cargado como `Singleton` inmutable vía `EmbeddedResource`.

## Technical Context

**Language/Version**: C# 13 / .NET 10
**Primary Dependencies**: (solo Standard Library) `System.Text.RegularExpressions`, `System.Collections.Immutable`
**Storage**: N/A (v0, sin persistencia)
**Testing**: xUnit + FluentAssertions
**Target Platform**: Linux server (Render.com Docker)
**Project Type**: Web service backend
**Performance Goals**: p95 <200ms para CVs <20k chars
**Constraints**: Constitution Art. II, III, VI, VIII
**Scale/Scope**: v0 MVP, ~100 scores/día esperados

## Constitution Check

| Art. | Status | Note |
|---|---|---|
| I — Cero invención | N/A (no IA en M0) | Scoring puro, no adaptación |
| II — Determinismo | ✅ PASS | Función pura, EngineVersion sellada |
| III — Privacidad | ✅ PASS | Sin persistencia, logs sin contenido |
| IV — Encuadre honesto | ✅ PASS | "coincidencia + legibilidad" en UI |
| VI — Clean Arch | ✅ PASS | Domain PURO verificado |
| VII — Rate-limit | ✅ PASS | Política "score" 60/min por IP |
| VIII — TDD | ✅ PASS | Tests rojos ANTES de implementación |
| IX — Habeas Data | N/A (v0 sin pago) | Sin datos persistidos |

## Project Structure

### Documentation (this feature)

```
specs/002-score-engine/
├── spec.md           # This file
├── plan.md           # Implementation plan (retroactive)
├── research.md       # Phase 0: research on scoring algorithms
├── data-model.md     # Phase 1: ScoreResult, components
├── quickstart.md     # Phase 1: how to test
├── tasks.md          # Phase 2: implementation tasks
└── contracts/        # Phase 1: HTTP contracts (POST /api/v1/score)
```

### Source Code

```
src/BuildCv.Domain/Scoring/
├── ScoringEngine.cs       # Orquestación (322 líneas)
├── SkillMatcher.cs        # Cascada de matching T0–T4 (107 líneas)
├── SkillScanner.cs        # Extracción de skills (45 líneas)
├── MatchResult.cs         # Tipo inmutable: Tier + Placement + Credit (30 líneas)
├── KeywordAnalysis.cs     # Análisis de keywords cruzadas (21 líneas)
├── ScoreResult.cs         # ComponentId, ScoreBand, ComponentScore, FormatIssue, GateApplied, ScoreResult (52 líneas)
├── Recommendation.cs      # Sugerencias priorizadas (23 líneas)
├── CvProfile.cs           # Value object CV (11 líneas)
├── IScoringEngine.cs      # Puerto (13 líneas)
└── ISkillMatcher.cs       # Puerto (9 líneas)
# Total: 633 líneas

src/BuildCv.Application/Features/Scoring/
├── ScoreCvCommand.cs
├── ScoreCvHandler.cs
└── ScoreCvValidator.cs
```

## Phase 0 — Research

- **Cascada de matching** (`SkillMatcher`): T0 Exacto → T1 Implicación ascendente (p. ej. ASP.NET Core ⇒ .NET) → T2 Lema/stem (keywords genéricas) → T3 Relacionado o implicación descendente → T4 Fuzzy blindado (Jaro-Winkler ≥ 0.92).
- **Componentes del score** (`ScoringEngine`): Match 45% / Structure 20% / Achievements 20% / Format 10% / Length 5%. El global renormaliza sobre el peso efectivamente medible (Measurability).
- **Gazetteer YAML**: cargar `skills.gazetteer.v1.yaml` como recurso embebido (`EmbeddedResource`) en `BuildCv.Infrastructure`, registrado como Singleton inmutable.
- **Blocklist de confundibles**: `java` ⇎ `javascript`, `c` ⇎ `c#`, `node` ⇎ `node.js` — el fuzzy matching NO debe cruzar estos.

## Phase 1 — Design

### Data Model

- **`ScoreResult`** (record, sellado): `int Overall`, `ScoreBand Band`, `string Disclaimer`, `IReadOnlyList<ComponentScore> Components`, `KeywordAnalysis Keywords`, `IReadOnlyList<Recommendation> Recommendations`, `IReadOnlyList<FormatIssue> FormatIssues`, `IReadOnlyList<GateApplied> GatesApplied`, `string EngineVersion`, `string LexiconVersion`, `string ContextHash`.
- **`ComponentScore`** (record): `ComponentId Id`, `double SubScore`, `double Weight`, `double Measurability`, `double Confidence`, `string Summary`.
- **`ScoreBand`** (enum): `Bajo` (<40), `Medio` (<65), `Bueno` (<85), `Fuerte` (≥85). El número es el valor rector, la banda es cualitativa.
- **`ComponentId`** (enum): `Match`, `Structure`, `Achievements`, `Format`, `Length`.
- **`GateApplied`** (record): `ComponentId Component`, `double Cap`, `string Reason`, `string Message` (cap aplicado, p. ej. "no-contact" baja Structure a 0.5).
- **`FormatIssue`** (record): `string Code`, `string Severity` ("warn" | "info"), `string Message`.

### Contracts

- **POST /api/v1/score** (HTTP, ver [contracts/](../../api/contracts.md))
  - Request: `{ cvText, jobText }` (max 20_000 chars cada uno, FluentValidation)
  - Response 200: shape completo de `ScoreResponseDto` con `score`, `band`, `components[]`, `present[]`, `missing[]`, `partial[]`, `recommendations[]`, `formatIssues[]`, `gatesApplied[]`, `disclaimer`, `engineVersion`.

## Phase 2 — Tasks

Ver [tasks.md](./tasks.md) para el breakdown TDD-ordered.

## Risks

1. **False positives en fuzzy matching**: el blocklist de confundibles es crítico. Tests exhaustivos.
2. **Performance con CVs grandes**: regex de 50k chars + match. p95 objetivo <200ms.
3. **Gazetteer desactualizado**: si no se actualiza, scores pierden accuracy. Plan: versionar el YAML.

## Out of Scope

- Persistencia de scores (v1)
- Histórico de scores del mismo usuario (v1)
- Cache de resultados (v1+)
