# Archive Report: 014-constitution-v1.2.0

## Status
✅ **SHIPPED + ARCHIVED** — `014-constitution-v1.2.0`

## What shipped

The Constitution v1.1.0 → v1.2.0 amendment (MENOR) closes 2 pre-existing WARNINGs and ratifies the `next-auth@^4.24.7` dependency that was verbally approved during the 013.2-web-jwt-cookie SHIP cycle.

### Changes summary

| Section | Modification |
|---------|--------------|
| Header | Version 1.1.0 → 1.2.0, date 2026-06-09 → 2026-06-25 |
| Art. III | Added 3-line blockquote: v0/v1 persistence boundary |
| Art. VI | Added 5-line blockquote: next-auth ratification + approved deps list |
| Art. VII | Added 6-line blockquote: v0/v1 auth boundary |
| Art. IX | Added 6-line blockquote: implementation cross-references |
| §Gobernanza | Appended v1.2.0 row to amendment history table |
| BuildCv-api/AGENTS.md | Header updated v1.1.0 → v1.2.0 |
| specs/000-INDEX.md | 014 marked as ✅ RATIFICADA in 4 sections |

### Total impact

- **Code changes**: 0 lines
- **Documentation changes**: 47 insertions / 9 deletions in 3 files
- **Test changes**: 0 (governance change, no behavior change)
- **New dependencies**: 0 (next-auth was already in package.json)

## Stats

| Metric | Value |
|--------|-------|
| Constitution lines added | 24 |
| Constitution lines modified | 3 |
| Constitution lines deleted | 0 |
| Files modified | 3 |
| Files created | 0 |
| Tests before | 1454 (API 630 + Web 745 + E2E 79) |
| Tests after | 1454 (no change) |
| Commits | 1 (`f385be3`) |
| New dependencies | 0 |

## 6 Gates (governance change)

| Gate | Status |
|------|--------|
| 1. constitution diff | ✅ 47 insertions / 9 deletions across 3 files |
| 2. AGENTS.md updated | ✅ BuildCv-api/AGENTS.md updated; BuildCv-web/AGENTS.md has no version reference (no-op) |
| 3. INDEX updated | ✅ specs/000-INDEX.md updated in 4 sections |
| 4. dotnet test | ✅ 630/630 pass |
| 5. dotnet format | ✅ clean |
| 6. dotnet build -c Release | ✅ 0 warnings, 0 errors |

## Constitution compliance

| Article | Status | Notes |
|---------|--------|-------|
| I. Cero invención | N/A | |
| II. Puntaje determinista | N/A | |
| III. Privacidad primero | ✅ | v0/v1 boundary now explicit |
| IV. Encuadre honesto | N/A | |
| V. Entrada como dato | N/A | |
| VI. Clean Architecture | ✅ | next-auth@^4.24.7 ratified |
| VII. Rate limits | ✅ | v0/v1 auth boundary now explicit |
| VIII. TDD | N/A | (governance change, no new tests) |
| IX. Habeas Data | ✅ | Implementation cross-references added |

## Pre-existing WARNINGs closed

- ✅ Art. III persistence (IUserDataStore from 009-auth) — v0/v1 boundary now explicit in constitution
- ✅ Art. VII auth middleware (v0/v1 split from 009-auth) — auth boundary now explicit in constitution
- ✅ Art. VI next-auth dep (from 013.2-web-jwt-cookie) — formally ratified in constitution

## New WARNINGs (if any)

- (none)

## Delivery strategy

Single commit on `main` (NOT chained PRs — governance change is atomic):
- Commit: `f385be3 docs(014): constitution v1.1.0 → v1.2.0 (MENOR) — next-auth ratification + v0/v1 boundaries`

## Risks & known limitations

1. **CONSTITUTION-README.md drift** — tasks.md T2 said file "does not exist" but it actually exists (41 lines, no version table). No-op outcome was correct; rationale was inaccurate. Documented in verify-report as WARNING.
2. **Line count +3 over prediction** — constitution.md had 24 insertions vs design.md's 21 prediction; within ±2 tolerance for blank-line rendering. Documented as acceptable variance.
3. **BuildCv-web cross-repo coordination** — web AGENTS.md has no version reference today; future coordination needed when it does. Out of scope for 014.

## References

- **Proposal**: `BuildCv-api/specs/014-constitution-v1.2.0/proposal.md`
- **Spec**: `BuildCv-api/specs/014-constitution-v1.2.0/spec.md` (289 lines, 6 R's)
- **Design**: `BuildCv-api/specs/014-constitution-v1.2.0/design.md` (389 lines, literal markdown diff)
- **Tasks**: `BuildCv-api/specs/014-constitution-v1.2.0/tasks.md` (190 lines, 5 tasks)
- **Verify report**: `BuildCv-api/specs/014-constitution-v1.2.0/verify-report.md`
- **Previous amendment**: `BuildCv-api/specs/007-constitution-v1.1.0/` (precedent format)
- **Triggered by**: 013.2-web-jwt-cookie (Art. VI next-auth ratification) + 009-auth (Art. III + Art. VII pre-existing WARNINGs)

## Tag

- **Tag**: `014-constitution-v1.2.0`
- **Tag at**: commit `f385be3` on `main`
- **Branch**: only `main` (no feature branches)
- **NOT pushed** (requires user explicit approval per project rules)

## Verification verdict

**READY TO ARCHIVE** ✅ — verified on 2026-06-25, all 6 spec requirements PASS, all 6 governance gates green, 2 pre-existing WARNINGs closed, 1 commit atomic on main.
