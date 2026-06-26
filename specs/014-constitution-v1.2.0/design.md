# Design: 014-constitution-v1.2.0

## Status

[Design] — Pending tasks

## Architecture overview

This is a **governance amendment** — no code changes. The design specifies the literal markdown changes to `BuildCv-api/.specify/memory/constitution.md` to bump v1.1.0 → v1.2.0, plus the supporting documentation touchpoints (CONSTITUTION-README.md, AGENTS.md, INDEX) required by §Gobernanza paso 4 (Registro).

The changes are organized into **6 sections** within `constitution.md`:

1. **Header** — version bump + ratification date + amendment note
2. **Art. III** — v0/v1 persistence boundary clarification (blockquote)
3. **Art. VI** — `next-auth@^4.24.7` dep ratification + approved deps list (blockquote)
4. **Art. VII** — v0/v1 auth boundary clarification (blockquote)
5. **Art. IX** — implementation cross-references (blockquote, no normative text change)
6. **§Gobernanza** — amendment history table append (1 row)

**Total constitution.md impact: ~22 lines added, ~1 line modified, 0 deleted.** Plus 4 supporting docs updated.

This change is purely additive (blockquote notes + 1 row in history table + 1 line replacement on Fecha). Zero risk of semantic conflict with existing articles. All modifications are documentary; no behavior observable to users changes.

## Markdown diff

> Convention: each section shows the literal `Before` and `After` markdown as it should appear in `constitution.md`. The blockquote notes (`>`) are appended immediately after the existing first paragraph of each article, preserving all existing content (Principio, Reglas, Justificación) unchanged.

### Section 1: Header

**Before** (lines 1–7 of constitution.md):

```markdown
# Constitución del Proyecto — BuildCv

> **Artefacto SDD:** `.specify/memory/constitution.md` — ley fundamental del proyecto al estilo Spec Kit.
> **Versión:** 1.1.0 · **Fecha de ratificación:** 2026-06-06 · **Última enmienda:** 2026-06-09
> **Estado:** Vigente (ratificada). Enmienda menor sobre v1.0.0 — ver §Gobernanza → Historial de enmiendas.
> **Ámbito:** Aplica a TODOS los artefactos y a TODO el código del proyecto BuildCv — `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `tasks.md`, backend .NET, frontend Next.js, prompts de IA, copy público y documentos legales.
> **Idioma:** español (documentación) · identificadores de código en inglés.
```

**After**:

```markdown
# Constitución del Proyecto — BuildCv

> **Artefacto SDD:** `.specify/memory/constitution.md` — ley fundamental del proyecto al estilo Spec Kit.
> **Versión:** 1.2.0 · **Fecha de ratificación:** 2026-06-06 · **Última enmienda:** 2026-06-25
> **Estado:** Vigente (ratificada). Enmienda menor sobre v1.1.0 (014-constitution-v1.2.0) — ver §Gobernanza → Historial de enmiendas.
> **Ámbito:** Aplica a TODOS los artefactos y a TODO el código del proyecto BuildCv — `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `tasks.md`, backend .NET, frontend Next.js, prompts de IA, copy público y documentos legales.
> **Idioma:** español (documentación) · identificadores de código en inglés.
```

**Lines changed**: 2 lines modified in place (the `Versión…` blockquote line and the `Estado…` blockquote line). Title (line 1), Ámbito (line 6), Idioma (line 7) untouched. Total: 0 added / 2 modified / 0 deleted.

> **Note on line count**: spec.md reports this section as "1 added / 2 modified". The table below uses 2 modified because both `Versión…` and `Estado…` lines change in place.

### Section 2: Art. III — Privacidad primero

**Before** (find the `**Principio.**` paragraph that opens Art. III):

