# Tasks: 014-constitution-v1.2.0

## Status

[Tasks] — Ready to apply (1 commit, 5 files)

## Review workload forecast

- **Total estimated diff**: ~50 lines (5 files)
- **400-line budget risk**: NONE (governance change, well under budget)
- **Chained PRs recommended**: No (single atomic amendment)
- **Strategy**: 1 commit on main, direct merge
- **Files modified**: 5
- **Files created**: 0

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

> The `size-exception` chain strategy line above is a sentinel — it is **not** an active recommendation, just a contract-field placeholder. This change is a governance amendment (single commit), not a chained PR. See "Apply strategy (locked)" below for the actual strategy.

## Apply strategy (locked)

Single commit on `main`:

- **Message**: `docs(014): constitution v1.1.0 → v1.2.0 (MENOR) — next-auth ratification + v0/v1 boundaries`
- **Files**: 5 modified, 0 created
- **Branch**: `main` only (no feature branches, no chained PRs)
- **Review**: owner sign-off per §Gobernanza paso 3 (PR review serves as ratification)

### Why single commit (not chained)

- Per §Gobernanza paso 4 (Registro), a constitutional amendment is **one ratification event**, not five. Splitting into 5 commits would imply 5 ratification events — wrong semantic.
- Total diff is ~50 lines across 5 files, well under the 400-line review budget. Chained PRs add coordination overhead with no review-load benefit.
- All changes are documentary (blockquotes, version strings, history row). Zero risk of merge conflict on `main` (single maintainer, linear history).
- §Gobernanza process enforces ONE event: proposal → spec → design → apply (1 commit) → verify → archive.

## Task list (5 tasks)

### T1 — Edit constitution.md (Header + Art. III + Art. VI + Art. VII + Art. IX + §Gobernanza)

**File**: `BuildCv-api/.specify/memory/constitution.md`

**Changes** (literal markdown diffs are in `design.md` §Sections 1–6 — copy each block as written):

| Section | Lines | Action |
|---|---|---|
| Section 1: Header (line 4) | 1 line modified | `**Versión:** 1.1.0` → `**Versión:** 1.2.0` (header blockquote, line 4) |
| Section 1: Header (line 5) | 1 line modified | `**Estado:** … Enmienda menor sobre v1.0.0` → `**Estado:** … Enmienda menor sobre v1.1.0 (014-constitution-v1.2.0)` |
| Section 2: Art. III | 3 lines added | Append `> **v0/v1 boundary (added v1.2.0):**` blockquote after the `**Principio.**` paragraph of Art. III, before `**Reglas.**` |
| Section 3: Art. VI | 5 lines added | Append `> **Approved external dependencies (added v1.2.0):**` blockquote after the `**Principio.**` paragraph of Art. VI, before `**Reglas.**` (ratifies `next-auth@^4.24.7`) |
| Section 4: Art. VII | 6 lines added | Append `> **v0/v1 boundary (added v1.2.0):**` blockquote after the `**Principio.**` paragraph of Art. VII, before `**Reglas.**` (documents v0/v1 endpoint boundary) |
| Section 5: Art. IX | 6 lines added | Append `> **Implementation references (added v1.2.0):**` blockquote after the `**Principio.**` paragraph of Art. IX, before `**Reglas.**` (cross-refs only — **no normative text change**) |
| Section 6.a: §Gobernanza history | 1 row appended | Add `\| **1.2.0** \| 2026-06-25 \| MENOR \| … \|` row to the "Historial de enmiendas" table |
| Section 6.b: Footer (line 220) | 1 line modified | `**Versión 1.1.0** · Ratificada el **2026-06-06** · Última enmienda **2026-06-09**.` → `**Versión 1.2.0** · Ratificada el **2026-06-06** · Última enmienda **2026-06-25**.` |

**Estimated lines**: +21 / ~3 / 0 (matches `design.md` line count summary table)

**Verifiable**:
- `git diff --stat BuildCv-api/.specify/memory/constitution.md` shows `21 insertions, 3 modifications, 0 deletions` (or ±2 line tolerance for blank-line rendering).
- `grep 'next-auth@\\^4\\.24\\.7' BuildCv-api/.specify/memory/constitution.md` returns ≥1 match.
- `grep -E 'Versión.*1\.2\.0' BuildCv-api/.specify/memory/constitution.md` returns ≥3 matches (header blockquote + footer + §Gobernanza row).

