# INDEX — Registro consolidado de features (BuildCv-api)

> **Este archivo es el entry point oficial al estado del producto BuildCv.**
> Cualquier agente o humano que necesite saber "qué está hecho, qué está en curso, qué falta" debe leer esto primero.

**Última actualización:** 2026-06-26 (**021-structured-cv-import-and-job-input**: **✅ SHIPPED + ARCHIVED** — Engine bump MAYOR `1.0.0 → 2.0.0` (Constitution Art. II SemVer seal, nota agregada en `constitution.md`); JSON Resume `CvDocument` + `ConfidenceMarker` en parsers (`PdfPigCvParser`/`OpenXmlCvParser` → `StructuredParseResult` con `CvDocument` + `confidence` markers); `JobSpec` mandatory con FluentValidation (anti-prompt-injection: control chars + zero-width + `ignore previous`/`system:`/`assistant:`); `ScoringEngine.Version` bump MAYOR `1.0.0 → 2.0.0` con `perSection: {experience, education, skills, certifications, contact}` + `redFlags[]` (employment gaps >6mo + job-hopping ≥3 <18mo/5y); discriminated-union `ScoreCvCommand` con backward-compat shim v1 (`VERSION_MISMATCH` 422 si se mezcla structured cv con engineVersion v1); determinism property test (1000 iter + parallel byte-identical, `ScoringEngineDeterminismPropertyTests`); 11 work-unit commits api (`5f3982a` PR1 → `6f0456f` PR6d) + 23 work-unit commits web (`30672b4` PR1 → `b75c5b1` PR6d + `55985f3` followups-1 + typo fix `82a400b`) + 2 merge commits; delta specs synced into `BuildCv-api/specs/{002-score-engine,005-cv-pdf-docx-import}/spec.md` + `BuildCv-web/specs/{006-web-cv-editor,008-observability-web}/spec.md`; tag `021-structured-cv-import-and-job-input-v1.0` at HEAD `5d40a53` (api) / `82a400b` (web) — NOT pushed by archive per project rules; see [archive report](./021-structured-cv-import-and-job-input/archive-report.md)) · 016-subscription-recurring: **✅ SHIPPED + ARCHIVED** — monthly recurring credit subscriptions via Wompi `payment_sources` + scheduled charges shipped via 3 chained PRs on `main` + 4 web work-unit commits: PR1 Domain+Application (4 commits), PR2 Infrastructure+DB (8 commits), PR3 API+rate limits (3 commits); 19 work-unit commits total (15 API + 4 Web); +123 tests over +43 forecast (29 unit + 66 integration + 7 API integration + 6 Playwright + 15 web unit); 5390 insertions / 42 deletions across 62 files (2001 prod + 2328 test + 1060 web); 7 R's PASS (R1 domain+state machine, R2 subscribe endpoint, R3 recurring webhook handler, R4 status GET, R6 retry handler, R7 reconciliation worker, R9 feature flag) + 3 R's WARNING deferred to 017 (W1 R5 cancel idempotency, W2 R8 ARCO Wompi pre-cancel, W3 R10 privacy policy v3); 6/6 gates green (lint + typecheck + test 834/834 API + 760/760 Web + e2e 85/85 + build + constitution-check); 011/012/013/014/015 backward compat preserved (336 baseline tests rerun unchanged); 0 new NuGet deps; tag `016-subscription-recurring-v1.0` at commit `c49cbc9`. See [archive report](./016-subscription-recurring/archive-report.md)) · **018-cv-iteration-loop**: **✅ SHIPPED + ARCHIVED** — best-of-N CV iteration loop with probability warning; reuses 002/003/013 + extends `AdaptCvCommand` (additive `Seed`) + `PromptBuilder` (additive `iterationSeed`); 11 R's PASS (R1 POST endpoint, R2 probability warning, R3 Art. I exclusion, R4 credit debit-before-loop via `ICreditLedger.AccreditAsync`, R5 idempotency TTL 24h, R6 timeout 30s/5min, R7 sequential, R8 `requestId` seeding via `{RequestId}:{i}`, R9 GET endpoint, R10 CV source reuse, R11 warning UI amber/red/hidden bands); 2 endpoints (POST/GET `/api/v1/adapt/iterate`) + new `"iterate"` rate-limit policy 10/h per IP (stricter than `"ai"` 5/h × N consumed); 2 ports (`IIterationService`, `IIterationStore`); `/analizar/iterate` page + 5 components (progress + settings + result-card + step-list + probability-warning) + 2 BFF routes; CV_generator integration v1 = manual upload (paste Markdown or upload PDF/DOCX via 005), v2 = webhook deferred; 3 chained PRs + 2 followup batches (followups-1 closed R6+R8 CRITICAL + 5 WARNINGs; followups-2 closed EF migration drift on `partial` column); 24 work-unit commits on `main` (17 API + 7 Web); +113 tests over +48 forecast (85 API + 21 Web unit + 7 Web e2e); ~1900 insertions / ~50 deletions; 0 new NuGet deps; 2 EF migrations (`20260625212735_AddIterationResults` + `20260625224658_AddPartialToIterationResults`); 6/6 gates green (lint + typecheck + test 925/925 API + 781/781 Web + e2e 92/92 + build + constitution-check); 8/9 articles compliant + 1 Art. IV WARNING deferred to v1.5 (`ProbabilityWarning` is `string?` instead of structured record; 3 other minor WARNINGs deferred: `EngineVersion` sealing, `IterationStep.Severity` field); 002/003/005/009/010/011/012/013/014/015/016/017 backward compat preserved; tag `018-cv-iteration-loop-v1.0` at commit `a58c673`. See [archive report](./018-cv-iteration-loop/archive-report.md))

## Constitución vigente

| Versión | Fecha | Estado | Diff |
|---|---|---|---|
| **1.2.0** | 2026-06-25 | ✅ Vigente | [specs/014-constitution-v1.2.0/spec.md](./014-constitution-v1.2.0/spec.md) |
| **1.1.0** | 2026-06-09 | 🗄️ Superada por v1.2.0 | [specs/007-constitution-v1.1.0/contracts/constitution-diff.md](./007-constitution-v1.1.0/contracts/constitution-diff.md) |
| 1.0.0 | 2026-06-06 | 🗄️ Superada por v1.1.0 | backup en `.specify/memory/constitution.md.orig` |

**Cambios clave de v1.1.0:**
- **Art. III**: persistencia local EXCLUSIVAMENTE en dispositivo del usuario (frontend `ICvStore`), botón "Limpiar borrador" obligatorio (FR-040a/b). v0.5 introduce carga de archivos.
- **Art. I**: el editor frontend (006) NO agrega entidades que el usuario no haya tipeado; Zod rechaza nuevas entidades en round-trip (FR-029a).
- **Art. VI**: nuevos puertos `ICvParser` (PDF/DOCX server-side) y `ICvStore` (localStorage frontend).
- **Art. VII**: nueva política de rate-limit `"import"` 30/h por IP. v0.5 es un nuevo hito entre v0 y v1.
- **Art. IX**: nota de estado del gate ZDR (Anthropic estándar → ZDR NO garantizado).

## Estado del producto (consolidado)