```markdown
## Artículo III — Privacidad primero y minimización de datos

**Principio.** El dato más seguro es el que no se guarda. v0 procesa en memoria; v0.5 (fase actual) admite persistencia local EXCLUSIVAMENTE en el dispositivo del usuario para soportar el flujo de edición de CV. v1.0 introducirá cuentas y persistencia server-side con consentimiento expreso (Habeas Data, Art. IX).

**Reglas.**
- En v0.5 (fase actual), el sistema **MUST** procesar el CV y la vacante en memoria del servidor. …
```

**After**:

```markdown
## Artículo III — Privacidad primero y minimización de datos

**Principio.** El dato más seguro es el que no se guarda. v0 procesa en memoria; v0.5 (fase actual) admite persistencia local EXCLUSIVAMENTE en el dispositivo del usuario para soportar el flujo de edición de CV. v1.0 introducirá cuentas y persistencia server-side con consentimiento expreso (Habeas Data, Art. IX).

> **v0/v1 boundary (added v1.2.0):**
> v0 procesa en memoria y NO persiste NADA server-side (no cuentas, no CV, no job content). v1 (introducido en 009-auth) PUEDE persistir identidad de usuario (email, name, OAuth provider ID) y balance/ledger de créditos bajo controles Habeas Data (Art. IX). **CV y job content NUNCA se persisten server-side, independientemente de la versión.** Ver `BuildCv.Application/Features/Auth/IUserDataStore` (009-auth) y `BuildCv.Domain/Credits/CreditLedgerEntry` (013-credit-consumption) para la superficie de persistencia v1.

**Reglas.**
- En v0.5 (fase actual), el sistema **MUST** procesar el CV y la vacante en memoria del servidor. …
```

**Lines changed**: 3 lines added (blank + 2 blockquote lines + blank separator, OR blank + 1 long blockquote line + blank = 3 depending on rendering). The Principio paragraph and Reglas section are untouched. Total: 3 added / 0 modified / 0 deleted.

### Section 3: Art. VI — Clean Architecture

**Before** (find the `**VI.**` paragraph that opens Art. VI):

```markdown
## Artículo VI — El backend demuestra .NET profesional (es portafolio)

**Principio.** El backend ES el portafolio estrella del dueño y debe ser **ejemplar**. Cada decisión de backend se juzga también por la señal de calidad técnica que envía a un evaluador senior en Colombia.

**Reglas.**
- El backend **MUST** estar construido en ASP.NET Core (C#, .NET) con una arquitectura limpia y defendible …
```

**After**:

```markdown
## Artículo VI — El backend demuestra .NET profesional (es portafolio)

**Principio.** El backend ES el portafolio estrella del dueño y debe ser **ejemplar**. Cada decisión de backend se juzga también por la señal de calidad técnica que envía a un evaluador senior en Colombia.

> **Approved external dependencies (added v1.2.0):**
> - **Backend** (shared utilities): `diff@^5`, `zod@^3`
> - **Frontend** (ratified 2026-06-25 by owner in 013.2-web-jwt-cookie, see `BuildCv-api/specs/013-credit-consumption-followups/013.2-web-jwt-cookie-design.md` §Art. VI Amendment): `web-vitals@^4`, `react-error-boundary@^5`, `next-auth@^4.24.7`
> - **`next-auth@^4.x` es la ÚNICA librería web-side de auth aprobada.** Futuras dependencias relacionadas con auth (sessions, OAuth, JWT en cliente, etc.) requieren enmienda constitucional explícita.

**Reglas.**
- El backend **MUST** estar construido en ASP.NET Core (C#, .NET) con una arquitectura limpia y defendible …
```

**Lines changed**: 5 lines added (blank separator + 4 blockquote lines + blank separator = 6, OR blank + 4 blockquote = 5). Total: 5 added / 0 modified / 0 deleted.

### Section 4: Art. VII — v0 lanzable sin fricción

**Before** (find the `**VII.**` paragraph that opens Art. VII):

