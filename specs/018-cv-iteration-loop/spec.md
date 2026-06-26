# Spec: 018-cv-iteration-loop — Best-of-N CV Adaptation with Probability Warning

**Feature**: 018-cv-iteration-loop
**Hito**: v1.5
**Status**: [Spec] — Pending design
**Created**: 2026-06-25
**Proposal**: [./proposal.md](./proposal.md) (13 decisions locked)
**Constitution**: v1.2.0 (ley suprema)

> **Frontend counterpart:** `BuildCv-web/specs/018-web-iterate-ui/`
> **INDEX global:** [../000-INDEX.md](../000-INDEX.md)
> **Reuses (zero new domain logic):** 002-score-engine, 003-adapt-ia, 005-cv-pdf-docx-import, 013-credit-consumption.

---

## Overview

Best-of-N iteration loop that adapts a CV to a vacancy N times and returns the best result (highest score that passes Art. I). Includes probability warning when best score < threshold. Reuses 002-score-engine (scoring) + 003-adapt-ia (adapt pipeline + Art. I validation) + 013-credit-consumption (debit gate). For v1 the user uploads their CV via the existing `POST /api/v1/import` (005) or pastes raw text in the iteration request body; direct `~/Documentos/CV_generator` API integration is explicitly deferred to v2.

The loop is a **thin orchestrator** over proven primitives. No new domain logic. No new NuGet packages. No new architecture patterns.

---

## Domain model

### `IterationRequest` (new — pure record)

```csharp
public sealed record IterationRequest(
    Guid   RequestId,            // PK, generated on accept (idempotency key)
    Guid   UserId,               // FK → users (Art. III — required for v1)
    string CvText,               // the source CV (validated max 50_000 chars)
    string JobText,              // the job posting (validated max 20_000 chars)
    int    IterationCount,       // 1-20, default 5
    int    ProbabilityThreshold, // 0-100, default 50 (interpreted as "score below this triggers a warning")
    DateTime CreatedAt);
```

### `RequestStatus` enum (new)

```csharp
public enum RequestStatus
{
    Running   = 1,
    Completed = 2,  // loop finished all iterations; best step is present OR all failed
    Failed    = 3,  // all iterations produced Hard inventions (Severity.Critical)
    TimedOut  = 4,  // per-iter 30s OR total 5min hard cap reached
}
```

### `IterationStep` (new — pure record, embedded in `IterationResult`)

```csharp
public sealed record IterationStep(
    int      IterationNumber, // 1-based
    string   AdaptedCvText,   // the adapted CV produced by AdaptCvHandler
    int      Score,           // 0-100, from ScoreCvHandler (deterministic, Art. II)
    Severity Severity,        // None | Warning | Critical (from ValidationReport)
    bool     PassedArtI,      // true iff Severity != Critical (Art. I gate)
    TimeSpan Duration,        // wall-clock time of this iteration
    DateTime CompletedAt);    // UTC timestamp
```

### `IterationResult` (new — pure record)

```csharp
public sealed record IterationResult(
    Guid                   RequestId,         // FK → IterationRequest
    RequestStatus          Status,            // Completed | Failed | TimedOut
    IterationStep?         BestStep,          // null iff Status == Failed (all excluded) OR no iteration completed (TimedOut before first completed)
    IReadOnlyList<IterationStep> AllSteps,    // full log including failed/excluded iterations
    ProbabilityWarning?    ProbabilityWarning,// null iff best score >= threshold OR Status == Failed
    int                    CreditsConsumed,  // = IterationCount
    bool                   Partial,           // true iff Status == TimedOut
    string                 EngineVersion,     // "018-iteration-loop-1.0.0" (sealed for traceability)
    DateTime               CompletedAt);      // UTC
```

### `ProbabilityWarning` (new — pure record)

```csharp
public sealed record ProbabilityWarning(
    bool                  BelowThreshold,   // always true when this record is non-null
    int                   ThresholdPct,     // 0-100, mirrors request
    int                   BestPct,          // 0-100, mirrors BestStep.Score
    IReadOnlyList<string> RecommendedActions); // 3 generic actions (Art. IV — never invent entities)
```