| # | Feature | Hito | Status | Branch | Engine version |
|---|---|---|---|---|---|
| 001 | `mvp-cv-ats` | MVP original | 🗄️ Archivado | `main` | — |
| 002 | `score-engine` | v0 / M0 | ✅ SHIPPED | `main` | `1.0.0` |
| 003 | `adapt-ia` | v0 / M1 | ✅ SHIPPED (StubAiClient) | `main` | `1.0.0` |
| 004 | `export-pdf` | v0 / M2 | ✅ SHIPPED (QuestPDF) | `main` | `1.0.0` |
| 005 | `cv-pdf-docx-import` | v0.5 / M3 | ✅ SHIPPED | `main` | `1.0.0` (parser) |
| 006 | `cv-editor` (frontend only, ver [006-cv-editor/](./006-cv-editor/)) | v0.5 / M4 | ✅ SHIPPED (`BuildCv-web`) | — | `0.5.0` (editor) |
| 007 | `constitution-v1.1.0` | governance | ✅ RATIFICADA | `main` | — |
| 008 | `observability` | v0.5.1 | ✅ SHIPPED | `main` | — |
| 009 | `auth` | v1 | ✅ SHIPPED (47 tasks, 290 tests) | `main` | — |
| 010 | `persistence` | v1 | ✅ SHIPPED (38 tasks, 342 tests) | `main` | — |
| 011 | `factus` | v1 | ✅ SHIPPED (DIAN invoicing, opcional) | `main` | — |
| 012 | `wompi` | v1 | ✅ SHIPPED (Wompi payment gateway, 3 chained PRs + warning-fix) | `main` | — |
| 013 | `credit-consumption` | v1 | ✅ SHIPPED + ARCHIVED (credit ledger closes the v1 monetization loop; webhook→invoice→ledger in one tx; 1-credit `RequireCredits` filter on `/adapt`; ARCO anonymize + cascade ledger + KEEP payments/invoices; 3 chained PRs; tag `013-credit-consumption-v1.0`) | `main` | — |
| 013.2 | `web-jwt-cookie` | v1 | ✅ SHIPPED + ARCHIVED (closes auth flow gap; NextAuth.js integration; 3 chained PRs; tag `013.2-web-jwt-cookie-v1.0`) | `main` | — |
| 014 | `constitution-v1.2.0` | governance | ✅ SHIPPED + ARCHIVED (enmienda MENOR 1.1.0 → 1.2.0: ratifies `next-auth@^4.24.7` + documents v0/v1 boundary; zero code, minimal docs; sdd-apply done en 1 commit `f385be3` sobre `main`: 3 archivos modificados (constitution.md, AGENTS.md, 000-INDEX.md) + T2 skip (CONSTITUTION-README.md no existe) + T4 no-op (web AGENTS.md sin v1.1.0 ref); sdd-verify PASS 6/6 gates; sdd-archive complete; tag `014-constitution-v1.2.0`) | `main` | — |
| 015 | `feature-flags` | v1 | ✅ SHIPPED + ARCHIVED (centralized feature flag management with hybrid storage appsettings+DB, `CachingFeatureFlagDecorator` (60s TTL + `Invalidate(name)`), append-only audit log with keyset pagination, admin API at `/api/v1/admin/feature-flags/*` with `admin` role + 30/min/IP rate limit; migrates 3 existing flags (Factus:Enabled, Wompi:Enabled, Credits:Enabled) into unified `IFeatureFlag` port via 3 backward-compat adapters; 3 chained PRs (PR1 Domain+App / PR2 Infrastructure+DB / PR3 API), 15 work-unit commits on `main`, +102 tests (forecast was +47, exceeded 3×), 4072 insertions / 2 deletions across 56 files, 0 new NuGet deps; 011/012/013 backward compat verified (test suites green unchanged); 7 R's PASS + 6/6 gates green; tag `015-feature-flags-v1.0` at commit `986e53e`) | `main` | — |
| 016 | `subscription-recurring` | v1 | ✅ SHIPPED + ARCHIVED (monthly recurring billing via Wompi; 3 chained PRs; tag `016-subscription-recurring-v1.0`) | `main` | — |
| 017 | `subscription-followups` | v1 | ✅ SHIPPED + ARCHIVED (3 WARNINGs closed by historical commits caaaf35 + cf958ec + 5f8db66; 18 tests passing; docs catch-up committed; see [archive report](./017-subscription-followups/archive-report.md)) | `main` | — |
| 018 | `cv-iteration-loop` | v1 | ✅ SHIPPED + ARCHIVED (best-of-N iteration loop with probability warning; 3 chained PRs + 2 followup batches; tag `018-cv-iteration-loop-v1.0`) | `main` | — |
| 021 | `structured-cv-import-and-job-input` (cross-repo: web frontend + api backend) | v0.5.3 | ✅ **SHIPPED + ARCHIVED** (JSON Resume `CvDocument` + `ConfidenceMarker`; `JobSpec` mandatory con Zod web + FluentValidation api; `ScoringEngine.Version` bump MAYOR `1.0.0 → 2.0.0` Art. II SemVer seal; `perSection: {experience, education, skills, certifications, contact}` + `redFlags[]`; discriminated-union `ScoreCvCommand` con backward-compat shim v1; editor open-resume-inspired; `promoteConfidence` solo en editor on blur Art. I; feature flag `NEXT_PUBLIC_STRUCTURED_INPUT=true` default; 6 chained PRs + 1 followup; 23 work-unit commits web + 11 work-unit commits api all merged to `main` + pushed to `origin`; determinism property test 1000 iter + parallel byte-identical; tag `021-structured-cv-import-and-job-input-v1.0` at HEAD `5d40a53` api / `82a400b` web) | `main` | `2.0.0` (bumped from `1.0.0`; v1 path retained via `engineVersion: "1.0.0"` for one release cycle) |

## Leyenda de status

- ✅ **SHIPPED** — feature cerrada, en producción, tests pasando
- 🚧 **EN CURSO** — implementación activa
- 📋 **PLANEADO** — los 7 artifacts están escritos; esperando ventana de implementación
- 📝 **SPEC COMPLETE** — `proposal.md` + `spec.md` escritos; pendiente `sdd-design` → `sdd-tasks` → `sdd-apply` → `sdd-verify` → `sdd-archive`
- 🔵 **PROPOSAL COMPLETE** — solo `proposal.md` está escrito; pendiente `sdd-spec` → `sdd-design` → `sdd-tasks` → `sdd-apply`
- 🗄️ **ARCHIVADO** — feature antigua, conservada solo para historia

> **Convención de numeración cross-repo:** Los números 002–005 son correlativos (tienen contraparte frontend+backend = mismo producto). A partir de 006, cada repo (`BuildCv-api` / `BuildCv-web`) tiene su propia secuencia para features independientes. Ver también `BuildCv-web/specs/000-INDEX.md`.

## Features SHIPPED (detalle)

### 002-score-engine (v0 / M0)

- **Spec:** [specs/002-score-engine/spec.md](./002-score-engine/spec.md)
- **Plan:** [specs/002-score-engine/plan.md](./002-score-engine/plan.md)
- **Research:** [specs/002-score-engine/research.md](./002-score-engine/research.md)
- **Data model:** [specs/002-score-engine/data-model.md](./002-score-engine/data-model.md)
- **Quickstart:** [specs/002-score-engine/quickstart.md](./002-score-engine/quickstart.md)
- **Tasks:** [specs/002-score-engine/tasks.md](./002-score-engine/tasks.md)
- **Contracts:** [specs/002-score-engine/contracts/score-api.md](./002-score-engine/contracts/score-api.md)
- **Endpoint:** `POST /api/v1/score` (rate-limited 60/min por IP, política `"score"`)
- **Engine version:** `1.0.0`
- **Constitution compliance:** Art. II ✅, Art. VI ✅, Art. VIII ✅
- **Tests:** 31 total (motor determinista, suite verificada)
- **Commit:** `eded372` "BuildCv API (.NET 10) — motor de puntaje determinista" + `b37498d` (archival) + `9d17af3` (INDEX + artifacts)

### 003-adapt-ia (v0 / M1)

- **Spec:** [specs/003-adapt-ia/spec.md](./003-adapt-ia/spec.md)
- **Plan:** [specs/003-adapt-ia/plan.md](./003-adapt-ia/plan.md)
- **Research:** [specs/003-adapt-ia/research.md](./003-adapt-ia/research.md)
- **Data model:** [specs/003-adapt-ia/data-model.md](./003-adapt-ia/data-model.md)
- **Quickstart:** [specs/003-adapt-ia/quickstart.md](./003-adapt-ia/quickstart.md)
- **Tasks:** [specs/003-adapt-ia/tasks.md](./003-adapt-ia/tasks.md)
- **Contracts:** [specs/003-adapt-ia/contracts/adapt-api.md](./003-adapt-ia/contracts/adapt-api.md)
- **Endpoint:** `POST /api/v1/adapt` (rate-limited 5/h por IP, política "ai")
- **Engine version:** `1.0.0`
- **Status:** v0 usa `StubAiClient` (deterministic, sin LLM real, 0 costo). M1 habilitará `AnthropicAiClient` con Claude Sonnet 4 (gate Art. IX — ZDR contractual, NO verificado a la fecha de v1.1.0).
- **Constitution compliance:** Art. I ✅ (CrossEntityValidator detecta invenciones), Art. V ✅ (PromptBuilder con bloques `<DATA nonce="...">`), Art. VI ✅, Art. VII ✅
- **Tests:** 40 total (validation cascade + adapt pipeline, suite verificada)
- **Commit:** `68baaf2` "feat(003-adapt-ia): adaptación con LLM, cero invención (Constitution Art. I)"

### 004-export-pdf (v0 / M2)

- **Spec:** [specs/004-export-pdf/spec.md](./004-export-pdf/spec.md)
- **Plan:** [specs/004-export-pdf/plan.md](./004-export-pdf/plan.md)
- **Research:** [specs/004-export-pdf/research.md](./004-export-pdf/research.md)
- **Data model:** [specs/004-export-pdf/data-model.md](./004-export-pdf/data-model.md)
- **Quickstart:** [specs/004-export-pdf/quickstart.md](./004-export-pdf/quickstart.md)
- **Tasks:** [specs/004-export-pdf/tasks.md](./004-export-pdf/tasks.md)
- **Contracts:** [specs/004-export-pdf/contracts/export-api.md](./004-export-pdf/contracts/export-api.md)
- **Endpoint:** `POST /api/v1/export` (rate-limited 20/h por IP, política "export")
- **Engine version:** `1.0.0` (ScoreEngine), `004-export-pdf` (PdfMetadata.ModelVersion)
- **Status:** QuestPDF con Community License, layout con header/content/footer, marca de agua honesta "No es un puntaje ATS oficial".
- **Constitution compliance:** Art. I ✅ (ValidationGate bloquea Hard invenciones con 422), Art. III ✅ (PDF en memoria, sin persistencia), Art. IV ✅ (filename "cv-adapted-", watermark honesto), Art. VI ✅, Art. VII ✅
- **Tests:** 16 total (`QuestPdfGenerator` unit + integration, suite verificada; e2e suite pendiente verificación cuantitativa — MEDIUM)
- **Commit:** `635d688` "feat(004-export-pdf): export CV adaptado a PDF (Constitution Art. I, IV)"

### 005-cv-pdf-docx-import (v0.5 / M3)

- **Spec:** [specs/005-cv-pdf-docx-import/spec.md](./005-cv-pdf-docx-import/spec.md)
- **Plan:** [specs/005-cv-pdf-docx-import/plan.md](./005-cv-pdf-docx-import/plan.md)
- **Research:** [specs/005-cv-pdf-docx-import/research.md](./005-cv-pdf-docx-import/research.md)
- **Data model:** [specs/005-cv-pdf-docx-import/data-model.md](./005-cv-pdf-docx-import/data-model.md)
- **Quickstart:** [specs/005-cv-pdf-docx-import/quickstart.md](./005-cv-pdf-docx-import/quickstart.md)
- **Tasks:** [specs/005-cv-pdf-docx-import/tasks.md](./005-cv-pdf-docx-import/tasks.md)
- **Contracts:** [specs/005-cv-pdf-docx-import/contracts/import-api.md](./005-cv-pdf-docx-import/contracts/import-api.md)
- **Endpoint:** `POST /api/v1/import` (rate-limited 30/h por IP, política `"import"`, NUEVA per v1.1.0)
- **Engine version:** `1.0.0` (parser adapter)
- **Status:** PdfPig (Apache-2.0) para PDF + DocumentFormat.OpenXml (MIT) para DOCX, server-side parsing, multipart 5 MB, validación dual (header + magic bytes), output `{ text, sections[], warnings[], engineVersion, traceId }`. `ParserRouter` selecciona adaptador por MIME/magic bytes.
- **Constitution compliance:** Art. III ✅ (no persistencia server-side, todo en RAM), Art. V ✅ (parsed text se trata como DATO), Art. VI ✅ (`ICvParser` puerto oficial v1.1.0), Art. VII ✅ (rate-limit `"import"` 30/h).
- **Commit:** `c61bdf4` "feat(005-cv-pdf-docx-import): parseo server-side de CV (PDF/DOCX, Constitution Art. III/V/VI/VII)"
- **Web counterpart:** [../../BuildCv-web/specs/005-web-cv-import-ui/](../../BuildCv-web/specs/005-web-cv-import-ui/) (mismo status, ship coordinado).

### 007-constitution-v1.1.0 (governance)

- **Spec:** [specs/007-constitution-v1.1.0/spec.md](./007-constitution-v1.1.0/spec.md)
- **Plan:** [specs/007-constitution-v1.1.0/plan.md](./007-constitution-v1.1.0/plan.md)
- **Research:** [specs/007-constitution-v1.1.0/research.md](./007-constitution-v1.1.0/research.md)
- **Data model:** [specs/007-constitution-v1.1.0/data-model.md](./007-constitution-v1.1.0/data-model.md)
- **Quickstart:** [specs/007-constitution-v1.1.0/quickstart.md](./007-constitution-v1.1.0/quickstart.md)
- **Tasks:** [specs/007-constitution-v1.1.0/tasks.md](./007-constitution-v1.1.0/tasks.md)
- **Contracts:** [specs/007-constitution-v1.1.0/contracts/constitution-diff.md](./007-constitution-v1.1.0/contracts/constitution-diff.md)
- **Tipo:** Enmienda MENOR (semver 1.0.0 → 1.1.0), 5 artículos modificados, ~30 líneas modificadas + ~15 añadidas, 0 líneas eliminadas.
- **Aprobación:** pendiente del owner (enmienda requiere aprobación explícita per §Gobernanza).
- **Aplicada:** ✅ la constitución física `BuildCv-api/.specify/memory/constitution.md` ya está en v1.1.0 con historial registrado.

### 006-cv-editor (frontend only, ver [006-cv-editor/](./006-cv-editor/))

Este feature NO tiene implementación en el backend. El API no recibe cambios: re-usa `AdaptCvCommand` y `ScoreCvCommand` con el texto editado por el usuario. Specs completas (7 artifacts) en esta carpeta, con cross-references a `BuildCv-web/specs/006-web-cv-editor/` y `BuildCv-web/specs/006-web-cv-diff-viewer/`.

**006-web-cv-editor:**
- **Spec:** [../../BuildCv-web/specs/006-web-cv-editor/spec.md](../../BuildCv-web/specs/006-web-cv-editor/spec.md)
- **Plan:** [../../BuildCv-web/specs/006-web-cv-editor/plan.md](../../BuildCv-web/specs/006-web-cv-editor/plan.md)
- **Research:** [../../BuildCv-web/specs/006-web-cv-editor/research.md](../../BuildCv-web/specs/006-web-cv-editor/research.md)
- **Data model:** [../../BuildCv-web/specs/006-web-cv-editor/data-model.md](../../BuildCv-web/specs/006-web-cv-editor/data-model.md)
- **Quickstart:** [../../BuildCv-web/specs/006-web-cv-editor/quickstart.md](../../BuildCv-web/specs/006-web-cv-editor/quickstart.md)
- **Tasks:** [../../BuildCv-web/specs/006-web-cv-editor/tasks.md](../../BuildCv-web/specs/006-web-cv-editor/tasks.md)
- **Contracts:** [../../BuildCv-web/specs/006-web-cv-editor/contracts/frontend-internal.md](../../BuildCv-web/specs/006-web-cv-editor/contracts/frontend-internal.md)
- **Engine version:** `0.5.0` (editor)
- **Decisiones shipped:** Zod v3 (8 schemas, defense in depth Art. I FR-029a) + `ICvStore` port (Art. VI v1.1.0) con `LocalStorageCvStore` (default). **Tiptap NO instalado** — 8 textareas estructurados en su lugar (deuda técnica documentada para v1). Zustand NO instalado.
- **Constitution compliance:** Art. I ✅ (FR-029a editor no agrega entidades), Art. III ✅ (FR-040b "Limpiar borrador" obligatorio), Art. VI ✅ (`ICvStore` puerto oficial).
- **Commit:** `748611d`

**006-web-cv-diff-viewer (sub-feature):**
- **Spec:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/spec.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/spec.md)
- **Plan:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/plan.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/plan.md)
- **Research:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/research.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/research.md)
- **Data model:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/data-model.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/data-model.md)
- **Quickstart:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/quickstart.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/quickstart.md)
- **Tasks:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/tasks.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/tasks.md)
- **Contracts:** [../../BuildCv-web/specs/006-web-cv-diff-viewer/contracts/frontend-internal.md](../../BuildCv-web/specs/006-web-cv-diff-viewer/contracts/frontend-internal.md)
- **Stack:** `diff` (jsdiff v5, BSD-3-Clause) + custom React renderer
- **Constitution compliance:** Art. I ✅ (bloquea "Aceptar y exportar" si hay invenciones Hard), Art. V ✅ (diff no se renderiza como HTML)
- **Commit:** `4bf92b7`

