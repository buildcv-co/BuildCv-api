# Implementation Plan: 002-score-engine

**Branch**: `002-score-engine` | **Date**: 2026-06-06 (orig), formalized 2026-06-08 | **Spec**: [spec.md](./spec.md)

> **Nota histórica:** Este plan fue creado retroactivamente para mantener consistencia con el resto del proyecto (003, 004 ya tienen planes completos). El código fue implementado en el commit `eded372` (M0 inicial).

## Summary

Motor de puntaje determinista y explicable que calcula coincidencia entre un CV y una vacante (0-100) sin usar LLM. Implementa la cascada C1-C5 (match exacto → sinonimia → fuzzy → relacionado → crédito parcial) con renormalización de componentes no observables (Art. II).

**Decisiones técnicas:**

- **C# puro (.NET 10)**, dominio PURO (0 packages, 0 project refs)
- **Cascada C1-C5**: orden de aplicación de reglas, cada una con peso propio
- **Renormalización**: componentes no observables se excluyen sin penalizar
- **EngineVersion sellada**: en cada `ScoreResult`, garantiza comparabilidad temporal
- **Gazetteer YAML embebido**: `Lexicon/skills.gazetteer.v1.yaml`, cargado como `ISingleton` inmutable

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
├── SkillMatcher.cs        # Cascada C1-C5 (107 líneas)
├── SkillScanner.cs        # Extracción de skills (45 líneas)
├── MatchResult.cs         # Tipo inmutable (30 líneas)
├── KeywordAnalysis.cs     # Análisis de keywords (21 líneas)
├── ScoreResult.cs         # Sella EngineVersion (52 líneas)
├── Recommendation.cs      # Sugerencias priorizadas (23 líneas)
├── CvProfile.cs           # Value object CV (11 líneas)
├── IScoringEngine.cs      # Puerto (13 líneas)
└── ISkillMatcher.cs       # Puerto (9 líneas)

src/BuildCv.Application/Features/Scoring/
├── ScoreCvCommand.cs
├── ScoreCvHandler.cs
└── ScoreCvValidator.cs
```

## Phase 0 — Research

- **Cascada C1-C5**: orden de aplicación de matching rules. Match exacto (C1) > sinonimia (C2) > fuzzy (C3) > relacionado (C4) > crédito parcial (C5).
- **Renormalización**: si un componente no es observable (ej. formato sin skills extraíbles), se excluye sin penalizar.
- **Gazetteer YAML**: cargar skills.gazetteer.v1.yaml como recurso embebido (immutable, versionado).
- **Blocklist de confundibles**: `java` ⇎ `javascript`, `c` ⇎ `c#`, `node` ⇎ `node.js` — el fuzzy matching NO debe cruzar estos.

## Phase 1 — Design

### Data Model

- **`ScoreResult`** (record): `int Score`, `string Band`, `IReadOnlyList<ComponentBreakdown> Components`, `IReadOnlyList<string> Present`, `IReadOnlyList<string> Missing`, `string EngineVersion`.
- **`ComponentBreakdown`** (record): `string Code`, `double Weight`, `double Value`, `string Rationale`.

### Contracts

- **POST /api/v1/score** (HTTP, ver [contracts/](../../api/contracts.md))
  - Request: `{ cvText, jobText }`
  - Response 200: `{ score, band, components, present, missing, engineVersion }`

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