---

## Requirements

### R1: Iteration loop endpoint (POST)

**Given** an authenticated user calls `POST /api/v1/adapt/iterate`
**With** body `{ cvText, vacancyText, iterationCount: 5, probabilityThreshold: 50 }`
**And** the user has at least `iterationCount` credits in their ledger (per 013-credit-consumption)
**When** the request is accepted
**Then**:
- A new `IterationRequest` is created with `RequestId` (Guid) + `Status=Running` + `CreatedAt=UtcNow`
- `iterationCount` credits are deducted atomically via the existing 013 `ConsumeForAdaptHandler` (fail-fast: debit happens BEFORE the loop starts)
- The iteration loop runs sequentially:
  - For each `i` in `1..iterationCount`:
    - Call existing `AdaptCvHandler.HandleAsync(cvText, jobText, iterationSeed=$"{RequestId}:{i}")` (reuses 003 prompt + validation pipeline)
    - If the result is `Severity.Critical`: mark step `PassedArtI=false`, skip scoring (still recorded in log for transparency)
    - Otherwise: call existing `ScoreCvHandler.Handle(adaptedCvText, jobText)` (reuses 002 deterministic engine)
    - Record `IterationStep` with score, severity, duration, completion timestamp
    - Track best step so far (highest `Score` among steps with `PassedArtI=true`; tie-break = first occurrence)
- After the loop:
  - If at least one step passed Art. I: `Status=Completed` + `BestStep=best passing step`
  - If NO step passed Art. I (all Critical): `Status=Failed` + `BestStep=null`
  - If total wall-clock exceeded 5min: `Status=TimedOut` + `BestStep=best-so-far if any` + `Partial=true`
- An `IterationResult` is built (with `ProbabilityWarning` if best score < threshold) and persisted to `IIterationStore` (TTL 24h)
- HTTP response is **200 OK** (synchronous default with `?wait=true`; with `?wait=false` returns `202 Accepted` + `RequestId` immediately and the client polls `GET /iterate/{requestId}`)
- Body: `{ requestId, status, bestStep, allSteps, probabilityWarning?, creditsConsumed, partial, engineVersion, completedAt }`

### R2: Probability warning

**Given** the iteration loop completes with `Status=Completed` or `Status=TimedOut` (i.e., a `BestStep` exists)
**And** `BestStep.Score < request.ProbabilityThreshold`
**When** the result is returned
**Then**:
- `ProbabilityWarning` field is populated with:
  - `BelowThreshold = true`
  - `ThresholdPct = request.ProbabilityThreshold`
  - `BestPct = BestStep.Score`
  - `RecommendedActions` = exactly 3 generic actions (in Spanish, neutral/professional tone, Art. IV compliant):
    1. `"Considera mejorar tu CV antes de aplicar."`
    2. `"La vacante puede requerir experiencia que tu CV no refleja aún; busca vacantes más afines o gana experiencia en las áreas clave."`
    3. `"Esta información es orientativa y no garantiza el resultado del proceso de selección."`
- HTTP response includes this warning in the JSON body

**Given** `BestStep.Score >= request.ProbabilityThreshold` (or `Status=Failed`)
**Then** `ProbabilityWarning` is `null` (not omitted — explicit JSON `null`).

### R3: Art. I enforcement (Hard invention exclusion)