## Features SHIPPED (detalle)

### 008-observability (v0.5.1)

- **Spec:** [specs/008-observability/spec.md](./008-observability/spec.md)
- **Plan:** [specs/008-observability/plan.md](./008-observability/plan.md)
- **Research:** [specs/008-observability/research.md](./008-observability/research.md)
- **Data model:** [specs/008-observability/data-model.md](./008-observability/data-model.md)
- **Quickstart:** [specs/008-observability/quickstart.md](./008-observability/quickstart.md)
- **Tasks:** [specs/008-observability/tasks.md](./008-observability/tasks.md)
- **Contracts:** [specs/008-observability/contracts/observability-api.md](./008-observability/contracts/observability-api.md)
- **Endpoint:** `GET /metrics` (Prometheus), `GET /health/ready` (detailed JSON)
- **Status:** Prometheus metrics (prometheus-net), OpenTelemetry tracing (OTLP), 3 component health checks (Parser, AiClient, PdfGenerator). Serilog structured logging already existed.
- **Tests:** 5 new integration tests (194 total, all passing)
- **Commit:** `4975966` "feat(008-observability): Prometheus metrics + component health checks + OpenTelemetry tracing"
- **Constitution compliance:** Art. III ✅ (no PII in logs/metrics/traces), Art. VI ✅ (observability in Infrastructure + Api, not Domain)

