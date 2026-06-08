# Tasks: 002-score-engine

**Date**: 2026-06-06 (orig) | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Nota histórica:** Estas tasks fueron ejecutadas en el commit `eded372` (M0 inicial). Este archivo es retroactivo para mantener consistencia con el formato del proyecto.

## Phase 0 — Setup

- [x] **T0.1** Crear `Lexicon/skills.gazetteer.v1.yaml` con 50+ skills tech colombianas.
- [x] **T0.2** Definir la estructura del `ISkillGazetteer` (puerto).

## Phase 1 — Domain (TDD)

### TextNormalizer + SpanishStemmer

- [x] **T1.1** [TEST RED] Normalizer preserva tokens técnicos (`C#`, `.NET`, `Node.js`).
- [x] **T1.2** [TEST RED] Normalizer preserva acentos y ñ (español colombiano).
- [x] **T1.3** [TEST RED] Stemmer NO confunde "año" con "ano".
- [x] **T1.4** [IMPL] `SpanishTextNormalizer` + `SpanishLightStemmer`.
- [x] **T1.5** [GREEN] Tests pasan.

### ConfusableBlocklist

- [x] **T1.6** [TEST RED] `java` ⇎ `javascript` bloqueado.
- [x] **T1.7** [TEST RED] `c` ⇎ `c#` bloqueado.
- [x] **T1.8** [TEST RED] `node` ⇎ `node.js` bloqueado.
- [x] **T1.9** [TEST RED] `postgres` ⇎ `postgresql` permitido (equivalentes).
- [x] **T1.10** [IMPL] `ConfusableBlocklist` con lista hardcoded.
- [x] **T1.11** [GREEN] Tests pasan.

### SkillScanner + ISkillMatcher (Cascada C1-C5)

- [x] **T1.12** [TEST RED] `SkillScanner` extrae skills conocidas.
- [x] **T1.13** [TEST RED] `SkillMatcher` cascada C1: match exacto.
- [x] **T1.14** [TEST RED] `SkillMatcher` cascada C2: sinonimia.
- [x] **T1.15** [TEST RED] `SkillMatcher` cascada C3: fuzzy con blocklist.
- [x] **T1.16** [TEST RED] `SkillMatcher` cascada C4: relacionado.
- [x] **T1.17** [TEST RED] `SkillMatcher` cascada C5: crédito parcial.
- [x] **T1.18** [IMPL] `SkillScanner` + `SkillMatcher` (Cascada C1-C5).
- [x] **T1.19** [GREEN] Tests pasan.

### ScoringEngine (orquestación)

- [x] **T1.20** [TEST RED] `ScoringEngine` produce score entero 0-100.
- [x] **T1.21** [TEST RED] `ScoringEngine` sella `EngineVersion`.
- [x] **T1.22** [TEST RED] `ScoringEngine` renormaliza cuando un componente no es observable.
- [x] **T1.23** [TEST RED] `ScoringEngine` es función pura (mismo input + versión = mismo output).
- [x] **T1.24** [IMPL] `ScoringEngine` orquesta todo.
- [x] **T1.25** [GREEN] Tests pasan.

## Phase 2 — Application

- [x] **T2.1** [IMPL] `ScoreCvCommand` (record).
- [x] **T2.2** [IMPL] `ScoreCvHandler` con primary constructor.
- [x] **T2.3** [IMPL] `ScoreCvValidator` (FluentValidation: max 20k chars CV y job).

## Phase 3 — Infrastructure

- [x] **T3.1** [IMPL] `Lexicon/skills.gazetteer.v1.yaml` (50+ skills).
- [x] **T3.2** [IMPL] `GazetteerLoader.LoadEmbedded()` (YamlDotNet).
- [x] **T3.3** [IMPL] Wire-up en `Infrastructure/DependencyInjection.cs`.

## Phase 4 — Api

- [x] **T4.1** [IMPL] `ScoreCvCommand` → endpoint POST /api/v1/score.
- [x] **T4.2** [IMPL] `ScoreResponseDto` + `ScoreResponseMapper`.
- [x] **T4.3** [IMPL] `RateLimiting.cs` con política "score" (60/min por IP).
- [x] **T4.4** [IMPL] `ValidationFilter<ScoreCvCommand>`.
- [x] **T4.5** [IMPL] `GlobalExceptionHandler` con RFC 9457 ProblemDetails.
- [x] **T4.6** [IMPL] `AiConfigHealthCheck` para health/ready.

## Phase 5 — Tests (final)

- [x] **T5.1** Golden set de CVs tech colombianos (5-10 casos) con trampa intencional.
- [x] **T5.2** Integration test del endpoint HTTP completo.
- [x] **T5.3** Cobertura ≥90% en `BuildCv.Domain/Scoring/`.
- [x] **T5.4** `dotnet test` → 100% verde.

## Pre-merge verification

- [x] `dotnet build BuildCv.slnx -c Release` → 0 warnings.
- [x] `dotnet test` → 100% verde.
- [x] `dotnet format --verify-no-changes` → limpio.
- [x] `dotnet list src/BuildCv.Domain package references` → 0 paquetes.
- [x] `dotnet list src/BuildCv.Domain reference` → 0 project refs.

## Resultado

- 92 tests verdes.
- 681 líneas de código en Domain.
- EngineVersion 1.0.0 sellada en cada `ScoreResult`.
- 0 supresiones (`#pragma warning disable`, `[Skip]`).
- Commit: `eded372` "BuildCv API (.NET 10) — motor de puntaje determinista".