### T2 — Skip CONSTITUTION-README.md (file does not exist)

**File**: `BuildCv-api/.specify/memory/CONSTITUTION-README.md`

**Action**: **SKIP** — `glob` confirms the file does not exist in the repo. The `spec.md` impact-declared table (line 60) and the `design.md` file-changes table (line 250) mention it as a "supporting doc", but the precedent is `007-constitution-v1.1.0` (which has `contracts/constitution-diff.md` instead). For 014, no README update is needed because no such file exists in v1.1.0 to update.

> **Note**: If a CONSTITUTION-README.md is later created (e.g., during a 015 follow-up), the v1.2.0 row should be added at that time. This is **out of scope** for 014.

### T3 — Update BuildCv-api/AGENTS.md

**File**: `BuildCv-api/AGENTS.md`

**Action**: Find any reference to "v1.1.0" in the constitution reference. Update to "v1.2.0".

**Specific edits**:
- Line 11: `## Constitución: ley suprema (v1.1.0)` → `## Constitución: ley suprema (v1.2.0)`

**Estimated lines**: 0 / 1 / 0 (1 line modified in place, in the section header)

**Verifiable**:
- `grep -E 'ley suprema.*v1\.2\.0' BuildCv-api/AGENTS.md` returns 1 match.
- No remaining `v1.1.0` reference in the constitution header line.

### T4 — Update BuildCv-web/AGENTS.md (cross-repo reference)

**File**: `BuildCv-web/AGENTS.md`

**Action**: Find any reference to the constitution version. Update to "v1.2.0" if found.

**Specific edits**:
- Line 5: `viven en \`../BuildCv-api/.specify/memory/constitution.md\`` — no version in the visible text. The web AGENTS.md does not currently show a version number for the constitution.
- **If no version reference is found in the web AGENTS.md, this task is a no-op** (no edit needed). The 013.2 archive report already established the cross-repo ratification pattern.

**Estimated lines**: 0 / 0 / 0 (no-op, OR 0 / 1 / 0 if a version reference is found and needs bumping)

**Verifiable**:
- `grep -nE 'constitution.*v1\.[0-9]+\.[0-9]+|v1\.[0-9]+\.[0-9]+.*constitution' BuildCv-web/AGENTS.md` is empty OR returns only `v1.2.0` matches.

### T5 — Update 000-INDEX.md

**File**: `BuildCv-api/specs/000-INDEX.md`

**Action**: Final update for the apply phase. Three sub-edits:

1. **Header (line 6)** — bump "Última actualización" date is **already 2026-06-25** from the design phase. Verify it is current; if not, refresh to `2026-06-25 (014-constitution-v1.2.0: **🟦 TASKS COMPLETE** — ...)`.
2. **Constitution table (line 12)** — update the `**1.1.0**` row's "Estado" column from `✅ Vigente` to `🗄️ Superada por v1.2.0`; add a new row `| **1.2.0** | 2026-06-25 | ✅ Vigente | [specs/014-constitution-v1.2.0/spec.md](./014-constitution-v1.2.0/spec.md) |` immediately after the v1.1.0 row.
3. **Row 40 (status table)** — update 014 status from `[Design] Pending tasks` to `[Tasks] Ready to apply` (or — if apply is the same commit — to `✅ RATIFICADA`).
4. **Line 237 (`### 014-constitution-v1.2.0` detail block)** — update the heading status from `[Design] Pending tasks` to `[Tasks] Ready to apply` (or ✅ RATIFICADA if apply is the same commit).
5. **Line 245 ("Pendiente")** — update the "Pendiente" line to reflect that `sdd-tasks` is now complete; remaining steps are `sdd-apply` → `sdd-verify` → `sdd-archive`.

**Estimated lines**: ~5 / ~3 / 0 (≈8 total edits across 3 sections)