### 009-auth (v1)

- **Spec:** [specs/009-auth/spec.md](./009-auth/spec.md)
- **Plan:** [specs/009-auth/plan.md](./009-auth/plan.md)
- **Research:** [specs/009-auth/research.md](./009-auth/research.md)
- **Data model:** [specs/009-auth/data-model.md](./009-auth/data-model.md)
- **Quickstart:** [specs/009-auth/quickstart.md](./009-auth/quickstart.md)
- **Tasks:** [specs/009-auth/tasks.md](./009-auth/tasks.md)
- **Contracts:** [specs/009-auth/contracts/auth-api.md](./009-auth/contracts/auth-api.md), [specs/009-auth/contracts/user-data-api.md](./009-auth/contracts/user-data-api.md)
- **Endpoints:** `POST /auth/google`, `POST /auth/linkedin`, `GET /auth/me`, `POST /auth/refresh`, `POST /auth/logout`, `GET/PUT/DELETE /user/data`, `POST /user/consent`, `POST /user/consent/revoke`, `GET /privacy-policy`
- **Status:** OAuth 2.0 (Google + LinkedIn), JWT access/refresh tokens, Habeas Data compliance (consent, ARCO rights, privacy policy), in-memory stores for v0.5
- **Tests:** 290 total (65 auth unit + 27 integration tests)
- **Constitution compliance:** Art. III ✅ (no PII in logs), Art. IV ✅ (honest privacy policy), Art. VI ✅ (Clean Architecture ports), Art. IX ✅ (Habeas Data: consent, ARCO, audit trail)
- **Deviations:** In-memory stores in Application layer (not Infrastructure), PKCE not implemented, error responses not RFC 9457

### 010-persistence (v1)

- **Spec:** [specs/010-persistence/spec.md](./010-persistence/spec.md)
- **Plan:** [specs/010-persistence/plan.md](./010-persistence/plan.md)
- **Research:** [specs/010-persistence/research.md](./010-persistence/research.md)
- **Data model:** [specs/010-persistence/data-model.md](./010-persistence/data-model.md)
- **Quickstart:** [specs/010-persistence/quickstart.md](./010-persistence/quickstart.md)
- **Tasks:** [specs/010-persistence/tasks.md](./010-persistence/tasks.md)
- **Contracts:** [specs/010-persistence/contracts/persistence-api.md](./010-persistence/contracts/persistence-api.md)
- **Status:** Specs completas (7 artifacts), 30 tareas definidas. Fix Art. VI (interface extraction) + PostgreSQL/EF Core. Pendiente de implementación.
- **Architecture:** IConsentStore + IUserDataStore interfaces in Application, EfConsentStore/EfUserDataStore/EfRefreshTokenStore adapters in Infrastructure, BuildCvDbContext with PostgreSQL
- **Constitution compliance:** Art. VI ✅ (ports in Application, adapters in Infrastructure), Art. IX ✅ (consent + ARCO persistence for legal compliance)

### 011-factus (v1)

- **Spec:** [specs/011-factus/spec.md](./011-factus/spec.md)
- **Plan:** [specs/011-factus/plan.md](./011-factus/plan.md)
- **Research:** [specs/011-factus/research.md](./011-factus/research.md)
- **Data model:** [specs/011-factus/data-model.md](./011-factus/data-model.md)
- **Quickstart:** [specs/011-factus/quickstart.md](./011-factus/quickstart.md)
- **Tasks:** [specs/011-factus/tasks.md](./011-factus/tasks.md)
- **Contracts:** [specs/011-factus/contracts/](./011-factus/contracts/)
- **Status:** Specs completas (7 artifacts), 34 tareas definidas. Plugin opcional para facturación DIAN vía Factus API v2.
- **Architecture:** IInvoiceProvider port in Application, FactusAdapter + LocalInvoiceProvider in Infrastructure, feature flag `Factus:Enabled`
- **Constitution compliance:** Art. VI ✅ (ports in Application, adapters in Infrastructure), Art. IX ✅ (facturación DIAN opcional, no bloquea uso del sistema)

### 012-wompi (v1)

- **Spec:** [specs/012-wompi/spec.md](./012-wompi/spec.md)
- **Proposal:** [specs/012-wompi/proposal.md](./012-wompi/proposal.md)
- **Design:** [specs/012-wompi/design.md](./012-wompi/design.md)
- **Tasks:** [specs/012-wompi/tasks.md](./012-wompi/tasks.md)
- **Archive report:** [specs/012-wompi/archive-report.md](./012-wompi/archive-report.md)
- **Endpoints:** `POST /api/v1/payments/checkout`, `POST /api/v1/payments/webhook` (HMAC), `GET /api/v1/payments/{id}`, `GET /api/v1/payments`
- **Status:** Wompi (Colombian payment gateway) Widget Checkout Web integration. 3 chained PRs (`feature-branch-chain`) + 1 warning-fix PR delivered in 3h 21min wall-clock.
- **Architecture:** `IPaymentProvider` + `IPaymentStore` ports in Application, `WompiAdapter` (HMAC SHA256) + `EfPaymentStore` + `InMemoryPaymentStore` + `DisabledPaymentProvider` in Infrastructure, `PaymentReconciliationWorker` (IHostedService, polling 60s) for stale-Pending recovery, integration with 011-factus via `IInvoiceProvider` on Approved.
- **Key features:** 3 credit packages in COP (Starter 10 / Standard 50 / Pro 100), server-side confirmation (Art. IX FR-046/048/049), idempotent webhooks (unique index on `wompi_transaction_id` + `idempotency_key`), background reconciliation for webhook delivery failures, feature flag `Wompi:Enabled`, environment gating (sandbox/production).
- **Tests:** 451/451 backend passing (83 new payment tests), 718/718 frontend passing (8 new BFF/widget tests). TDD: red→green on every handler + adapter.
- **Zero suppressions** (Art. VIII / project rules).
- **Constitution compliance:** Art. III ✅ (no PII in logs, no card data), Art. VI ✅ (Clean Architecture ports), Art. VIII ✅ (TDD), Art. IX FR-046 ✅ (server-side confirmation via webhook + GET /v1/transactions + reconciliation worker), Art. IX FR-048 ✅ (verify amount/status server-side), Art. IX FR-049 ✅ (browser events advisory only, webhook + GET are source of truth).
- **Deviations from design:** (1) `Payment.ProviderSessionId` added (PR1) to enable idempotent session replay without re-calling the provider; (2) EF shadow property `xmin` for PostgreSQL system column optimistic concurrency (avoids touching Domain); (3) `InvoiceType.Invoice` enum value added (warning fix PR) for payment-triggered Factus invoices. All non-breaking, additive.
- **Commits:** `562f735` (PR1 domain+application) → `790b26b` (PR2 infrastructure+DB) → `8a7a3a7` (PR3 endpoints+BFF) → `a94c53e` (PR3 doc sync) → `7aa141b` (5 sdd-verify warnings closed via TDD).
- **Git tag:** `012-wompi-v1.0` at commit `7aa141b`.
- **Follow-up:** 012 deferred "Credit consumption logic (separate feature)" (proposal.md line 24). **013-credit-consumption** is the explicit follow-up that closes the v1 monetization loop.

### 014-constitution-v1.2.0 (governance) — ✅ SHIPPED + ARCHIVED

