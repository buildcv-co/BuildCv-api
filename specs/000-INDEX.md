# INDEX — Registro consolidado de features (BuildCv-api)

> **Este archivo es el entry point oficial al estado del producto BuildCv.**
> Cualquier agente o humano que necesite saber "qué está hecho, qué está en curso, qué falta" debe leer esto primero.

**Última actualización:** 2026-06-24 (013-credit-consumption SHIPPED — 3 chained PRs merged: Domain+App PR1, Infra+DB PR2, API+Web PR3; 012-wompi shipped: 3 chained PRs + 1 warning-fix PR)

## Constitución vigente

| Versión | Fecha | Estado | Diff |
|---|---|---|---|
| **1.1.0** | 2026-06-09 | ✅ Vigente | [specs/007-constitution-v1.1.0/contracts/constitution-diff.md](./007-constitution-v1.1.0/contracts/constitution-diff.md) |
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
| 013 | `credit-consumption` | v1 | ✅ SHIPPED (credit ledger closes the v1 monetization loop; webhook→invoice→ledger in one tx; 1-credit `RequireCredits` filter on `/adapt`; ARCO anonymize + cascade ledger + KEEP payments/invoices; 3 chained PRs) | `main` | — |

## Leyenda de status

- ✅ **SHIPPED** — feature cerrada, en producción, tests pasando
- 🚧 **EN CURSO** — implementación activa
- 📋 **PLANEADO** — los 7 artifacts están escritos; esperando ventana de implementación
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
7. ~~**012-wompi**~~ → ✅ SHIPPED (Wompi payment gateway, 3 chained PRs + 1 warning-fix PR, tag `012-wompi-v1.0`)
8. **013-credit-consumption** → 📋 TASKS COMPLETE (artifacts: [proposal.md](./013-credit-consumption/proposal.md), [spec.md](./013-credit-consumption/spec.md), [design.md](./013-credit-consumption/design.md), [tasks.md](./013-credit-consumption/tasks.md)). Próximo: `sdd-apply` → 3 chained PRs (Domain+Application / Infrastructure+DB / API+Web), cada uno mergeable a `main`, cada uno con build+test green. Forecast: +90 tests (35 Application + 20 Integration + 10 API e2e + 25 Web e2e).

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
