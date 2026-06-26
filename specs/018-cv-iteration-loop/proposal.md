# Proposal: 018-cv-iteration-loop — Best-of-N CV Adaptation with Probability Warning

## Status

[Proposal] — Pending spec (no `spec.md` / `design.md` / `tasks.md` exist yet).

## Context

**The problem.** BuildCv currently adapts a CV to a vacancy in a **single attempt** (003-adapt-ia). The result is the LLM's first response, which is intrinsically non-deterministic: same prompt may produce different text across calls due to temperature, sampling, or context. For most cases the first attempt is acceptable, but for hard vacancies (mismatch between CV seniority and vacancy seniority, niche tech stacks, ambiguous language) the first adaptation may leave significant score on the table. The user has no visibility into whether a better adaptation exists, and no way to get one without manually retrying.

**The user's external context.** The user maintains a separate CV generator at `~/Documentos/CV_generator:main` (independent repo). The pipeline is: generate CV (external) → import into BuildCv (005) → score (002) → adapt (003) → export PDF (004). The CV produced by `CV_generator` is typically well-structured Markdown or PDF; it arrives in BuildCv via the existing `POST /api/v1/import` endpoint or the 006 editor. For v1 the user uploads manually; **direct `CV_generator` ↔ BuildCv API integration is explicitly out of scope** (deferred to v2).

**The user need.** "Iterate the adaptation N times against this vacancy and return the BEST one. Tell me how good the best one is (probability of compatibility) and warn me if it's still poor. Don't invent things that aren't in my CV."

**Why now.** 002 (score), 003 (adapt), 004 (export), 005 (import), 006 (editor), 009 (auth), 013 (credit ledger) are all shipped. The product has all the primitives needed for an iteration loop — there is no platform blocker. The 003-adapt-ia prompt and validation pipeline is the bottleneck: every iteration is an LLM call + cross-entity validation, which is expensive (credits + latency). A *capped, configurable, deterministic* iteration loop is the natural next step that closes the value loop without re-architecting 003.

**Constitutional pressure.** The loop MUST NOT invent (Art. I), MUST NOT log content (Art. III), MUST use honest probability framing (Art. IV — never "guaranteed match"), MUST treat inputs as data (Art. V — the loop must not amplify prompt-injection across iterations), MUST live behind Clean Architecture ports (Art. VI), MUST be rate-limited (Art. VII — iteration is more expensive than a single adapt), MUST be TDD-first (Art. VIII), and MUST NOT persist CV content (Art. IX — loop results are ephemeral or 24h TTL max).

## Goal

After 018 ships, an authenticated user can:
1. Submit their CV + a vacancy to `POST /api/v1/adapt/iterate`.
2. Receive a `requestId` immediately; the server runs N iterations (default 5, configurable 1-20) of `adapt → score` sequentially.
3. Track each iteration step (adapted CV, score, severity, passed-Art-I flag, timestamp) in `iteration_results`.
4. Return the best iteration (highest score with `Severity != Critical` wins; on tie, first occurrence).
5. Receive a `probabilityWarning` when the best score is below the configured threshold (default 50%), with a percentage and 2-3 honest recommended actions (e.g., "consider gaining X skill", "the CV may not be a strong fit", "this is informational, not a guarantee").
6. Re-fetch results by `requestId` (idempotent — same input + same `requestId` returns cached result).
7. Export the best adapted CV via the existing 004-export-pdf endpoint.
8. Consume 1 credit per iteration (5 iterations = 5 credits), debited via the existing 013 `RequireCredits` filter pattern.

## Non-goals