- **Proposal:** [specs/014-constitution-v1.2.0/proposal.md](./014-constitution-v1.2.0/proposal.md)
- **Spec:** [specs/014-constitution-v1.2.0/spec.md](./014-constitution-v1.2.0/spec.md)
- **Design:** [specs/014-constitution-v1.2.0/design.md](./014-constitution-v1.2.0/design.md) (literal markdown diff de las 6 secciones + apply strategy de 1 commit)
- **Tasks:** [specs/014-constitution-v1.2.0/tasks.md](./014-constitution-v1.2.0/tasks.md) (5 tasks T1–T5)
- **Verify:** [specs/014-constitution-v1.2.0/verify-report.md](./014-constitution-v1.2.0/verify-report.md) (6/6 gates green, 2 WARNINGs closed)
- **Archive:** [specs/014-constitution-v1.2.0/archive-report.md](./014-constitution-v1.2.0/archive-report.md)
- **Tipo:** Enmienda MENOR (semver 1.1.0 → 1.2.0), 4 artículos modificados (III/VI/VII/IX cross-ref) + header bump + §Gobernanza append.
- **Code impact:** CERO. **Docs impact:** 24 líneas añadidas + 3 modificadas + 0 eliminadas en `BuildCv-api/.specify/memory/constitution.md` (+ 3 supporting docs: AGENTS.md, 000-INDEX.md; CONSTITUTION-README.md no existe — T2 skip). Total 47 insertions / 9 deletions across 3 files.
- **Aprobación:** owner sign-off per §Gobernanza paso 3 (PR review sirve como ratificación).
- **Apply:** ✅ `sdd-apply` ejecutado — 1 commit `f385be3` en `main` con 3 archivos del lado api (constitution.md + AGENTS.md + 000-INDEX.md). `sdd-verify` PASS (6/6 gates: 630/630 dotnet test, dotnet format clean, 0 warnings build). `sdd-archive` completo con tag `014-constitution-v1.2.0` (local only, no push).
- **Closes:** 2 pre-existing WARNINGs del verify de 013.2-web-jwt-cookie (Art. VI ratification) y 009-auth (Art. III/VII v0/v1 boundary).

### 015-feature-flags (v1) — ✅ SHIPPED + ARCHIVED

- **Proposal:** [specs/015-feature-flags/proposal.md](./015-feature-flags/proposal.md)
- **Spec:** [specs/015-feature-flags/spec.md](./015-feature-flags/spec.md)
- **Design:** [specs/015-feature-flags/design.md](./015-feature-flags/design.md)
- **Tasks:** [specs/015-feature-flags/tasks.md](./015-feature-flags/tasks.md)
- **Verify:** [specs/015-feature-flags/verify-report.md](./015-feature-flags/verify-report.md)
- **Archive:** [specs/015-feature-flags/archive-report.md](./015-feature-flags/archive-report.md)
- **Endpoints:** `GET /api/v1/admin/feature-flags` + `GET /api/v1/admin/feature-flags/{name}` + `PUT /api/v1/admin/feature-flags/{name}` + `GET /api/v1/admin/feature-flags/{name}/audit-log` (admin role + 30/min/IP rate limit).
- **Status:** Centralized feature flag management. `IFeatureFlag` port replaces 3 bespoke patterns (011-factus, 012-wompi, 013-credit-consumption) with hybrid storage (appsettings defaults + DB overrides) + in-memory caching (`IMemoryCache`, 60s TTL, configurable via `FeatureFlags:CacheTtlSeconds`) + `Invalidate(name)` on admin updates + append-only audit log (`feature_flag_audit_log` table, keyset pagination `base64(ticks:id)`).
- **Architecture:** Domain pure (0 packages): `FeatureFlag` + `FeatureFlagAuditLog` + `FeatureFlagNotFoundException`. Ports (`IFeatureFlag`, `IFeatureFlagStore`, `IFeatureFlagAdminService`, `FeatureFlagsOptions`) in Application/Common. Adapters (`EfFeatureFlagStore` with xmin concurrency + `InMemoryFeatureFlagStore` + `CachingFeatureFlagDecorator` + `FeatureFlagAdminService` + `FeatureFlagMigrationService` IHostedService + 3 backward-compat adapters) in Infrastructure. EF migration `20260625085419_AddFeatureFlags` adds 2 tables with PKs, indexes, CHECK constraints. API layer adds `FeatureFlagAdminEndpoints` (4 endpoints) + `AuthPolicies.Admin` (require role "admin") + `RateLimiting.AdminPolicy` (fixed-window 30/min/IP).
- **Migrated flags:** 011-factus `factus-enabled`, 012-wompi `wompi-enabled`, 013-credit-consumption `credits-enabled`. Existing appsettings keys continue to work as defaults via `FeatureFlags:Defaults:{name}`.
- **Tests:** 732/732 backend passing (+102 over pre-015 baseline of 630). 102 new tests across 19 files (7 Domain + 24 Application + 55 Infrastructure + 16 Integration).
- **Delivery:** 3 chained PRs (PR1 Domain+Application / PR2 Infrastructure+DB / PR3 API), 15 work-unit commits on `main`, all green per gate. 0 new NuGet dependencies (uses `Microsoft.Extensions.Caching.Memory` already in API project).
- **Constitution compliance:** Art. III ✅ (audit log stores `Guid` user id only, no PII), Art. VI ✅ (Domain pure, ports in Application, adapters in Infrastructure), Art. VII ✅ (new `"admin"` rate-limit policy 30/min/IP, lower than `score` 60/min and `ai` 5/h intentionally), Art. VIII ✅ (TDD on every handler, decorator, adapter), Art. IX ✅ (every flag change is audited with `changedBy`, `oldValue`, `newValue`, `changedAt`, `reason`).
- **Commits:** `c880067` (PR1 domain) → `368e6bb` (PR1 ports) → `b79878e` (PR1 handlers) → `df765fb` (PR1 tests) → `e94a800` (PR2 EF config) → `4a6f9af` (PR2 migration) → `aefae24` (PR2 stores) → `c68fe3f` (PR2 cache + admin service) → `5a8135b` (PR2 adapters) → `ac184f0` (PR2 migration service + DI) → `9d23a4c` (PR2 format) → `7868ec8` (PR3 cache invalidation fix) → `5229e4b` (PR3 endpoints) → `a154ff1` (PR3 format) → `986e53e` (PR3 e2e tests, HEAD).
- **Git tag:** `015-feature-flags-v1.0` at commit `986e53e` (local only, no push).
- **Known deviation:** 011/012 backward-compat adapters (`FeatureFlagInvoiceAdapter`, `FeatureFlagPaymentAdapter`) exist with correct delegation logic but are NOT wired in production DI — production DI still uses the pre-015 startup-time appsettings-based choice. Practical impact: admin updates to `factus-enabled` / `wompi-enabled` via the API are persisted to DB + audit log, but do NOT change which `IInvoiceProvider` / `IPaymentProvider` is resolved in the running process — they take effect on next restart via migration reseed. 011/012 test suites pass unchanged (zero regression). Adapter classes are unit-tested and ready for future migration.
- **Deferred to v1.5:** admin dashboard UI, per-user flags, A/B testing framework, time-based rollout (`enable_at` / `disable_at`), audit log retention policy.
- **Use cases unblocked:** per-user flags (e.g., beta tester override), time-based rollout (10% over 24h), A/B testing (50/50 split on scoring algorithms), emergency kill-switches (compliance / legal), compliance toggles.

### 016-subscription-recurring (v1) — ✅ SHIPPED + ARCHIVED