**Verifiable**:
- `grep '014-constitution-v1.2.0' BuildCv-api/specs/000-INDEX.md` returns ≥4 matches (header + table row + status line + detail block).
- Constitution table now lists both v1.1.0 (🗄️ superada) and v1.2.0 (✅ vigente).

## Commit (single)

```bash
git add BuildCv-api/.specify/memory/constitution.md \
        BuildCv-api/AGENTS.md \
        BuildCv-web/AGENTS.md \
        BuildCv-api/specs/000-INDEX.md

git commit -m "docs(014): constitution v1.1.0 → v1.2.0 (MENOR) — next-auth ratification + v0/v1 boundaries"
```

> **Note on the commit message**: The format follows the project convention (conventional commits, Spanish, no AI attribution, single line). The design.md provides an expanded multi-line version with bullet points for documentation purposes; the apply phase can use either form (the body is informational, the title is what gets indexed).

**Optional tag after merge**:

```bash
git tag 014-constitution-v1.2.0-v1.0
```

## Critical execution order

1. **T1 first** — `constitution.md` is the primary artifact; everything else references it.
2. **T2-T4 in any order** — supporting docs; T2 is a no-op (file does not exist).
3. **T5 last** — INDEX finalization (after all other docs are updated, so the status reflects the full surface).

All five tasks execute in a single commit (no intermediate commits). The order above is the **editing** order within the commit, not the **commit** order.

## Conventions

- **Conventional commits**, Spanish messages, no AI attribution.
- **Single work-unit commit** (one commit for the whole amendment).
- **Branch**: only `main` (no feature branches).
- **Direct merge** to main.
- **No force-push**, no interactive rebase.
- **No commit signing ceremony** (matches 012-wompi / 013-credit-consumption precedent — owner sign-off happens at PR review).

## Out of scope

- No new articles (Art. X, XI, etc.) — clarification only.
- No code changes — governance-only.
- No new tests — no behavior change. 1454/1454 existing tests must still pass.
- No new dependencies — `next-auth@^4.24.7` was already ratified and shipped in 013.2.
- No changes to the §Gobernanza process itself.
- No CONSTITUTION-README.md creation (file does not exist in v1.1.0; out of scope for 014).

## Verification (for sdd-verify, not part of apply)

| Check | Command | Expected |
|---|---|---|
| Diff scope | `git diff --name-only main~1` | Exactly 4 files: constitution.md, AGENTS.md, AGENTS.md (web), 000-INDEX.md |
| No code touched | `git diff --name-only main~1 \| grep -E '\.(cs\|ts\|tsx\|json\|sql)$'` | Empty (or only `package.json` if Art. VI dep is moved into verified-position) |
| Version string | `grep -c '1\.2\.0' BuildCv-api/.specify/memory/constitution.md` | ≥3 matches |
| next-auth ratified | `grep 'next-auth@\\^4\\.24\\.7' BuildCv-api/.specify/memory/constitution.md` | ≥1 match |
| Backend tests | `dotnet test` in `BuildCv-api/` | 1454/1454 passing |
| Web tests | `pnpm test` in `BuildCv-web/` | All passing |
| Build clean | `dotnet build BuildCv.slnx -c Release` | 0 warnings |
| Build clean | `pnpm build` in `BuildCv-web/` | Success |
| Format | `dotnet format --verify-no-changes` | No drift |
| Lint | `pnpm lint` in `BuildCv-web/` | 0 errors |
| INDEX status | `grep '\[Tasks\] Ready to apply' BuildCv-api/specs/000-INDEX.md` | 1+ matches |

## Risks (top 3)

1. **Wrong line count after apply** — Severity: **Low**. Mitigation: `git diff --stat` compared to design.md line-count table; ±2 line tolerance for blank-line rendering.
2. **Split-brain across docs** — Severity: **Low**. Mitigation: 4 files updated in single commit; sdd-verify checks all 4 reference v1.2.0.
3. **Owner rejects amendment** — Severity: **Low**. Mitigation: amendment closes 2 pre-existing WARNINGs and ratifies a verbal approval already given during 013.2 SHIP.

## Next

`sdd-apply` → execute the 5 tasks in a single commit on `main`, then `sdd-verify` runs the verification matrix above.