**Given** an iteration produces an adapted CV with `Severity.Critical` (Hard invention — see 003 `CrossEntityValidator`)
**When** the iteration result is validated
**Then**:
- The step is recorded with `PassedArtI=false`, `Score=0` (scoring skipped), `Severity=Critical`
- The step is **excluded** from best-step selection (decision #2 — Hard inventions never win even if they would have scored higher)
- If **all** iterations fail Art. I: `Status=Failed`, `BestStep=null`, `ProbabilityWarning=null` (no best to warn about)
- The HTTP response body includes a top-level field `artIViolations: N` (count of excluded iterations) so the UI can show "N iteraciones excluidas por invención" (Art. IV honest disclosure)

### R4: Credit consumption (atomic debit-before-loop)

**Given** a user with `N` credits requests `iterationCount=5`
**When** the request is accepted
**Then**:
- 5 credits are deducted atomically via the existing 013 `ConsumeForAdaptHandler` BEFORE iteration starts (fail-fast)
- If the user has **fewer than 5** credits: HTTP **402 Payment Required** with body `{ error: "CREDIT/INSUFFICIENT", required: 5, balance: M }` — NO iteration runs, NO credits deducted
- If the iteration loop fails mid-way (LLM timeout, validator crash, etc.): credits are **NOT refunded** (consumed regardless — same pattern as single adapt in 013). The user paid for the attempt, not the outcome.

### R5: Idempotency by `requestId` (result caching)

**Given** a user retries the same `RequestId` within 24h (browser refresh, network blip, accidental double-click)
**When** `GET /api/v1/adapt/iterate/{requestId}` is called
**Then**:
- The cached `IterationResult` is returned from `IIterationStore.GetByRequestIdAsync`
- **No re-iteration** runs
- **No double-charge** (the original debit stands; idempotency by requestId means the second call short-circuits before `ConsumeForAdaptHandler`)
- HTTP **200 OK** with the same body shape as the original POST response

**Given** the same `RequestId` is requested after 24h
**Then** HTTP **404 Not Found** with `{ error: "ITERATION/EXPIRED" }` (TTL cleanup worker deleted the row).

### R6: Timeout handling

**Given** the iteration loop is in progress
**When** any single iteration exceeds **30 seconds** (LLM call + cross-entity validation + scoring)
**Then**: the current iteration is abandoned, recorded as `Status=Failed` for that step, and the loop moves on.

**When** the **total** wall-clock time exceeds **5 minutes**
**Then**:
- The current iteration is abandoned
- The `BestStep` so far (if any passed Art. I) is returned
- `Status=TimedOut`, `Partial=true`
- `ProbabilityWarning` is computed normally based on `BestStep.Score` vs threshold

### R7: Concurrency (sequential — one at a time)

**Given** a request has `iterationCount=5`
**When** the loop runs
**Then** iterations are executed **sequentially** (one at a time, not in parallel):
- Reason 1: **Determinism** — parallel races could finish in any order, breaking the "highest score wins + tie-break by first occurrence" reproducibility contract.
- Reason 2: **Cost control** — parallel does NOT reduce per-iteration LLM cost (Anthropic API is per-token regardless of concurrency), but DOES increase peak memory + risk of rate-limit hitting the LLM provider.
- Reason 3: **State machine** — a single sequential loop is trivially testable with a fake `IAiClient` returning canned responses in order.

### R8: Determinism via `requestId` seeding

**Given** the same `RequestId` is used for a re-run within 24h
**When** iterations run
**Then**:
- The LLM seed is set to `{RequestId}:{i}` per iteration `i` (e.g., `"a1b2c3d4-...:1"`, `"a1b2c3d4-...:2"`)
- The Anthropic SDK `seed` parameter is set to `requestId.GetHashCode()` (int32, stable across re-runs)
- Result: same input + same `RequestId` + same Anthropic model version → **similar** (but not byte-identical) iterations
- Each iteration is independently scored (scoring is 100% deterministic anyway, Art. II)

**Note (Art. IV honest copy)**: This determinism is "best-effort". Anthropic SDK `seed` is documented but not contractually guaranteed to produce identical output. The UI copy acknowledges this: "Re-ejecutar puede producir texto ligeramente distinto."

### R9: GET result endpoint (idempotent re-fetch)

**Given** an authenticated user calls `GET /api/v1/adapt/iterate/{requestId}`
**When** the result exists (cached or just-completed)
**Then**:
- HTTP **200 OK** with the full `IterationResult` (same shape as POST response)
- The response is byte-identical to the original POST response (modulo `completedAt` timestamp which was sealed at first completion)

**Given** the `RequestId` does not exist (never created OR cleaned up after 24h)
**Then** HTTP **404 Not Found** with `{ error: "ITERATION/NOT_FOUND" }`.

### R10: CV source integration (reuse existing patterns)

**Given** the user has a CV from `~/Documentos/CV_generator:main` (external repo) OR uploads one
**When** they want to iterate
**Then** both sources are supported in v1:
- **Option A** (recommended for PDF/DOCX): upload via existing `POST /api/v1/import` (005-cv-pdf-docx-import) → get plain text → paste into iteration request OR send both `cvText` and the original file's text in the iteration body
- **Option B** (recommended for Markdown from CV_generator): paste raw text directly into the iteration request body (`cvText` field)
- Both options are accepted by v1; direct `CV_generator` ↔ BuildCv API integration is **explicitly deferred to v2** (documented in `docs/integrations/cv-generator.md`)

### R11: Probability warning UI

**Given** the iteration result includes `probabilityWarning` (non-null)
**When** the web UI renders the result
**Then**:
- A warning banner is displayed at the top of the result card (semantic role=`"alert"`, ARIA live region)
- Color:
  - Amber (`bg-amber-100 text-amber-900`) for `BestPct` in range **25-49%** (mid-warning)
  - Red (`bg-red-100 text-red-900`) for `BestPct < 25%` (strong warning)
  - Hidden if `BestPct >= 50%` (no warning = no banner)
- Text format: `"Compatibilidad: {bestPct}% — {warning}"` (Art. IV honest framing)
- Action buttons (in Spanish, neutral/professional tone):
  - `"Mejorar CV"` (navigates to `/editor` to re-edit the CV)
  - `"Ver sugerencias"` (expands to show the 3 `RecommendedActions` in a list)
- **NEVER** shown text: "garantizado", "perfect match", "alto porcentaje de éxito" (Art. IV)

**Given** `probabilityWarning` is `null`
**Then** no banner is rendered.

---

## API contracts

### `POST /api/v1/adapt/iterate`

| Aspect | Value |
|---|---|
| **Auth** | Required (JWT — reuse 009 pattern) |
| **Rate limit** | `"iterate"` policy: **10/h per IP** (NEW policy, stricter than `"ai"` 5/h × iterations consumed; see Art. VII) |
| **Credit check** | Requires `iterationCount` credits (atomic debit BEFORE loop start, via 013 `ConsumeForAdaptHandler`) |
| **Body** | `{ cvText: string (max 50_000), vacancyText: string (max 20_000), iterationCount: int (1-20, default 5), probabilityThreshold: int (0-100, default 50), wait: bool (default true) }` |
| **Response 200** | `{ requestId, status, bestStep, allSteps, probabilityWarning?, creditsConsumed, partial, artIViolations, engineVersion, completedAt }` (when `wait=true`) |
| **Response 202** | `{ requestId, status: "Running", creditsConsumed }` (when `wait=false`; client polls GET endpoint) |
| **Response 401** | `{ error: "AUTH/UNAUTHENTICATED" }` |
| **Response 402** | `{ error: "CREDIT/INSUFFICIENT", required: N, balance: M }` |
| **Response 422** | `{ error: "VALIDATION/INVALID_THRESHOLD" }` (if iterationCount < 1 or > 20, or threshold < 0 or > 100) |
| **Response 429** | `{ error: "RATE_LIMITED", retryAfter: N }` |

### `GET /api/v1/adapt/iterate/{requestId}`

| Aspect | Value |
|---|---|
| **Auth** | Required (JWT) |
| **Rate limit** | `"iterate"` policy (shared bucket with POST) |
| **Response 200** | `{ requestId, status, bestStep, allSteps, probabilityWarning?, creditsConsumed, partial, artIViolations, engineVersion, completedAt }` |
| **Response 401** | `{ error: "AUTH/UNAUTHENTICATED" }` |
| **Response 404** | `{ error: "ITERATION/NOT_FOUND" }` |
| **Response 404** | `{ error: "ITERATION/EXPIRED" }` (when result was cleaned up after 24h TTL) |

---

## Application ports

### `IIterationService` (new) — `BuildCv.Application/Features/Iterations/IIterationService.cs`

```csharp
public interface IIterationService
{
    Task<IterationResult> RunAsync(IterationRequest request, CancellationToken ct = default);
    Task<IterationResult?> GetAsync(Guid requestId, CancellationToken ct = default);
}
```

### `IIterationStore` (new) — `BuildCv.Application/Features/Iterations/IIterationStore.cs`

```csharp
public interface IIterationStore
{
    Task<IterationResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default);
    Task SaveAsync(IterationResult result, CancellationToken ct = default);
    Task DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default);
}
```

**Adapters** (in `BuildCv.Infrastructure/Iterations/`):
- `EfIterationStore` — EF Core adapter; persists to `iteration_results` table (TTL 24h, indexed on `(user_id, created_at)` and `(expires_at)`).
- `InMemoryIterationStore` — for unit tests + InMemory provider.
- `IterationCleanupWorker` — `IHostedService` running every 1h, calls `DeleteExpiredAsync(UtcNow)`.

---

## Frontend integration

### New page: `/analizar/iterate`

- Form: paste CV text OR upload file (calls existing `POST /api/v1/import` 005 first, then submits iteration with the parsed text) + paste vacancy text
- Settings: iteration count slider (1-20, default 5) + threshold slider (0-100, default 50)
- Cost estimate (live): "Créditos necesarios: {N}" (updates with slider)
- Confirmation modal before starting (Art. IV honest: "Esto consumirá N créditos. ¿Continuar?")
- Progress UI during iteration: "Iteración {N} de {M}" (polls `GET /iterate/{requestId}` every 2s when `wait=false`)
- Results UI: best step card + all steps list (collapsible) + probability warning banner

### New components

- `IterationProgress` — shows current iteration N of M (when `wait=false`).
- `IterationResultCard` — displays best step + score badge + "Exportar PDF" button (calls existing 004 endpoint) + "Ver otros intentos" collapsible.
- `IterationStepList` — displays all attempts with scores, severity badges, passed-Art-I flags.
- `ProbabilityWarning` — banner with role="alert", conditional color (amber/red), action buttons.
- `IterationSettings` — sliders + live cost estimate.

### BFF routes (mirror 013 cookie pattern)

- `BuildCv-web/app/api/adapt/iterate/route.ts` — POST
- `BuildCv-web/app/api/adapt/iterate/[requestId]/route.ts` — GET

### Copy (Spanish, neutral/professional, Art. IV honest)

- "Iteración de adaptación" (page title)
- "Generando la mejor versión de tu CV para esta vacante" (subtitle)
- "Iteración {N} de {M}" (progress)
- "Tu compatibilidad con esta vacante es del {score}%" (warning text)
- "Considera mejorar tu CV antes de aplicar" (recommendation)
- "Mejores resultados requieren mayor compatibilidad" (empty-state when all fail)
- **Never**: "garantizado", "perfect match", "alto porcentaje de éxito"

---

## CV generator integration note (documentation only)

The `~/Documentos/CV_generator:main` repo generates CVs in Markdown/PDF format. For **v1**, the user uploads the generated CV via existing `POST /api/v1/import` (005) or pastes the Markdown text directly into the iteration request. For **v2** (out of scope per proposal §Non-goals), direct API integration via webhook from `CV_generator` → `BuildCv` to start iteration automatically.

A `docs/integrations/cv-generator.md` page will document the v1 upload flow + the v2 roadmap.

---

## Strategy

3 chained PRs (matching 016-subscription-recurring pattern, each < 400 line diff, all gates green):

| PR | Scope | Approx lines | Tests |
|---|---|---|---|
| **PR1** | Domain + Application: `IterationRequest`, `IterationResult`, `IterationStep`, `ProbabilityWarning`, `RequestStatus`, `IIterationService`, `IIterationStore`, `IterateAdaptationHandler`, `GetIterationResultHandler`, PromptBuilder extension (accept `iterationSeed`) | ~250 | +20 unit (Domain + Application: best-selection rule, partial timeout, all-excluded, probability warning threshold, idempotency hit/miss) |
| **PR2** | Infrastructure + DB: `EfIterationStore`, `InMemoryIterationStore`, `IterationCleanupWorker`, EF migration `20260625HHMMSS_AddIterationResults`, DI wiring | ~300 | +15 integration (concurrency, idempotency TTL, EF mapping, worker cleanup) |
| **PR3** | API + Web: `IterationEndpoints` POST/GET, `.RequireCredits(N)` extension, `"iterate"` rate-limit policy; `/analizar/iterate` page, BFF routes, components, `docs/integrations/cv-generator.md` | ~200 | +10 e2e (Playwright: start iteration → wait → see result, probability warning UI, credit gate 402) |

**Work only on `main`**, direct merge per project rules. Each PR's `main` is the previous PR's `main` (feature-branch-chain pattern). Each PR's gates (all 6 must pass):
1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors).
2. `dotnet format --verify-no-changes`.
3. `dotnet test -c Release --no-build` — existing tests pass + new tests pass.
4. `pnpm lint && pnpm build && pnpm test` in `BuildCv-web` (PR3 only).
5. `constitution-check.sh` — no Art. I-IX violations.
6. `./scripts/preflight.sh` — full pipeline green.