- **Proposal:** [specs/016-subscription-recurring/proposal.md](./016-subscription-recurring/proposal.md)
- **Spec:** [specs/016-subscription-recurring/spec.md](./016-subscription-recurring/spec.md)
- **Design:** [specs/016-subscription-recurring/design.md](./016-subscription-recurring/design.md)
- **Tasks:** [specs/016-subscription-recurring/tasks.md](./016-subscription-recurring/tasks.md)
- **Verify:** [specs/016-subscription-recurring/verify-report.md](./016-subscription-recurring/verify-report.md) (PASS WITH WARNINGS — 3 deferred to 017)
- **Archive:** [specs/016-subscription-recurring/archive-report.md](./016-subscription-recurring/archive-report.md)
- **Endpoints:** `POST /api/v1/subscriptions` (10/min/IP, JWT) + `GET /api/v1/subscriptions/me` (JWT) + `DELETE /api/v1/subscriptions/me` (5/h/IP, JWT) + `POST /api/v1/payments/webhook` extended with `recurring_charge.successful` / `recurring_charge.failed` events (60/min/IP, HMAC).
- **Status:** Monthly recurring billing via Wompi `payment_sources` + scheduled charges. Closes the v1 monetization retention loop (manual one-time purchases were the only lever pre-016).
- **Architecture:** Domain pure (0 packages): `Subscription` + `SubscriptionPlan` + `SubscriptionStatus` + `SubscriptionStateMachine` (3 retries [1d/3d/7d] + 14d grace). Ports (`ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider`, `ISubscriptionFeatureFlag`) in Application. Adapters (`EfSubscriptionStore` with xmin concurrency + `InMemorySubscriptionStore` + `WompiRecurringAdapter` extends Wompi HTTP client + `DisabledSubscriptionProvider` + `SubscriptionFeatureFlag`) in Infrastructure. `SubscriptionReconciliationWorker` (IHostedService, 60s poll) + `SubscriptionConfiguration` + EF migration `20260625184302_AddSubscriptions` (table + 3 CHECK + 1 partial unique index `ux_subscriptions_user_active` + 1 composite index `ix_subscriptions_status_next_charge`). API: `SubscriptionEndpoints` (POST/GET/DELETE). Web: BFF routes + components + `/suscripciones` page.
- **Reuses (zero new ledger logic):** `AccreditPurchaseHandler` from 013 (credit grants via `Reason=Purchase` + idempotency key `subscription_period:{subscriptionId}:{periodStartUtc}`); `HandleWebhookHandler` extended with `event_type` dispatch (HMAC verification unchanged); `IFeatureFlag` from 015 (`subscription-recurring-enabled` defaults to `false`); 012 `IPaymentProvider` HMAC + Wompi HTTP client.
- **State machine:** Active ↔ PastDue (transitions on charge outcome) → Canceled (user cancel OR max retries exhausted OR grace period expired). Reject any transition FROM Canceled (closed-fail).
- **Tests:** 834/834 backend passing (+102 over pre-016 baseline of 732). 760/760 web passing (+15). 85/85 Playwright e2e (+6). Total +123 tests (forecast was +43, exceeded 2.86×). Forecast overshoot reflects broader coverage of state machine edge cases, xmin concurrency, retry state transitions, webhook idempotency, and 4 additional web unit tests.
- **Constitution compliance:** Art. III ✅ (payment source tokenized Wompi-side, never raw PAN), Art. IV ✅ ("Se renueva automáticamente cada mes" + "Sin reembolso al cancelar" copy), Art. VI ✅ (4 ports in Application, adapters in Infrastructure), Art. VII ✅ (3 new policies: `subscription` 10/min, `subscription-cancel` 5/h, `subscription-webhook` 60/min), Art. VIII ✅ (TDD red→green on every handler + adapter + state transition + worker), Art. IX ✅ (ARCO cascade via FK `ON DELETE CASCADE`).
- **3 WARNINGs deferred to 017:** W1 R5 cancel idempotency (current behavior: 404 on second cancel; spec says 200 with same `accessUntil`), W2 R8 ARCO anonymize doesn't pre-cancel Wompi scheduled charge (FK cascade works; Wompi side stays open briefly until Wompi's retry sequence exhausts), W3 R10 privacy policy v3 missing (v2 already covers Wompi + ARCO + DIAN; v3 would add explicit subscription disclosure).
- **Commits (15 API):** `da11fbf` (PR1 domain) → `1c404e0` (PR1 ports) → `fe96fef` (PR1 handlers + AccreditPurchaseHandler overload) → `1f6d8a9` (PR1 unit tests) → `146ab69` (PR2 EF config + DbContext) → `cca736f` (PR2 migration `20260625184302_AddSubscriptions`) → `b93b703` (PR2 stores) → `fb52026` (PR2 Wompi adapter + providers) → `58b7155` (PR2 reconciliation worker) → `bc818b9` (PR2 webhook extension) → `5a8b504` (PR2 DI + appsettings) → `da11254` (PR2 integration tests) → `0693a83` (PR3 endpoints + DTOs + 7 tests) → `33b6cce` (PR3 rate limit policies) → `c49cbc9` (PR3 format, HEAD).
- **Commits (4 Web):** `cfeb829` (BFF routes) → `0c5f258` (subscription card + modals + 15 tests) → `0f6f8e` (i18n copy Art. IV) → `6e4ab17` (`/suscripciones` page + 6 Playwright e2e, HEAD).
- **Delivery:** 3 chained PRs (PR1 Domain+Application / PR2 Infrastructure+DB / PR3 API+Web), 19 work-unit commits on `main`, all green per gate. 0 new NuGet deps.
- **Git tag:** `016-subscription-recurring-v1.0` at commit `c49cbc9` (local only, no push).
- **Deferred to v1.5:** 3+ plans (Pro tier), annual plans, free trials, promotional pricing, proration on plan change, family/shared plans, subscription pause, email notifications for failed charges, customer-initiated refunds (no refund endpoint; current period non-refundable per Art. IV).

### 021-structured-cv-import-and-job-input (v0.5.3, cross-repo: web frontend + api backend) — ✅ SHIPPED + ARCHIVED

