# Research: 002-score-engine

**Date**: 2026-06-06 (orig) | **Status**: M0 cerrado (commit eded372)

## 1. Algoritmo de matching

**Cascada C1-C5**: orden de aplicación de matching rules, cada una con peso propio.

| Nivel | Regla | Peso base | Notas |
|---|---|---|---|
| **C1** | Match exacto (canonical == token) | 1.0 | Match perfecto |
| **C2** | Sinonimia (alias match) | 0.9 | `Node.js` → `NodeJS` |
| **C3** | Fuzzy (Levenshtein ≤2) | 0.7 | Bloqueado por blocklist de confundibles |
| **C4** | Relacionado (cascade credit) | 0.5 | `React` → `Frontend` |
| **C5** | Crédito parcial (sin match) | 0.0 | Se exclude en renormalización |

## 2. Renormalización

Cuando un componente no es observable (ej. el CV es muy corto y no tiene skills explícitas), se **excluye del cálculo** sin penalizar. Esto evita dar score bajo artificial por falta de información.

**Ejemplo:** Si `SkillMatch` tiene peso 0.5 y no es evaluable, se redistribuye el peso entre los componentes evaluables proporcionalmente.

## 3. Gazetteer (YAML)

**Path:** `BuildCv.Infrastructure/Lexicon/skills.gazetteer.v1.yaml`

**Estructura:** cada skill tiene:
- `id`: canonical (ej. `skill.csharp`)
- `canonical`: nombre display (ej. `C#`)
- `category`: HardSkill | Tool | SoftSkill | GenericKeyword
- `aliases`: nombres equivalentes (ej. `Node.js`, `NodeJS`)
- `implies`: skills que este implica (ej. `ASP.NET Core` ⇒ `.NET`)
- `related`: skills relacionadas (ej. `React` ⇒ `Frontend`)
- `confusable_with`: lista negra de fuzzy match (ej. `java` ⇎ `javascript`)

**Carga:** `YamlDotNet` deserializa a `List<SkillEntry>`, registrado como `Singleton` inmutable.

## 4. Blocklist de confundibles

Skills que NO deben hacer fuzzy match entre sí:
- `java` ⇎ `javascript`
- `c` ⇎ `c#`
- `node` ⇎ `node.js`
- `postgres` ⇎ `postgresql` (este SÍ es equivalente)

## 5. Performance

- CV típico (5-10k chars) → score en ~150-300ms.
- CV máximo (50k chars) → score en ~800ms.
- p95 objetivo: <200ms para CVs <20k chars.

## 6. Riesgos identificados

1. **False positives en fuzzy**: el blocklist es la defensa. Tests exhaustivos.
2. **Gazetteer desactualizado**: si no se actualiza, scores pierden accuracy. Plan: versionar el YAML con `Version` field.
3. **Cold start**: el primer request carga el YAML (1-2s). Solución: `Singleton` eager.

## 7. Stack technique rationale

- **C# puro (no F#)**: equipo tiene experiencia, ecosistema .NET maduro.
- **YAML sobre JSON**: más legible para edición manual, comentarios permitidos.
- **YamlDotNet**: deserialización rápida, soporta streaming.
- **xUnit + FluentAssertions**: estándar en .NET, mejor legibilidad que NUnit.

## Next Phase

→ Phase 1: Design — `data-model.md`, `quickstart.md`, `contracts/`.
→ Phase 2: Tasks — TDD-ordered.