- **LLM temperature sampling control.** Use existing default; do not expose `temperature` parameter to user.
- **A/B testing of different prompts.** v1 uses the same prompt per iteration; prompt variation deferred to v1.5.
- **User feedback loop ("did this help?").** Deferred to v1.5 (requires persistence + accounts).
- **Multi-vacancy ranking.** 018 handles one CV ↔ one vacancy. Multi-vacancy is v1.5.
- **Direct `CV_generator` API integration.** v1 = user uploads via existing `POST /api/v1/import` (005) or pastes text into the 006 editor. v2 will add a webhook or polling endpoint.
- **Parallel iteration execution.** v1 is strictly sequential (one iteration at a time) for determinism + cost control. Parallel is v1.5 with batch-grade cost reduction.
- **Per-iteration streaming.** v1 returns the full result after N iterations; per-iteration streaming (SSE per step) is v1.5.
- **Custom probability thresholds per user / per vacancy category.** Single configurable threshold per request.

## Decisions (locked)

All 13 decisions are **ACCEPTED** in this proposal. They are listed here so the proposal-review step has a single source of truth and any override can be made before spec/design/tasks are written.

| # | Decision | Rationale | Constitution |
|---|---|---|---|
| **1** | **Iteration count**: default **5**, configurable **1-20** per request | 5 balances exploration (more chances to find a higher score) vs cost (5 credits per request) vs latency (~25-50s total). 20 is the hard ceiling to prevent abuse. | Art. VII (rate limit per iteration is the natural abuse wall). |
| **2** | **Selection criteria**: highest `ScoreResult.Overall` with `Severity != Critical` wins; tie-break = first occurrence | Deterministic, defensible, simple. We never select a Critical-severity adaptation even if it scored higher (Art. I is non-negotiable). | Art. I (cero invención) + Art. II (deterministic selection rule). |
| **3** | **Probability threshold**: default **50%** (`0.50`), configurable **0-100%** per request | 50% is the canonical "below this, you're probably not a strong match" cutoff used in recruitment heuristics. Configurable so the user can tune sensitivity. | Art. IV (encuadre honesto: we say "probabilidad de compatibilidad", not "garantía"). |
| **4** | **Determinism**: same input + same `requestId` → same result; LLM is seeded with `requestId:iterationIndex` per iteration | Reproducibility for re-runs and idempotency. The Anthropic SDK supports `seed` parameter for reproducibility; we add it where supported, log it where not. | Art. II (mismo input + misma versión ⇒ mismo score, generalized to the loop level). |
| **5** | **Credit cost**: **1 credit per iteration**; 5 iterations = 5 credits, debited via existing 013 `RequireCredits(N)` filter | Reuses the proven 013 credit gate; symmetric with single-adapt cost (1 credit). Higher iteration count = higher cost, clearly visible to user before starting. | Art. VII (anti-abuse wall) + reuse of 013 pattern. |
| **6** | **Concurrency**: **sequential** (one iteration at a time, not parallel) | Determinism (parallel races could finish in any order, breaking reproducibility), cost control (parallel doesn't reduce per-iteration LLM cost), and simpler state machine. | Art. II (deterministic) + cost discipline. |
| **7** | **Timeout**: **30s per iteration**, **5min total** hard cap | 30s accommodates Anthropic Sonnet 4 p99 latency (~15s) + cross-entity validation (~1s) + buffer. 5min total = 10 iterations worst-case before timeout. On timeout, return best-so-far with `partial: true` flag. | Art. VII (abuse wall) + UX (user must get a response within 5min). |
| **8** | **CV source**: existing endpoints (`POST /api/v1/import` for PDF/DOCX, 006 editor paste) + **raw text input** in iteration request body | Reuse proven flows. Raw text input is the simplest path for `CV_generator` users who generate Markdown and paste it. | Art. VI (reuse existing `ICvParser` port + ICvStore). |
| **9** | **Reuse**: `002-score-engine` for scoring, `003-adapt-ia` for adaptation (IAiClient + PromptBuilder + CrossEntityValidator + SeverityPolicy) | Zero duplication. The iteration loop is a thin orchestrator over proven primitives. | Art. VI (Clean Architecture: ports reused, no domain changes). |
| **10** | **Art. I enforcement**: `CrossEntityValidator` MUST pass (Severity != Critical) for every iteration; iterations that produce Critical severity are **excluded** from the best-result selection and flagged in `iterationLog` with `passedArtI: false` | Non-negotiable. The best-score selection must never pick a Hard-invention result. The log surfaces them for transparency. | Art. I (cero invención — the validator is the gate). |
| **11** | **Probability warning**: when best score < threshold, return `{ belowThreshold: true, thresholdPct: 50, bestPct: 42, recommendedActions: [...] }` with 2-3 honest, generic actions | "Generic" means actions do NOT mention specific invented skills or companies (that would be its own form of invention). Examples: "Consider gaining experience in skill categories the vacancy emphasizes", "The CV may not be a strong fit — review the vacancy requirements", "This is informational, not a guarantee of outcome". | Art. IV (encuadre honesto: "probabilidad", "informational", never "garantía"). |
| **12** | **No new NuGet dependencies**: reuse existing `Microsoft.Extensions.Caching.Memory`, `Microsoft.EntityFrameworkCore`, `Anthropic.SDK` (or whatever 003 already uses), `Microsoft.Extensions.Logging` | The loop is pure orchestration. No new infrastructure. Keeps the dependency tree stable and respects project discipline. | Art. VI (Clean Architecture: ports in Application, adapters in Infrastructure, no new packages in Domain). |
| **13** | **Idempotency by `requestId`**: same `requestId` → cached result (no double-charge, no re-execution); TTL = **24h**; cache key = `requestId` (NOT cvText + jobText, to keep cache hits intentional, not accidental) | 24h matches 003-adapt-ia ephemeral TTL convention. Idempotency protects against retries (network blips, browser refresh) from double-charging credits — same problem 013 solved for `adapt`. | Art. IX (operational metadata only, no CV content in cache key) + symmetry with 013 idempotency pattern. |

## Architecture (locked)

### Backend — Domain (`BuildCv.Domain/Iteration/`)

```csharp
public enum IterationOutcome { Completed, Failed, ExcludedInvention, Timeout }

public sealed record IterationStep(
    int IterationNumber,
    string AdaptedCv,            // empty if failed/excluded
    int Score,                   // 0 if failed/excluded
    Severity Severity,           // None/Warning/Critical (from ValidationReport)
    bool PassedArtI,             // Severity != Critical
    IterationOutcome Outcome,
    DateTime CompletedAt);

public sealed record IterationRequest(
    Guid RequestId,
    Guid UserId,                 // v1: required (auth); v0.5 fallback: null + IP rate-limit
    string CvText,
    string JobText,
    int IterationCount,          // 1-20, default 5
    int ProbabilityThresholdPct, // 0-100, default 50
    DateTime CreatedAt);

public sealed record ProbabilityWarning(
    bool BelowThreshold,
    int ThresholdPct,
    int BestPct,
    IReadOnlyList<string> RecommendedActions);

public sealed record IterationResult(
    Guid RequestId,
    IterationStep BestStep,                       // highest Score with PassedArtI
    IReadOnlyList<IterationStep> IterationLog,    // all attempts (including excluded)
    ProbabilityWarning? ProbabilityWarning,
    bool Partial,                                 // true if loop hit timeout before completing all N
    string EngineVersion,                         // "1.0.0"
    DateTime CompletedAt);
```

### Backend — Application (`BuildCv.Application/Features/Iteration/`)

```csharp
public interface IIterationService
{
    Task<Result<IterationResult>> IterateAsync(IterationRequest request, CancellationToken ct);
}

public interface IIterationStore
{
    Task SaveAsync(IterationResult result, CancellationToken ct);
    Task<IterationResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct);
    Task DeleteExpiredAsync(DateTime olderThan, CancellationToken ct);  // 24h TTL cleanup
}
```

**Handler**: `IterateAdaptationHandler`
- Validates request (1 ≤ `IterationCount` ≤ 20, 0 ≤ `ProbabilityThresholdPct` ≤ 100).
- Checks `IIterationStore.GetByRequestIdAsync(requestId)` first → return cached if hit (idempotency).
- For each iteration i in 1..N:
  - Call existing `AdaptCvHandler.HandleAsync` (reuse 003 pipeline; pass `iterationSeed = $"{requestId}:{i}"` to PromptBuilder so Anthropic `seed` parameter is deterministic).
  - Run `ScoreCvHandler.Handle` (reuse 002 engine) on the adapted CV.
  - Record `IterationStep` in memory.
- Apply best-selection rule (decision #2).
- Compute `ProbabilityWarning` if `BestStep.Score < threshold` (decision #11).
- Cache + return.

**Handler**: `GetIterationResultHandler` — thin pass-through to `IIterationStore`.

### Backend — Infrastructure (`BuildCv.Infrastructure/Iteration/`)

- `EfIterationStore` — EF adapter. New table `iteration_results` with PK `(request_id)`, columns for `cv_text_hash` (SHA-256, no content), `job_text_hash`, `user_id`, `iteration_count`, `threshold_pct`, `best_score`, `best_adapted_cv` (full text? or only on demand?), `iteration_log_json`, `probability_warning_json`, `partial` bool, `created_at`, `expires_at`. Indexes on `(user_id, created_at)` and `(expires_at)` for cleanup queries.
- `InMemoryIterationStore` — for unit tests + InMemory provider.
- `IterationCleanupWorker` — `IHostedService` running every 1h, deletes rows where `expires_at < UtcNow`.
- DI: register in `AddApplication()` and `AddInfrastructure()`. EF migration `20260625HHMMSS_AddIterationResults`.

**Cache-vs-persist decision**: `iteration_results` is **persisted** (not just in-memory) so the user can refresh their browser / reopen the tab and still retrieve the result. 24h TTL respects Art. III (ephemeral, no long-term CV storage).

### Backend — API (`BuildCv.Api/Features/Iteration/`)

| Method | Path | Auth | Rate limit | Description |
|---|---|---|---|---|
| `POST` | `/api/v1/adapt/iterate` | JWT + `RequireCredits(iterationCount)` | `"iterate"` 10/h per IP (NEW policy, stricter than `ai` 5/h × N iterations consumed) | Start iteration loop, return `requestId` immediately + run async OR return full result synchronously? |
| `GET` | `/api/v1/adapt/iterate/{requestId}` | JWT | `"iterate"` 10/h per IP (shared bucket) | Retrieve cached result by `requestId` (idempotent) |

**Synchronous vs async decision**: v1 returns **synchronously** with a `?wait=true` query param defaulting to true (waits up to 5min). With `wait=false` returns `202 Accepted` + `requestId` immediately, and client polls `GET /iterate/{requestId}`. v1.5 will offer webhook notification.

**Endpoint filter**: `.RequireCredits(N)` where N = request `iterationCount` (extends 013 pattern).

### Frontend — Web (`BuildCv-web/`)

- New page: `/analizar/iterate` — file upload or paste CV + paste vacancy + slider for iteration count (1-20) + threshold input (0-100%) + "Start iteration" button.
- Components:
  - `IterationControlPanel` — iteration count slider, threshold input, "credits needed: N" indicator, "Start" CTA.
  - `IterationProgress` — live progress bar (polls `GET /iterate/{requestId}` every 2s while `completed=false`).
  - `IterationResultCard` — best score badge, severity indicator, "View best adaptation" + "Export PDF" + "View other attempts" buttons.
  - `ProbabilityWarning` — yellow banner with threshold + best percentage + recommended actions list. Copy: "Probabilidad de compatibilidad baja ({bestPct}% < {thresholdPct}%) — informativo, no garantía."
  - `IterationLogTable` — collapsible panel showing per-iteration: number, score, severity, passed-Art-I flag, timestamp.
- BFF routes: `app/api/adapt/iterate/route.ts` (POST) + `app/api/adapt/iterate/[requestId]/route.ts` (GET). Mirror 013 BFF cookie pattern.
- Documentation note: a `docs/integrations/cv-generator.md` page explaining "to use CV_generator with BuildCv iteration loop: generate CV → export as Markdown/PDF → upload to `/analizar/iterate`". No code integration in v1.

### Reuse map (zero new domain logic)

| Component | Origin | Role in 018 |
|---|---|---|
| `IAiClient` + `AnthropicAiClient` | 003 | LLM call per iteration |
| `PromptBuilder` | 003 | Build prompt (extend to accept `iterationSeed`) |
| `CrossEntityValidator` | 003 (Domain) | Art. I gate (decision #10) |
| `SeverityPolicy` | 003 (Domain) | Severity classification (decision #2) |
| `IScoringEngine` + `ScoringEngine` | 002 (Domain) | Score per adapted CV |
| `ICreditLedger` + `RequireCredits` filter | 013 | Credit gate (decision #5) |
| `ICvParser` + parsers | 005 | CV text extraction (when uploaded) |
| `ICvStore` | 006 (web) | Web-side CV draft persistence |

## Risks

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| **1** | **LLM non-determinism across iterations.** Even with `seed`, Anthropic may produce similar but not identical text. Same `requestId` re-run may produce slightly different result. | Med | Document the limitation in copy ("re-running may produce slightly different text"). Cache key includes `requestId`, so the FIRST result is the canonical one for 24h. Log LLM `seed_used` flag. |
| **2** | **Credit cost surprise.** User starts 20 iterations without realizing that's 20 credits. | Med | UI shows "credits needed: N" prominently; "Start" button requires confirmation modal. Backend `.RequireCredits(N)` filter debits BEFORE iteration starts (fail-fast if insufficient). |
| **3** | **Probability threshold "magic number".** 50% is a guess. Users may tune it poorly. | Med | Configurable per request. Document the default in API swagger + UI tooltip. v1.5 will offer per-vacancy-category defaults. |
| **4** | **CrossEntityValidator false positives.** If validator is too strict, all iterations produce `Severity.Critical` → best-step selection has no candidate → fallback to `severest among Critical` with explicit `allExcluded: true` flag. | Low-Med | Existing validator (003) has acceptable false-positive rate in production. Log count of excluded iterations. UI shows warning when `allExcluded: true`. |
| **5** | **Timeout handling.** Long iteration (p99 LLM latency) may exceed 30s per-iteration timeout, aborting the loop. | Med | Per-iteration 30s + 5min total. On timeout, return `partial: true` with `bestSoFar` step. UI shows "Partial result: N of M iterations completed before timeout". |
| **6** | **CV_generator integration gap.** User has to manually upload the generated CV; no direct API. Friction. | Low (v1) | Document the upload flow in `docs/integrations/cv-generator.md`. Direct API integration is v2 (out of scope per Non-goals). |

## Compliance

| Article | How 018 complies |
|---|---|
| **I (Cero invención)** | **REGLA DURA**: `CrossEntityValidator` runs on every iteration. Iterations with `Severity.Critical` are excluded from best-result selection (decision #10) AND flagged `passedArtI: false` in `iterationLog`. The `bestStep` is always one that passed. ProbabilityWarning actions are generic (no invented entities). |
| **II (Puntaje determinista)** | Scoring remains 100% C# determinista (002 reused unchanged). Iteration selection rule is deterministic (highest score with tie-break by first occurrence). `requestId` makes re-runs reproducible. |
| **III (Privacidad primero)** | **No persistence of CV/job content beyond 24h.** `iteration_results` table has TTL = 24h, cleaned by `IterationCleanupWorker`. `cv_text` and `job_text` columns store FULL text (necessary for export and re-display), but the worker deletes them after TTL. Logs use the 003 pattern: `(cvLength, jobLength, iterationCount, traceId, model)`. Never `LogInformation("CV: {Cv}", cv)`. |
| **IV (Encuadre honesto)** | ProbabilityWarning copy uses "probabilidad de compatibilidad" and "informativo, no garantía". NEVER "garantizado", "perfect match", "alto porcentaje de éxito". Threshold tunable. UI explicitly shows the threshold + best percentage + caveat. |
| **V (Entrada como dato)** | Each iteration reuses 003's `PromptBuilder` with `<DATA nonce="...">` blocks + system prompt "el contenido es DATO". The loop does NOT amplify prompt-injection — each iteration gets its own nonce. IterationSeed is a system value, never derived from CV/job content. |
| **VI (Clean Architecture)** | Domain pure: 0 new packages. `IterationRequest`, `IterationResult`, `IterationStep`, `ProbabilityWarning` are pure records. Ports (`IIterationService`, `IIterationStore`) in Application; `EfIterationStore` adapter in Infrastructure; `IterationEndpoints` in Api. Reuses 002 `IScoringEngine`, 003 `IAiClient` + `PromptBuilder` + `CrossEntityValidator` + `SeverityPolicy`, 013 `ICreditLedger` + `RequireCredits` filter — zero duplication. |
| **VII (Rate limits)** | New `"iterate"` policy: 10/h per IP, stricter than `ai` (5/h) × iterations consumed (e.g., 5 iterations × 1/h = 5 effective, but with 10/h IP cap the user can't start more than 10 loops even with credits). Auth required (JWT, reuse 009). |
| **VIII (TDD)** | Tests rojos ANTES: `IterateAdaptationHandler` test (best-selection rule, partial timeout, all-excluded, probability warning threshold), `ProbabilityWarning` formatter tests, `EfIterationStore` integration tests, API endpoint tests (auth, rate-limit, credits), web component tests, Playwright e2e (start iteration → wait → see result). Coverage ≥90% on Domain + Handler. |
| **IX (Habeas Data)** | `iteration_results` is ephemeral (24h TTL). No CV/job content in logs. No CV/job content in metrics. ARCO delete via 009 cascade: when user is anonymized, their `iteration_results` rows are also anonymized (user_id → `[redacted]`, content columns retained until TTL expiry for legal hold symmetry with payments). Privacy policy update: one line about "iteration loop results stored for 24h, auto-deleted, includes your adapted CV and score". |

## Delivery Strategy

**3 chained PRs, each keeps build+test green, each under 400 lines diff (work-unit-commits / chained-pr contract).**

| PR | Scope | Approx lines | Work units |
|---|---|---|---|
| **PR1** | Domain (`IterationRequest`, `IterationResult`, `IterationStep`, `ProbabilityWarning`) + Application (`IIterationService`, `IIterationStore`, `IterateAdaptationHandler`, `GetIterationResultHandler`) + Domain tests + Handler tests | ~250 | 5-6 commits (one per type + one per handler + one for PromptBuilder extension + one for tests) |
| **PR2** | Infrastructure (`EfIterationStore`, `InMemoryIterationStore`, `IterationCleanupWorker`, EF migration, DI) + integration tests (concurrency, idempotency, TTL) | ~300 | 5-6 commits (migration + adapter + worker + DI + integration tests + format) |
| **PR3** | API (`IterationEndpoints` POST/GET, `.RequireCredits(N)` extension, `"iterate"` rate-limit policy) + Web (`/analizar/iterate` page, BFF routes, components, `IterationResultCard`, `ProbabilityWarning`, `IterationLogTable`, `docs/integrations/cv-generator.md`) + API integration tests + Playwright e2e | ~200 | 5-6 commits (endpoint per route + filter + rate-limit + web components per page + e2e + format) |

**Work only on `main`**, direct merge per project rules. Each PR's `main` is the previous PR's `main` (feature-branch-chain pattern).

**Per PR gates (must all pass):**
1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors).
2. `dotnet format --verify-no-changes`.
3. `dotnet test -c Release --no-build` — existing 834 tests pass + new tests pass.
4. `pnpm lint && pnpm build && pnpm test` in `BuildCv-web` (PR3 only).
5. `constitution-check.sh` — no Art. I-IX violations (Art. I + III + VI + VII + IX are the most critical).
6. `./scripts/preflight.sh` — full pipeline green.

## Open Questions (for proposal-review time)

The 13 decisions are all accepted. These are *implementation* questions the spec/design phases will need answered, surfaced here so the user can correct framing before artifact-writing begins.

1. **Synchronous vs async loop execution** — confirm? v1 default = synchronous with `wait=true` (blocks up to 5min), `wait=false` returns 202 + polls. Alternative: always async (202 + poll) to match typical LLM workflow conventions. Spec will default to synchronous; user can override.
2. **Storage of best `AdaptedCv` in `iteration_results`** — store full text or only summary + score + ref to `adapt` endpoint for re-fetch? Decision impacts table size. **Default: store full text** (necessary for one-click export PDF + browser refresh).
3. **`CV_generator` direct integration** — confirm v1 = upload only, v2 = API? Or is there a v1.5 middle ground (e.g., webhook from CV_generator → BuildCv starts iteration automatically)? **Default: v1 upload only, v2 API.**
4. **Per-iteration scoring cost** — 002 scoring is CPU-bound + deterministic (microseconds per CV), so scoring 5 iterations is negligible. But what if CV is 50k chars and 5 iterations × 50k scoring = 250k chars total? Still fast. **No mitigation needed; documented for spec phase.**
5. **Iteration cleanup worker frequency** — every 1h reasonable? Or daily cron at low-traffic hour? **Default: every 1h via `IHostedService`.**
6. **`RecommendedActions` content** — hardcode 3 generic actions in code, or expose to user config? **Default: hardcoded 3 (Art. IV consistency); user can override per request via optional `customActions` field.**

## Next

`sdd-spec` → write `spec.md` with 10+ requirements (R1: iteration loop, R2: best-selection, R3: probability warning, R4: Art. I enforcement per iteration, R5: credit gate, R6: idempotency, R7: 24h TTL, R8: timeout handling, R9: rate limit, R10: privacy disclosure, R11: CV source reuse) + scenarios using `Given/When/Then`.

Then `sdd-design` → ports, EF migration, endpoint filter implementation, frontend component contracts.

Then `sdd-tasks` → forecast 400-line budget, recommend 3 chained PRs, lock the work-unit commits per PR.

Then `sdd-apply` → 3 chained PRs, each green, each mergeable on `main`.

## References

- **Existing scoring engine:** `BuildCv-api/specs/002-score-engine/spec.md`, `BuildCv-api/src/BuildCv.Application/Features/Scoring/ScoreCvHandler.cs`, `BuildCv-api/src/BuildCv.Domain/Scoring/ScoreResult.cs`.
- **Existing adaptation:** `BuildCv-api/specs/003-adapt-ia/spec.md`, `BuildCv-api/src/BuildCv.Application/Features/Adapt/AdaptCvHandler.cs`, `BuildCv-api/src/BuildCv.Domain/Adapt/AdaptationTypes.cs` (`AdaptationResult`, `ValidationReport`, `Severity`, `EntityInvention`).
- **Existing import:** `BuildCv-api/specs/005-cv-pdf-docx-import/spec.md` (CV source).
- **Existing export:** `BuildCv-api/specs/004-export-pdf/spec.md` (downstream consumer of best adapted CV).
- **Credit consumption pattern:** `BuildCv-api/specs/013-credit-consumption/proposal.md` (mirrors format + `RequireCredits(N)` filter reuse).
- **Rate limit patterns:** `BuildCv-api/src/BuildCv.Api/Security/RateLimiting.cs` (extends with `"iterate"` policy).
- **External CV generator (user's separate repo):** `~/Documentos/CV_generator:main` (manual upload via 005 in v1; direct API in v2).
- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` v1.2.0 (ley suprema).
- **Work-unit commits skill:** `~/.config/opencode/skills/work-unit-commits/SKILL.md`.
- **Chained PR skill:** `~/.config/opencode/skills/chained-pr/SKILL.md`.