- **Proposal:** [specs/021-structured-cv-import-and-job-input/proposal.md](./021-structured-cv-import-and-job-input/proposal.md)
- **Spec:** [specs/021-structured-cv-import-and-job-input/spec.md](./021-structured-cv-import-and-job-input/spec.md)
- **Design:** [specs/021-structured-cv-import-and-job-input/design.md](./021-structured-cv-import-and-job-input/design.md)
- **Tasks:** [specs/021-structured-cv-import-and-job-input/tasks.md](./021-structured-cv-import-and-job-input/tasks.md)
- **Archive:** [specs/021-structured-cv-import-and-job-input/archive-report.md](./021-structured-cv-import-and-job-input/archive-report.md)
- **Web counterpart:** [../../BuildCv-web/specs/021-structured-cv-import-and-job-input/](../../BuildCv-web/specs/021-structured-cv-import-and-job-input/) (✅ SHIPPED + ARCHIVED).
- **Endpoints modified (no new paths):** `POST /api/v1/score` (discriminated-union `{cv, job, engineVersion}` / `{cvText, jobText, engineVersion}`); `POST /api/v1/import` (engineVersion dispatch: legacy `{text, sections}` or structured `{cv: CvDocument}`); `GET /health/ready` (no change).
- **Engine version:** `2.0.0` (bumped from `1.0.0`; v1 path retained via `engineVersion: "1.0.0"` for one release cycle). `ScoringEngine.Version` is a `public const string` in `BuildCv.Domain.Scoring` (single source of truth, referenced from HTTP contracts).
- **Constitution compliance:** Art. I ✅ (parser only INFERs; `confidence: 'inferred' | 'explicit'`; `user_confirmed` is metadata set ONLY by the editor on save — never auto-promoted), Art. II ✅ (`ScoringEngine.ScoreV2` is pure C# with no IO/clock/randomness; determinism property test 1000 iter + parallel byte-identical; SemVer bump seals the contract), Art. III ✅ (zero server-side persistence; rollback has no data impact — no migrations, no ETL, no cache flushes required), Art. IV ✅ (honest copy in BFF; no "ATS oficial"), Art. V ✅ (`JobSpecValidator` rejects prompt-injection-shaped strings — control chars U+0000–U+001F, zero-width U+200B–U+200D/U+FEFF, substrings `ignore previous` / `system:` / `` / `assistant:`), Art. VI ✅ (Domain pure, 0 external packages verified; `ICvParser` port extended via discriminated-union `ParseResult`; no new ports needed), Art. VII ✅ (no new rate-limit policies — score/import unchanged), Art. VIII ✅ (TDD on every handler + adapter + UI component; 0 suppressions across both repos).
- **Architecture (API):** Domain types `PerSectionScore` + `RedFlag` + `RedFlagSeverity` + `ScoreResultV2` (PR3a); `ScoringEngine.ScoreV2` pure function with per-section scoring + renormalization on missing section + contact hard-gate (PR3b); `ScoringEngine.Version` bump to `2.0.0` sealed + `ScoreCvHandler` switches on `engineVersion` + `ScoreResponseMapper` exposes `perSection` + `redFlags` only on v2 (PR3c); determinism property test 1000 iter + parallel byte-identical (PR3d). Application: `JobSpec` + `JobSpecValidator` (FluentValidation, anti-prompt-injection); `CvDocument` JSON Resume; `ScoreCvCommand` discriminated union (PR1). Infrastructure: `IStructuredParser` → `ParseResult` discriminated union (`RawParseResult` / `StructuredParseResult` / `ParsingWarning`) + `LegacyParserAdapter` shim (PR2a); `PdfPigCvParser` emits `StructuredParseResult` with `CvDocument` + `confidence` markers (PR2b); `OpenXmlCvParser` preserves DOCX tables/lists in `Highlights[]` (PR2c); `ParserRouter` dispatches by `engineVersion` (PR2d). API: `ImportEndpoints` accepts `?engineVersion=` / `X-Engine-Version` (default `2.0.0`, 400 if unknown with code `IMPORT_UNSUPPORTED_ENGINE_VERSION`) + `ImportResponseMapper.Map` returns `object` discriminating by variant (PR2e).
- **Architecture (Web, summary — full detail in [BuildCv-web/INDEX](../BuildCv-web/specs/000-INDEX.md)):** `lib/editor/schema/jsonresume.ts` (12 Zod schemas + Colombian `datosPersonales`); `lib/editor/types.ts` (`migrateLegacyToJsonResume` bridge); 4 section forms (`BasicsForm`/`WorkList`/`EducationList`/`SkillsByCategory`); `lib/editor/confidence-promotion.ts` (pure `promoteConfidence(cv, touched)` — only touched slots → `user_confirmed`); `lib/job/job-spec.ts` (Zod `JobSpec`); `components/analyzer/{job-spec-form,section-breakdown}.tsx`; `lib/observability/{log-store,types}.ts` (`engineVersion` tagging with `LEGACY_ENGINE_VERSION = "1.0.0"` fallback). Feature flag `NEXT_PUBLIC_STRUCTURED_INPUT=true` (default). 4 golden JSON Resume fixtures (`basic-cv` / `full-cv` / `colombian-cv`).
- **Delta specs synced:** `BuildCv-api/specs/002-score-engine/spec.md` (+ `## v2.0.0 Changes` section with ADDED + MODIFIED + REMOVED + Rollback); `BuildCv-api/specs/005-cv-pdf-docx-import/spec.md` (+ `## v2.0.0 Changes` section); `BuildCv-web/specs/006-web-cv-editor/spec.md` (+ `## v2.0.0 Changes` section); `BuildCv-web/specs/008-observability-web/spec.md` (+ `## v2.0.0 Changes` section). New cross-cutting sub-specs (`score-section-breakdown`, `structured-job-spec`) preserved in `specs/021-structured-cv-import-and-job-input/specs/` as audit trail.
- **Tests (this PR chain):** +N api (PR1–PR6d — JobSpecValidator, discriminated-union tests, IStructuredParser contract, PdfPig/OpenXml structured tests, ScoreV2 per-section + red-flag tests, deterministic property test); +M web (Zod schemas, section component tests, JobSpecForm tests, observability `engineVersion` tagging tests, golden fixture round-trip). All previous baseline (002/003/005/006/008/009-016/018) test suites rerun unchanged (no regressions).
- **Commits API (11 work-unit):** `5f3982a` (PR1 FluentValidation + CvDocument + discriminated-union ScoreCvCommand) → `bcbd078` (PR2a IStructuredParser + ParseResult) → `7dd0089` (PR2b PdfPigCvParser StructuredParseResult) → `9456bbc` (PR2c OpenXmlCvParser StructuredParseResult) → `a4c4277` (PR2d ParserRouter engineVersion dispatch) → `2194c92` (PR2e ImportEndpoints engineVersion + ImportResultV2) → `1628a5f` (PR3a Domain types PerSectionScore/RedFlag/ScoreResultV2) → `64c3987` (PR3b ScoreV2 pure function) → `3afbe26` (PR3c ScoringEngine v2.0.0 sealed + handler dispatch) → `26cdd2b` (PR3d determinism property test) → `6f0456f` (PR6d constitution Art. II SemVer note + INDEX sync) → `5d40a53` (merge).
- **Commits Web (23 work-unit, summary):** `30672b4` (PR1 Zod+types) → `9eadbc0` (PR2a docs) → ... → `b75c5b1` (PR6d INDEX sync + proposal final-status) → `55985f3` (followups-1: 8 e2e migrated + AnalizarScreen job-lift) → `c9b893a` (merge) → `82a400b` (typo fix `020→021`).
- **Git tag:** `021-structured-cv-import-and-job-input-v1.0` at commit `5d40a53` (api HEAD) / `82a400b` (web HEAD) — local only, NOT pushed by archive per project rules.
- **Deferred / known limitations:** (a) `@axe-core/playwright` not wired for automated WCAG contrast checks (carried from 019 WARNING-2, also tracked by `020-a11y-automated-audit`). (b) `JobSpec` rejection messages use generic codes (`JOB_SPEC_PROMPT_INJECTION`) without echoing the offending payload (per Art. III — privacy first; the offending string is not logged). (c) `StructuredCvDocument` wire-format (PR1 backend `lib/job/cv-document.ts` Tagged* wrappers `{entry, confidence}`) differs in shape from the editor's internal flat JSON Resume (`lib/editor/schema/jsonresume.ts`); unification deferred to a follow-up once the editor is fully wired to the analyzer.
- **Date:** 2026-06-26.

## Features ARCHIVADAS

### 001-mvp-cv-ats (MVP original)

- **Status:** 🗄️ Archivado. La spec original (378 líneas) cubría scoring + adapt + export en un solo bloque. Se rompió en 002/003/004 para tracking granular.
- **Archive:** [specs/_archive/001-mvp-cv-ats-original/](./_archive/001-mvp-cv-ats-original/)
- **Razón del archivo:** scope demasiado grande, specs pequeñas son más testeables y revisables.

## Próximos pasos (recomendados, en orden de planificación)

1. ~~**005-cv-pdf-docx-import**~~ → ✅ SHIPPED (commit `c61bdf4`)
2. ~~**006-web-cv-editor + 006b-web-cv-diff-viewer**~~ → ✅ SHIPPED en `BuildCv-web` (commits `748611d` + `4bf92b7`)
3. ~~**008-observability (backend)**~~ → ✅ SHIPPED (commit `4975966`)
4. ~~**009-auth**~~ → ✅ SHIPPED (47 tasks, 290 tests, specs migrated)
5. ~~**010-persistence**~~ → ✅ SHIPPED (38 tasks, 342 tests)
6. ~~**011-factus**~~ → ✅ SHIPPED (DIAN invoicing opcional, invoice integration wired on payment Approved)
7. ~~**012-wompi**~~ → ✅ SHIPPED + ARCHIVED (Wompi payment gateway, 3 chained PRs + 1 warning-fix PR, tag `012-wompi-v1.0`)
8. ~~**013-credit-consumption**~~ → ✅ SHIPPED + ARCHIVED (credit ledger + 1-credit consumption gate + ARCO anonymize, 3 chained PRs + 2 verify-fix commits, tag `013-credit-consumption-v1.0`). See [archive report](./013-credit-consumption/archive-report.md).
9. ~~**013.2-web-jwt-cookie**~~ → ✅ SHIPPED + ARCHIVED (NextAuth.js integration closes Web ↔ .NET auth gap, 3 chained PRs, 11 work-unit commits, +32 tests, Art. VI amendment for `next-auth@^4.24.7` ratified, constitution bump 1.1.0 → 1.2.0 pending; tag `013.2-web-jwt-cookie-v1.0`). See [archive report](./013-credit-consumption-followups/013.2-web-jwt-cookie-archive-report.md).

### Próximos pasos candidatos (en orden de urgencia)

1. **013.1-arco-legal-review** — **SPEC COMPLETO** ([specs/013-credit-consumption-followups/013.1-arco-legal-review.md](./013-credit-consumption-followups/013.1-arco-legal-review.md)). Checklist para revisión legal ARCO antes de v1 production rollout. Pendiente: sign-off del abogado colombiano. ⚠️ Bloquea producción.
2. ~~**013.2-web-jwt-cookie**~~ → ✅ **SHIPPED + ARCHIVED** (NextAuth.js integration closes auth flow gap; tag `013.2-web-jwt-cookie-v1.0`). See [archive report](./013-credit-consumption-followups/013.2-web-jwt-cookie-archive-report.md).
3. **013.3-refund-midstream-test** — **SPEC COMPLETO** ([specs/013-credit-consumption-followups/013.3-refund-midstream-test.md](./013-credit-consumption-followups/013.3-refund-midstream-test.md)). Test de defense-in-depth para R3. Pendiente: sdd-apply (1 commit).
4. ~~**014-constitution-v1.2.0**~~ → ✅ **SHIPPED + ARCHIVED** ([archive report](./014-constitution-v1.2.0/archive-report.md); tag `014-constitution-v1.2.0` local en `f385be3`). Enmienda MENOR 1.1.0 → 1.2.0 ejecutada: ratifies `next-auth@^4.24.7` (Art. VI) + v0/v1 boundary documentado (Art. III persistence, Art. VII auth) + cross-references Art. IX. Zero code changes; `sdd-apply` + `sdd-verify` + `sdd-archive` completos. Closes 2 pre-existing WARNINGs from 009-auth and ratifies Art. VI amendment from 013.2.
5. ~~**015-feature-flags**~~ → ✅ **SHIPPED + ARCHIVED** (centralized feature flag management; `IFeatureFlag` port + `CachingFeatureFlagDecorator` (60s TTL) + admin API with `admin` role + 30/min rate limit + append-only audit log with keyset pagination; migrates 011/012/013 via 3 backward-compat adapters; 3 chained PRs, 15 work-unit commits on `main`, +102 tests, 4072 insertions / 2 deletions across 56 files; 6/6 gates green, 7/7 R's PASS, 011/012/013 backward compat verified; tag `015-feature-flags-v1.0` at commit `986e53e`). See [archive report](./015-feature-flags/archive-report.md). Prepara v1.5: per-user flags, time-based rollout, A/B testing.
6. ~~**016-subscription-recurring**~~ → ✅ **SHIPPED + ARCHIVED** (monthly recurring billing via Wompi `payment_sources` + scheduled charges; reuses 012 webhook + 013 credit ledger + 015 feature flag; 2 monthly plans [Starter 30 cr/$30k, Standard 100 cr/$80k]; 3 retries on day 1/3/7 + 14d grace; `SubscriptionReconciliationWorker` every 60s; ARCO cascade-deletes subscriptions; 3 new rate-limit policies; 3 chained PRs, 19 work-unit commits on `main` (15 API + 4 Web), +123 tests over +43 forecast, 5390 insertions / 42 deletions; 6/6 gates green, 7/10 R's fully PASS + 3 R's WARNING deferred to 017 [W1 R5 cancel idempotency, W2 R8 ARCO anonymize pre-cancel Wompi charge, W3 R10 privacy policy v3]; 011/012/013/014/015 backward compat preserved; tag `016-subscription-recurring-v1.0` at commit `c49cbc9`). See [archive report](./016-subscription-recurring/archive-report.md).
7. ~~**018-cv-iteration-loop**~~ → ✅ **SHIPPED + ARCHIVED** (best-of-N CV iteration loop with probability warning; 3 chained PRs + 2 followup batches on `main`, 24 work-unit commits [17 API + 7 Web], +113 tests over +48 forecast [85 API + 21 Web unit + 7 Web e2e], 6/6 gates green, 8/9 articles compliant + 1 Art. IV WARNING deferred to v1.5, all CRITICALs closed [R6 timeout + R8 seeding + EF migration drift], 002/003/005/009/010/011/012/013/014/015/016/017 backward compat preserved, tag `018-cv-iteration-loop-v1.0` at commit `a58c673`). See [archive report](./018-cv-iteration-loop/archive-report.md). Reuses 002-score-engine + 003-adapt-ia (additive `Seed` on `AdaptCvCommand`/`PromptBuilder`) + 013-credit-consumption (`ICreditLedger.AccreditAsync` atomic debit) + 016-subscription-recurring. CV_generator integration v1 = manual upload via 005 + paste Markdown, v2 = webhook deferred.
6. ~~**016-subscription-recurring**~~ → ✅ **SHIPPED + ARCHIVED** (monthly recurring billing via Wompi `payment_sources` + scheduled charges; reuses 012 webhook + 013 credit ledger + 015 feature flag; 2 monthly plans [Starter 30 cr/$30k, Standard 100 cr/$80k]; 3 retries on day 1/3/7 + 14d grace; `SubscriptionReconciliationWorker` every 60s; ARCO cascade-deletes subscriptions; 3 new rate-limit policies; 3 chained PRs, 19 work-unit commits on `main` (15 API + 4 Web), +123 tests over +43 forecast, 5390 insertions / 42 deletions; 6/6 gates green, 7/10 R's fully PASS + 3 R's WARNING deferred to 017 [W1 R5 cancel idempotency, W2 R8 ARCO Wompi pre-cancel, W3 R10 privacy policy v3]; 011/012/013/014/015 backward compat preserved; tag `016-subscription-recurring-v1.0` at commit `c49cbc9`). See [archive report](./016-subscription-recurring/archive-report.md).
8. ~~**021-structured-cv-import-and-job-input**~~ → ✅ **SHIPPED + ARCHIVED** (cross-repo: web frontend + api backend; structured CV import via JSON Resume `CvDocument` + `ConfidenceMarker`; mandatory `JobSpec`; `ScoringEngine.Version` bump MAYOR `1.0.0 → 2.0.0` Art. II SemVer seal; `perSection: {experience, education, skills, certifications, contact}` + `redFlags[]`; discriminated-union `ScoreCvCommand` con backward-compat shim v1; editor open-resume-inspired; `promoteConfidence` solo en editor on blur Art. I; feature flag `NEXT_PUBLIC_STRUCTURED_INPUT=true` default; 6 chained PRs + 1 followup on `feature/021-structured-cv-import-and-job-input`, 11 work-unit commits api + 23 work-unit commits web + 2 merge commits, all merged to `main` + pushed to `origin`; determinism property test 1000 iter + parallel byte-identical; delta specs synced into 002/005 (api) + 006/008 (web); tag `021-structured-cv-import-and-job-input-v1.0` at HEAD `5d40a53` api / `82a400b` web, NOT pushed by archive per project rules). See [archive report](./021-structured-cv-import-and-job-input/archive-report.md).

### Features PENDIENTES (post-013 follow-ups)

| # | Feature | Status | Spec | Esfuerzo |
|---|---------|--------|------|----------|
| 013.1 | `arco-legal-review` | 📋 PLANEADO (no-code) | [spec.md](./013-credit-consumption-followups/013.1-arco-legal-review.md) | ~30 min (sign-off externo) |
| 013.2 | `web-jwt-cookie` | ✅ SHIPPED + ARCHIVED | [spec.md](./013-credit-consumption-followups/013.2-web-jwt-cookie.md) · [archive-report.md](./013-credit-consumption-followups/013.2-web-jwt-cookie-archive-report.md) | — |
| 013.3 | `refund-midstream-test` | 📋 PLANEADO | [spec.md](./013-credit-consumption-followups/013.3-refund-midstream-test.md) | ~100 líneas (1 commit) |
| 017 | `subscription-followups` | 📋 RECOMENDADO (resolve 3 WARNINGs de 016 verify: W1 cancel idempotency 200 instead of 404, W2 ARCO anonymize pre-cancel Wompi charge, W3 privacy policy v3 with subscription disclosure) | [verify-report.md](./016-subscription-recurring/verify-report.md) §Gaps | ~200 líneas (1-2 PRs) |

## Reglas de mantenimiento

1. **Cada feature nueva DEBE tener los 7 artifacts** (spec, plan, research, data-model, quickstart, tasks, contracts). Sin excepción.
2. **El INDEX se actualiza AL COMMITEAR** el commit que cierra la feature. Status pasa de 🚧 a ✅.
3. **Features archivadas** mantienen sus archivos en `_archive/` con un README explicando por qué se archivó.
4. **Las Constitution compliance** se audita con `./scripts/constitution-check.sh` antes de marcar ✅ SHIPPED.
5. **Los tests** deben pasar 100% con `./scripts/preflight.sh` antes de marcar ✅ SHIPPED.
6. **Toda modificación constitucional** sigue el §Gobernanza → Proceso de enmienda: propuesta + impacto declarado + aprobación owner + actualización de versión.

## Convenciones de naming

- `NNN-kebab-case-name/` — NNN es el número secuencial (3 dígitos), kebab-case para el nombre.
- Ejemplos: `002-score-engine/`, `003-adapt-ia/`, `004-export-pdf/`, `005-cv-pdf-docx-import/`, `007-constitution-v1.1.0/`.
- `000-INDEX.md` (este archivo) es la única excepción al patrón numérico.
- Las features de governance usan sufijo de versión: `NNN-constitution-vX.Y.Z/`.

## Links externos

- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` (v1.1.0, ley suprema)
- **AGENTS.md:** `BuildCv-api/AGENTS.md` (tarjeta de identidad del sub-proyecto)
- **Frontend counterpart:** `BuildCv-web/specs/` (mismo patrón, ID correlativo)
- **Spec-kit oficial:** `BuildCv-api/.specify/` (CLI, scripts bash, plantillas)