```markdown
## Artículo VII — v0 lanzable sin fricción; entrega por hitos

**Principio.** Primero lanzar valor real, gratis y sin barreras. El alcance se entrega por hitos ordenados: **v0** (núcleo de valor) antes que **v0.5** (drafting local) antes que **v1** (cuentas, créditos, legal). Nada que no sea esencial para el núcleo bloquea el lanzamiento de v0.

**Reglas.**
- v0 **MUST** ser usable de principio a fin **sin crear cuenta ni iniciar sesión** y sin guardado *(FR-040, US-008)*.
```

**After**:

```markdown
## Artículo VII — v0 lanzable sin fricción; entrega por hitos

**Principio.** Primero lanzar valor real, gratis y sin barreras. El alcance se entrega por hitos ordenados: **v0** (núcleo de valor) antes que **v0.5** (drafting local) antes que **v1** (cuentas, créditos, legal). Nada que no sea esencial para el núcleo bloquea el lanzamiento de v0.

> **v0/v1 boundary (added v1.2.0):**
> - **v0 endpoints** (anónimos, sin auth requerida): `/api/v1/score`, `/api/v1/adapt`, `/api/v1/export`, `/api/v1/import`, `/api/v1/health/*`. Rate-limited por IP.
> - **v1 endpoints** (introducidos en 009-auth, requieren auth): `/api/v1/auth/*`, `/api/v1/user/*`, `/api/v1/payments/*`, `/api/v1/credits/*`. Rate-limited por usuario autenticado + IP fallback.
> - La frontera es **per-endpoint**, declarada vía middleware `RequireAuthorization()`. Ambas políticas de rate-limit (IP y user) aplican acumulativamente según el rol del endpoint.
> - **Migration note**: endpoints v0 pueden migrar a v1 en versiones futuras; la migración requiere una enmienda separada.

**Reglas.**
- v0 **MUST** ser usable de principio a fin **sin crear cuenta ni iniciar sesión** y sin guardado *(FR-040, US-008)*.
```

**Lines changed**: 6 lines added (blank separator + 5 blockquote lines = 6). Total: 6 added / 0 modified / 0 deleted.

### Section 5: Art. IX — Habeas Data (cross-reference, no normative text change)

**Before** (find the `**IX.**` paragraph that opens Art. IX):

```markdown
## Artículo IX — Cumplimiento Habeas Data al monetizar

**Principio.** Desde el momento en que el sistema guarda datos personales o cobra, opera bajo la ley colombiana de protección de datos (Habeas Data) con consentimiento informado y derechos del titular plenamente respetados. …

**Reglas.**
- Antes de prometer públicamente "retención cero / no entrenamiento" del proveedor de IA, el sistema **MUST** verificarlo contractualmente …
```

**After**:

```markdown
## Artículo IX — Cumplimiento Habeas Data al monetizar

**Principio.** Desde el momento en que el sistema guarda datos personales o cobra, opera bajo la ley colombiana de protección de datos (Habeas Data) con consentimiento informado y derechos del titular plenamente respetados. …

> **Implementation references (added v1.2.0):**
> - User identity persistence: `BuildCv.Application/Features/Auth/IUserDataStore` (009-auth)
> - Credit ledger: `BuildCv.Domain/Credits/CreditLedgerEntry` (013-credit-consumption)
> - ARCO anonymize pattern: `BuildCv.Application/Features/Auth/DeleteUserDataHandler.AnonymizeAsync` (013-credit-consumption)
> - Privacy policy v2: `BuildCv.Application/Features/Consent/PrivacyPolicyQueryHandler` (013-credit-consumption fix-verify-blockers)

**Reglas.**
- Antes de prometer públicamente "retención cero / no entrenamiento" del proveedor de IA, el sistema **MUST** verificarlo contractualmente …
```

**Lines changed**: 6 lines added (blank separator + 5 blockquote lines = 6). The Art. IX Principio, Reglas, Justificación text is **unchanged** — only an implementation-references blockquote is appended for trazabilidad. Total: 6 added / 0 modified / 0 deleted.

> **Important**: Art. IX is cross-reference only. NO normative wording changes. The rules stay exactly as v1.1.0 ratified them.

### Section 6: §Gobernanza — Historial de enmiendas

**Before** (existing table tail at lines 213–216):

```markdown
### Historial de enmiendas

| Versión | Fecha | Tipo | Resumen | PR / spec |
|---|---|---|---|---|
| **1.0.0** | 2026-06-06 | — | Ratificación inicial. Nueve artículos, sin persistencia server-side, ZDR pendiente. | — |
| **1.1.0** | 2026-06-09 | MENOR | (a) Art. III admite persistencia local EXCLUSIVAMENTE en dispositivo del usuario, con botón "Limpiar borrador" (FR-040a/b). (b) Art. I añade defense in depth en editor (FR-029a). (c) Art. VI lista `ICvParser` y `ICvStore` como puertos oficiales. (d) Art. VII introduce hito v0.5 (carga de archivos + editor) y la política de rate-limit `"import"` (30/h/IP). (e) Art. IX deja nota de estado del gate ZDR (Anthropic estándar, ZDR no garantizado). Sin cambio MAYOR ni eliminación de principios. | `specs/007-constitution-v1.1.0/` |
```

**After**:

```markdown
### Historial de enmiendas

| Versión | Fecha | Tipo | Resumen | PR / spec |
|---|---|---|---|---|
| **1.0.0** | 2026-06-06 | — | Ratificación inicial. Nueve artículos, sin persistencia server-side, ZDR pendiente. | — |
| **1.1.0** | 2026-06-09 | MENOR | (a) Art. III admite persistencia local EXCLUSIVAMENTE en dispositivo del usuario, con botón "Limpiar borrador" (FR-040a/b). (b) Art. I añade defense in depth en editor (FR-029a). (c) Art. VI lista `ICvParser` y `ICvStore` como puertos oficiales. (d) Art. VII introduce hito v0.5 (carga de archivos + editor) y la política de rate-limit `"import"` (30/h/IP). (e) Art. IX deja nota de estado del gate ZDR (Anthropic estándar, ZDR no garantizado). Sin cambio MAYOR ni eliminación de principios. | `specs/007-constitution-v1.1.0/` |
| **1.2.0** | 2026-06-25 | MENOR | (a) Art. III documenta v0/v1 boundary de persistencia (v0 nada; v1 identidad + ledger; CV/job nunca). (b) Art. VI ratifica `next-auth@^4.24.7` como ÚNICA librería web-side de auth aprobada. (c) Art. VII documenta v0/v1 auth boundary per-endpoint (v0 anónimos por IP; v1 autenticados por user+IP). (d) Art. IX cross-references a implementaciones (`IUserDataStore`, `CreditLedgerEntry`, `DeleteUserDataHandler.AnonymizeAsync`, `PrivacyPolicyQueryHandler`). Cierra 2 WARNINGs pre-existentes de 009-auth y 013.2-web-jwt-cookie verifies. Sin cambio MAYOR ni eliminación de principios. | `specs/014-constitution-v1.2.0/` |
```

**Lines changed**: 1 row appended to the table. Total: 1 added / 0 modified / 0 deleted.

### Bonus: Footer line (line 220)

**Before**:

```markdown
**Versión 1.1.0** · Ratificada el **2026-06-06** · Última enmienda **2026-06-09**.
```

**After**:

```markdown
**Versión 1.2.0** · Ratificada el **2026-06-06** · Última enmienda **2026-06-25**.
```

**Lines changed**: 1 line modified in place. This footer mirrors the header metadata. Total: 0 added / 1 modified / 0 deleted.

## Line count summary

| Section | Lines added | Lines modified | Lines deleted |
|---|---|---|---|
| Header (Section 1) | 0 | 2 | 0 |
| Footer (Section 6.b) | 0 | 1 | 0 |
| Art. III (Section 2) | 3 | 0 | 0 |
| Art. VI (Section 3) | 5 | 0 | 0 |
| Art. VII (Section 4) | 6 | 0 | 0 |
| Art. IX (Section 5) | 6 | 0 | 0 |
| §Gobernanza (Section 6) | 1 | 0 | 0 |
| **TOTAL constitution.md** | **21** | **3** | **0** |

> **Note on numbers**: spec.md estimated "~21 added / ~2 modified". This design refines to **21 added / 3 modified** because:
> - Section 1 (Header) has **2** modified lines (`Versión…` and `Estado…`), not 1 (Fecha alone).
> - Section 6 footer has **1** additional modified line (the trailing `**Versión X.Y.Z** · …` line on row 220 of constitution.md).
>
> Apply phase will run `git diff --stat` after the edit and verify `21 insertions / 3 modifications / 0 deletions` match the table above. Discrepancy of ±2 lines is acceptable (rendering of blank lines around blockquotes varies by editor).

## File changes (full apply surface)

| File | Action | Description |
|---|---|---|
| `BuildCv-api/.specify/memory/constitution.md` | Modify | The 6 sections above (~21 added / ~3 modified / 0 deleted). Bump version 1.1.0 → 1.2.0, date 2026-06-09 → 2026-06-25. |
| `BuildCv-api/.specify/memory/CONSTITUTION-README.md` | Modify | Add v1.2.0 row to the comparative table (Spec-Kit vs BuildCv). See spec.md line 60, precedent: 007-constitution-v1.1.0/contracts/constitution-diff.md §7. |
| `BuildCv-api/AGENTS.md` | Modify | Update header reference from "Constitución: ley suprema (v1.1.0)" to v1.2.0; update Art. VI column row that mentions approved external libs. |
| `BuildCv-web/AGENTS.md` | Modify | Update header reference from v1.1.0 → v1.2.0 (same as api side). |
| `BuildCv-api/specs/000-INDEX.md` | Modify | Promote 014 status from `[Spec] Pending design` to `[Design] Pending tasks`; refresh "Última actualización" line; refresh pending-steps bullet. |

**Total files touched**: 5 (1 governance artifact + 1 README + 2 AGENTS.md + 1 INDEX).

## Architecture decisions

### Decision: Bump semver MENOR (1.1.0 → 1.2.0), not PARCHE

**Choice**: Semver MENOR. New blockquote clarifications + 1 dep ratified (next-auth).

**Alternatives considered**:
- **PARCHE** (1.1.0 → 1.1.1): rejected because the change adds material content (5 new blockquote blocks totaling ~21 lines), not just typos or rewording.
- **MAYOR** (1.1.0 → 2.0.0): rejected because no existing principle is redefined or invalidated. All changes are documentary additions.

**Rationale**: §Gobernanza versionado semántico rule: "MENOR: se añade un nuevo artículo/principio o se amplía materialmente uno existente sin romper los demás." This amendment adds material content (v0/v1 boundaries, approved-deps list, implementation cross-refs) without breaking or redefining any existing rule. MENOR is correct.

### Decision: Blockquote notes under each article, not new sub-articles

**Choice**: Append `> **… (added v1.2.0):**` blockquotes immediately after the `**Principio.**` paragraph of each affected article.

**Alternatives considered**:
- **Edit the Reglas list in place**: rejected because it would force the Reglas section to grow monotonically; old versions of v1.1.0 wouldn't be recoverable from git blame without a reference. Blockquotes keep old text intact and make the amendment self-documenting (`(added v1.2.0)` marker).
- **Move Art. III to v0/v1 split into two separate articles**: rejected because the Constitution says new articles are a MAYOR bump; this is a clarification, not a structural split.

**Rationale**: Blockquotes preserve the original Principio and Reglas text as ratified in v1.1.0, mark the new content with `(added v1.2.0)`, and keep the diff purely additive. Any auditor reading v1.2.0 sees both the original wording AND the new clarification, with provenance.

### Decision: Single-commit apply strategy

**Choice**: 1 commit on `main` covering all 5 file changes (constitution.md + 4 supporting docs).

**Alternatives considered**:
- **Separate commits per file**: rejected — this is one logical amendment, not 5 independent changes. Splitting commits obscures the audit trail.
- **PR with multiple chained PRs**: rejected — change is small (~50 lines net), no risk of review overload; one PR is sufficient.

**Rationale**: §Gobernanza paso 4 (Registro) requires a single ratification event (proposal → spec → design → apply → verify → archive). One commit per file would imply 5 ratification events. Single commit preserves "amendment = one event" semantic.

### Decision: Keep Art. IX normative text unchanged, only add implementation references

**Choice**: Art. IX gets only an implementation-references blockquote; its Principio / Reglas / Justificación text is untouched.

**Alternatives considered**:
- **Rewrite Art. IX to reflect v1 reality**: rejected because Art. IX was already ratified as forward-looking ("En v1, antes de recolectar…"); rewriting would be MAYOR.
- **Remove Art. IX entirely**: rejected — Art. IX is the only normative anchor for Habeas Data compliance; removing it would be a regression.

**Rationale**: Cross-references give auditors a direct path from constitutional rule → implementation file without altering the rule itself. This is the lowest-risk form of trazabilidad.

## Apply strategy

**Single commit on `main`** with all 5 files:

```bash
# 1. Edit constitution.md (6 sections per diff above)
# 2. Edit CONSTITUTION-README.md (add v1.2.0 row)
# 3. Edit BuildCv-api/AGENTS.md (header + Art. VI col)
# 4. Edit BuildCv-web/AGENTS.md (header)
# 5. Edit specs/000-INDEX.md (status + fecha)

git add BuildCv-api/.specify/memory/constitution.md \
        BuildCv-api/.specify/memory/CONSTITUTION-README.md \
        BuildCv-api/AGENTS.md \
        BuildCv-web/AGENTS.md \
        BuildCv-api/specs/000-INDEX.md

git commit -m "docs(014): constitution v1.1.0 → v1.2.0 (MENOR)

- Art. III: v0/v1 persistence boundary (v0 nada; v1 identidad + ledger; CV/job nunca)
- Art. VI: ratify next-auth@^4.24.7 as ÚNICA auth lib web-side aprobada
- Art. VII: v0/v1 auth boundary per-endpoint (v0 anónimos, v1 autenticados)
- Art. IX: cross-references a IUserDataStore / CreditLedgerEntry / AnonymizeAsync / PrivacyPolicyQueryHandler
- §Gobernanza: append 1.2.0 row al historial

Closes 2 WARNINGs pre-existentes (009-auth, 013.2-web-jwt-cookie).
Zero code changes. ~21 lines added / ~3 modified / 0 deleted in constitution.md."

git tag 014-constitution-v1.2.0-v1.0
```

The change is purely additive (blockquote notes + 1 row in history + 1-line replacements on 3 lines). Zero risk of merge conflict (single maintainer, linear history on `main`).

## Verification strategy

| Check | Command | Expected |
|---|---|---|
| constitution.md line count | `git diff --stat BuildCv-api/.specify/memory/constitution.md` | `21 insertions, 3 modifications, 0 deletions` (or ±2 tolerance for blank-line rendering) |
| Affected files only | `git diff --name-only main~1` | Exactly 5 files: constitution.md, CONSTITUTION-README.md, both AGENTS.md, 000-INDEX.md |
| No code files touched | `git diff --name-only main~1 | grep -E '\\.(cs|ts|tsx|json|sql)$'` | Empty output |
| Version string updated | `grep -E 'Versión.*1\.2\.0\|v1\.2\.0' BuildCv-api/.specify/memory/constitution.md` | 3+ matches (header blockquote + footer line + §Gobernanza row) |
| next-auth ratified | `grep 'next-auth@\\^4\\.24\\.7' BuildCv-api/.specify/memory/constitution.md` | 1+ matches |
| Existing tests still pass | `dotnet test` in `BuildCv-api/` | 1454/1454 passing (zero regressions, governance-only change) |
| Existing tests still pass | `pnpm test` in `BuildCv-web/` | All passing |
| Build clean | `dotnet build BuildCv.slnx -c Release` | 0 warnings (warnings-as-errors) |
| Build clean | `pnpm build` in `BuildCv-web/` | Success |
| Lint clean | `dotnet format --verify-no-changes` | No formatting drift |
| Lint clean | `pnpm lint` in `BuildCv-web/` | 0 errors |
| INDEX updated | `grep '014' BuildCv-api/specs/000-INDEX.md` | Status reflects `[Design] Pending tasks` |
| Owner sign-off | PR review | Required per §Gobernanza paso 3 |

## Compliance

- **Art. I–IX**: N/A for behavioral change. Art. III / VI / VII / IX are *clarified* (additive), not modified. No rule is broken.
- **§Gobernanza process**: ✅ proposal (`proposal.md`, 103 lines) + spec (`spec.md`, 289 lines) + design (this file) + apply (1 commit) + verify (sdd-verify) + archive (sdd-archive).
- **Owner approval**: Required per §Gobernanza paso 3. PR review serves as the sign-off mechanism.
- **Semver rules**: ✅ MENOR bump per §Gobernanza versionado semántico rule.
- **Date rules**: ✅ Ratificación original (2026-06-06) NO cambia. Última enmienda 2026-06-09 → 2026-06-25.

## Out of scope

- No new articles (Art. X, XI, etc.) — this is a clarification, not an expansion.
- No breaking changes to existing articles — everything is ADDED (blockquote under existing Principio).
- No changes to the §Gobernanza process itself.
- No new governance rules (no voting, no multi-owner, no approval workflows).
- No refactor of code — governance-only.
- No new tests — no behavior change.

## Risk assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Wrong line count after apply | Low | sdd-verify runs `git diff --stat` and compares to the table in this design. ±2 line tolerance for blank-line rendering. |
| Owner rejects amendment | Low | The amendment closes 2 pre-existing WARNINGs and ratifies a verbal approval already given during 013.2 SHIP. Risk of rejection is minimal. |
| INDEX status drift | Low | INDEX is updated as part of the same commit; no orphan references. |
| Future contributors unaware of next-auth ratification | Low | The Art. VI blockquote explicitly marks `next-auth@^4.x` as the ONLY web-side auth lib. Future auth-related deps will trigger a `constitution-check.sh` (when implemented) failure. |
| Constitution v1.2.0 vs v1.1.0 split-brain across docs | Medium | 5 files updated in single commit: constitution.md, CONSTITUTION-README.md, both AGENTS.md, INDEX. sdd-verify checks all 5 reference v1.2.0. |

## Open questions

None. All decisions documented in:
- `BuildCv-api/specs/014-constitution-v1.2.0/proposal.md` (intent + impact)
- `BuildCv-api/specs/014-constitution-v1.2.0/spec.md` (per-article changelog + acceptance criteria)
- `BuildCv-api/specs/013-credit-consumption-followups/013.2-web-jwt-cookie-design.md` (Art. VI Amendment — source of next-auth ratification)
- `BuildCv-api/specs/013-credit-consumption-followups/013.2-web-jwt-cookie-verify-report.md` (WARNING #1 — Art. VI)
- `BuildCv-api/specs/009-auth/verify-report.md` (WARNING #2 — Art. III/VII boundary)

## Next

`sdd-tasks` → single task: edit `constitution.md` per Sections 1–6 + apply 4 supporting doc updates in 1 commit.