---

## Compliance

| Article | How 018 complies |
|---|---|
| **Art. I (Cero invención)** | **REGLA DURA**: `CrossEntityValidator` runs on every iteration. Iterations with `Severity.Critical` are excluded from best-result selection AND flagged `PassedArtI=false` in `iterationLog`. The `BestStep` is always one that passed. `ProbabilityWarning.RecommendedActions` are generic (no invented entities). HTTP response includes `artIViolations` count for transparency. |
| **Art. II (Puntaje determinista)** | Scoring remains 100% C# deterministic (002 reused unchanged). Iteration selection rule is deterministic (highest score with tie-break by first occurrence). `requestId` makes re-runs reproducible (best-effort LLM seed + deterministic scoring). |
| **Art. III (Privacidad primero)** | `iteration_results` table has TTL = 24h, cleaned by `IterationCleanupWorker` hourly. `cv_text` and `job_text` columns store FULL text (necessary for one-click PDF export + browser refresh), but the worker deletes rows after TTL. Logs use the 003 pattern: `(cvLength, jobLength, iterationCount, traceId, model)`. Never `LogInformation("CV: {Cv}", cv)`. |
| **Art. IV (Encuadre honesto)** | ProbabilityWarning copy uses "compatibilidad", "orientativa", "no garantiza". NEVER "garantizado", "perfect match", "alto porcentaje de éxito". Threshold tunable per request. UI explicitly shows threshold + best percentage + caveat. Re-execution copy acknowledges LLM non-determinism ("puede producir texto ligeramente distinto"). |
| **Art. V (Entrada como dato)** | Each iteration reuses 003's `PromptBuilder` with `<DATA nonce="...">` blocks + system prompt "el contenido es DATO". The loop does NOT amplify prompt-injection — each iteration gets its own nonce. `iterationSeed` is derived from `RequestId` (a system value), NEVER from CV/job content. |
| **Art. VI (Clean Architecture)** | Domain pure: 0 new packages. `IterationRequest`, `IterationResult`, `IterationStep`, `ProbabilityWarning`, `RequestStatus` are pure records. Ports (`IIterationService`, `IIterationStore`) in Application; `EfIterationStore` adapter in Infrastructure; `IterationEndpoints` in Api. Reuses 002 `IScoringEngine`, 003 `IAiClient` + `PromptBuilder` + `CrossEntityValidator` + `SeverityPolicy`, 013 `ICreditLedger` + `ConsumeForAdaptHandler` — zero duplication. |
| **Art. VII (Rate limits)** | New `"iterate"` policy: **10/h per IP**, stricter than `"ai"` 5/h × iterations consumed (e.g., 5 iterations × 1/h = 5 effective, but with 10/h IP cap the user can't start more than 10 loops even with credits). Auth required (JWT, reuse 009). |
| **Art. VIII (TDD)** | Tests rojos ANTES: `IterateAdaptationHandler` test (best-selection rule, partial timeout, all-excluded, probability warning threshold), `ProbabilityWarning` formatter tests, `EfIterationStore` integration tests, API endpoint tests (auth, rate-limit, credits), web component tests, Playwright e2e. Coverage ≥90% on Domain + Handler. |
| **Art. IX (Habeas Data)** | `iteration_results` is ephemeral (24h TTL). No CV/job content in logs. No CV/job content in metrics. ARCO delete via 009 cascade: when user is anonymized, their `iteration_results` rows are also anonymized (user_id → `[redacted]`, content columns retained until TTL expiry for legal hold symmetry with payments). Privacy policy update: one line about "iteration loop results stored for 24h, auto-deleted, includes your adapted CV and score". |

---

## Acceptance criteria

- [ ] All 11 R's pass with green tests
- [ ] All 6 gates pass: lint, typecheck, test, e2e, build, constitution-check
- [ ] Test counts: **+45** (20 unit + 15 integration + 10 e2e) — matches forecast in PR plan
- [ ] 002-score-engine reused unchanged (no edits to `BuildCv.Domain/Scoring/`)
- [ ] 003-adapt-ia reused unchanged for adapt pipeline (only PromptBuilder extended to accept `iterationSeed` parameter — additive, backward compatible)
- [ ] 013-credit-consumption `ConsumeForAdaptHandler` reused for debit (no new credit logic)
- [ ] `iteration_results` table has TTL = 24h with index on `(expires_at)`
- [ ] `IterationCleanupWorker` runs hourly and deletes expired rows
- [ ] Backward compat: all existing 834+ tests pass unchanged

---

## Out of scope (deferred)

- LLM temperature sampling control (v1.5)
- A/B testing of different prompts (v1.5)
- User feedback loop "did this help?" (v1.5 — requires persistence + accounts)
- Multi-vacancy ranking (v1.5)
- Per-iteration streaming via SSE (v1.5)
- Parallel iteration execution (v1.5 with batch-grade cost reduction)
- Custom `RecommendedActions` per request (v1.5 — Art. IV consistency: hardcoded 3)
- **Direct `~/Documentos/CV_generator` API integration (v2)** — v1 = upload via 005 or paste text

---

## Next

`sdd-design` → ports (`IIterationService`, `IIterationStore`), EF migration SQL (`20260625HHMMSS_AddIterationResults`), iteration loop implementation (orchestration of 003 + 002), integration with existing adapt + score services + 013 credit gate, frontend component contracts, `"iterate"` rate-limit policy extension.

Then `sdd-tasks` → forecast 400-line budget per PR, lock the work-unit commits per PR (5-6 commits each).

Then `sdd-apply` → 3 chained PRs on `main`, each green, each mergeable.

Then `sdd-verify` → 6/6 gates green + 11/11 R's PASS + 45/45 new tests.

Then `sdd-archive` → tag `018-cv-iteration-loop-v1.0`.

---

## References

- **Proposal:** [./proposal.md](./proposal.md) (13 locked decisions)
- **Existing scoring engine:** [../002-score-engine/spec.md](../002-score-engine/spec.md), `BuildCv-api/src/BuildCv.Application/Features/Scoring/ScoreCvHandler.cs`
- **Existing adaptation:** [../003-adapt-ia/spec.md](../003-adapt-ia/spec.md), `BuildCv-api/src/BuildCv.Application/Features/Adapt/AdaptCvHandler.cs`
- **Existing import:** [../005-cv-pdf-docx-import/spec.md](../005-cv-pdf-docx-import/spec.md) (CV source)
- **Existing export:** [../004-export-pdf/spec.md](../004-export-pdf/spec.md) (downstream consumer of best adapted CV)
- **Credit consumption pattern:** [../013-credit-consumption/proposal.md](../013-credit-consumption/proposal.md) (mirrors format + `ConsumeForAdaptHandler` reuse)
- **Rate limit patterns:** `BuildCv-api/src/BuildCv.Api/Security/RateLimiting.cs` (extends with `"iterate"` policy)
- **External CV generator (user's separate repo):** `~/Documentos/CV_generator:main` (manual upload via 005 in v1; direct API in v2)
- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` v1.2.0 (ley suprema)
- **Work-unit commits skill:** `~/.config/opencode/skills/work-unit-commits/SKILL.md`
- **Chained PR skill:** `~/.config/opencode/skills/chained-pr/SKILL.md`