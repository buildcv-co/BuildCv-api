# 017-subscription-followups — Exploration

## Goal

Close 3 deferred WARNINGs (W1 R5 cancel idempotency, W2 R8 ARCO Wompi pre-cancel, W3 R10 privacy policy v3) from `BuildCv-api/specs/016-subscription-recurring/verify-report.md`.

## Critical finding upfront

**All 3 WARNINGs are ALREADY FIXED in code on `main`**, landed in 3 commits on `2026-06-25` — 3 to 4 hours BEFORE `016-subscription-recurring/verify-report.md` was finally committed to the repo. The verify-report is **stale/outdated**, not a true spec deviation.

| W | Fix commit | Code file | Test file | Landed |
|---|------------|-----------|-----------|--------|
| W1 (R5) | `caaaf35` | `src/BuildCv.Application/Features/Subscriptions/CancelSubscriptionHandler.cs` | `tests/BuildCv.Application.Tests/Features/Subscriptions/CancelSubscriptionHandlerTests.cs` | 2026-06-25 15:27 -0500 |
| W2 (R8) | `cf958ec` | `src/BuildCv.Application/Features/Auth/DeleteUserDataHandler.cs` | `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs` | 2026-06-25 15:30 -0500 |
| W3 (R10) | `5f8db66` | `src/BuildCv.Application/Features/Consent/PrivacyPolicyQueryHandler.cs` | `tests/BuildCv.Application.Tests/Features/Consent/PrivacyPolicyQueryTests.cs` | 2026-06-25 15:32 -0500 |
| — | `4cad307` (docs sync) | `specs/016-subscription-recurring/verify-report.md` finally committed (still says WARNING) | — | 2026-06-25 19:02 -0500 |

Test run confirms the 3 fix areas green: `dotnet test --filter 'FullyQualifiedName~CancelSubscriptionHandlerTests|FullyQualifiedName~DeleteUserDataHandler|FullyQualifiedName~PrivacyPolicyQueryTests'` → **18/18 passed**.

**The actual gap is documentation, not code.** No `specs/017-subscription-followups/` directory exists; `000-INDEX.md` still lists 017 as `📋 PLANEADO`; 016's verify-report.md still describes all 3 items as WARNING. The 017 SDD artifacts (proposal, spec, design, tasks, verify-report, archive-report) were never authored. 018's verify-report (`specs/018-cv-iteration-loop/verify-report.md` line 161) already lists `017-subscription-followups` in its backward-compat table as shipped and its archive-report (`specs/018-cv-iteration-loop/archive-report.md` line 316) states "**Total backward compat verified**: All 011-017 + 002/003/005/009/010 test suites pass unchanged. ✅".

## WARNINGs analyzed

### R5 (W1) — Cancel subscription idempotency

- **Files (current state)**:
  - `src/BuildCv.Application/Features/Subscriptions/CancelSubscriptionHandler.cs:11-34` — `HandleAsync(userId, ct)`. Lines 13-17 load sub with `includeCanceled: true`; lines 19-25 short-circuit and return `sub` unchanged if `Status == Canceled` with `LogInformation("... already canceled ... idempotent no-op")`. Lines 27-33 only execute for Active/PastDue.
  - `src/BuildCv.Api/Endpoints/SubscriptionEndpoints.cs:85-110` — endpoint handler. Maps handler `InvalidOperationException` whose message contains `"not found"` or `"No subscription"` to 404. Since the fix returns the canceled sub without throwing, a second cancel call returns 200 with the same `accessUntil` (line 99-101). Error message tightened from `"No active subscription"` → `"No subscription found for user {userId}"` (commit `caaaf35`).
  - `src/BuildCv.Application/Features/Auth/IUserDataStore.cs` and `src/BuildCv.Application/Features/Subscriptions/ISubscriptionStore.cs` — unchanged; `GetByUserIdAsync(userId, includeCanceled, ct)` was already wired for `GetSubscriptionHandler` in 016 and is now reused.
  - Tests: `tests/BuildCv.Application.Tests/Features/Subscriptions/CancelSubscriptionHandlerTests.cs:61-85` — `HandleAsync_returns_existing_canceled_subscription_when_called_twice`. Asserts same `Id`, same `CanceledAt`, same `CurrentPeriodEnd` on both calls and that `provider.CancelledPaymentSources` count is exactly 1.
- **Current behavior (verified by reading code)**: Cancel-twice returns 200 with same `accessUntil` on the second call; Wompi cancel is invoked exactly once.
- **Edge cases already handled**:
  - Second cancel hits already-canceled sub → returns same record, no provider call, `LogInformation` with structured `subscriptionId` + `userId` (no PII).
  - User has never subscribed → throws `InvalidOperationException("No subscription found for user ...")` → endpoint 404 (correct per spec scenario 1).
  - User has only a PastDue sub → handler will still call Wompi cancel + transition to Canceled; `SubscriptionStateMachine.TransitionToCanceled` accepts from Active and PastDue (rejects only from Canceled).
- **Edge cases NOT explicitly tested (potential gaps)**:
  - Concurrent double-cancel: two DELETE requests in flight. `EfSubscriptionStore` uses xmin concurrency (verified in 016 design §xmin) so the second write loses with a `DbUpdateConcurrencyException`. The handler does not wrap the upsert in try/catch for this — would bubble up to the endpoint. Mitigated by idempotent behavior on the read side: if the second request's read happens after the first's commit, it returns the canceled sub without writing. But if both reads happen pre-commit, both attempt Wompi cancel + write; the second write fails with concurrency exception → 500. Acceptable risk for v1 (mitigated by rate limit 5/h/IP), but worth flagging for v1.5 hardening.
  - PastDue → cancel path: no explicit test. State machine allows it, but no test asserts the `accessUntil` semantics for a PastDue cancel.
- **Risks**: None remaining; only minor gap on concurrent cancel (acceptable at current rate limits).
- **Complexity**: **simple** — already implemented; only documentation remains.
- **Recommended approach**: Reuse commit `caaaf35` as the canonical W1 fix; verify-report should be updated retroactively to mark W1 as ✅ PASS.

### R8 (W2) — ARCO Wompi pre-cancel

- **Files (current state)**:
  - `src/BuildCv.Application/Features/Auth/DeleteUserDataHandler.cs:8-77` — handler signature now takes `ISubscriptionStore subscriptionStore` + `ISubscriptionProvider subscriptionProvider` (commit `cf958ec`). Lines 26-42: before anonymize/delete, fetch `activeSubscription = GetByUserIdAsync(userId, includeCanceled: false)`. If non-null, `try { subscriptionProvider.CancelScheduledChargeAsync(paymentSourceId) } catch { LogWarning }`. Lines 44-59 continue to anonymize or hard-delete based on `HasPaymentsAsync`. The `try/catch` ensures Art. IV honesty: Wompi failure is logged with structured `subscriptionId`+`userId` but does NOT block ARCO cascade-delete; FK cascade on `subscriptions.user_id → users.id` still removes the row even if Wompi is unreachable (cascade is at DB level, not application level).
  - `src/BuildCv.Infrastructure/Persistence/EfUserDataStore.cs` — unchanged (cascade works at DB level via FK `ON DELETE CASCADE` per migration `20260625184302_AddSubscriptions`; verified by `AddSubscriptionsMigrationTests.Migration_declares_FK_to_users_with_cascade_delete`).
  - `src/BuildCv.Infrastructure/Payments/WompiRecurringAdapter.cs:63-75` — `CancelScheduledChargeAsync` exists and calls `DELETE /v1/subscriptions/{chargeId}` with bearer token, returns `IsSuccessStatusCode`.
  - Tests: `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs:369-506` — three new tests added:
    - `DeleteUserDataHandler_Anonymize_PreCancelsWompiScheduledCharge_BeforeCascade` (line 370) — asserts `subscriptionProvider.CancelledPaymentSources` contains `"ps_arc_cancel"`.
    - `DeleteUserDataHandler_Anonymize_WithoutSubscription_DoesNotCallProvider` (line 417) — asserts no provider call when user has no active sub.
    - `DeleteUserDataHandler_Anonymize_ContinuesEvenIfWompiCancelFails` (line 459) — asserts result is `IsSuccess` even when `CancelChargeOverride` throws `InvalidOperationException("Wompi unreachable")`. This is the Art. IV honesty test: ARCO must not fail because Wompi is down.
- **Current behavior (verified)**: On `DELETE /api/v1/user/data`, if user has an active subscription, Wompi `DELETE /v1/subscriptions/{paymentSourceId}` is called BEFORE the user record is anonymized or hard-deleted. If Wompi fails, the ARCO cascade still proceeds with a `LogWarning`. Payments and invoices remain per 011-factus (handled by `HasPaymentsAsync` branch).
- **Edge cases already handled**:
  - User with active subscription + paid invoices → anonymize + Wompi pre-cancel.
  - User with active subscription + no payments → hard-delete + Wompi pre-cancel.
  - User with no subscription → no Wompi call (test asserts).
  - Wompi unreachable → `LogWarning`, ARCO proceeds, FK cascade still removes subscription row. Wompi's own retry sequence will eventually clean up the scheduled charge.
  - User with already-canceled subscription → `GetByUserIdAsync(includeCanceled: false)` returns null → no Wompi call. (Implicit: a canceled subscription has no scheduled charge left; Wompi cancel was already invoked at user-cancel time.)
- **Edge cases NOT explicitly tested (potential gaps)**:
  - `PaymentSourceId` was deleted/expired on Wompi side → `CancelScheduledChargeAsync` returns 404 → handler currently treats that as success (`response.IsSuccessStatusCode == false`) but does not branch on this. The `try/catch` swallows any exception but a non-success HTTP response simply returns false without throwing, so the cascade proceeds with `LogInformation("... pre-canceled Wompi ...")`. Inconsistent log level: should `LogWarning` on `IsSuccessStatusCode == false`. Minor logging issue, not a correctness issue.
  - Subscription was created but `NextChargeAt` is in the past (already-due retry) → pre-cancel may race with Wompi's retry attempt. Wompi's retry will likely succeed and charge the user; the canceled sub status on our side is irrelevant after the user is anonymized. Low risk.
- **Risks**: Low. Only the false-success logging edge case (returns false but logs success) is worth a follow-up but is not blocking — does not affect user-visible behavior or Constitution compliance.
- **Complexity**: **simple** — already implemented; only documentation remains. One optional micro-improvement on logging accuracy.
- **Recommended approach**: Reuse commit `cf958ec` as the canonical W2 fix. Consider a follow-up patch for `LogWarning` when Wompi returns non-2xx, but this is not part of 017 scope.

### R10 (W3) — Privacy policy v3

- **Files (current state)**:
  - `src/BuildCv.Application/Features/Consent/PrivacyPolicyQueryHandler.cs:61-104` — `new PrivacyPolicyResponse(Version: 3, Content: """... """)` entry added in commit `5f8db66`. Content includes a new "Section 5: Subscriptions (NEW v3)" paragraph covering all 4 required disclosures:
    1. "Subscription status, period dates (start, end, next charge), retry count, and the Wompi payment source ID" — server-side storage ✅
    2. "Your card details are tokenized Wompi-side and never touch our servers" — Art. III ✅
    3. "When you exercise your ARCO right (delete account), any active subscription is pre-canceled at Wompi before your user record is anonymized, and the subscription row is cascade-deleted" — Art. IX ✅
    4. "When you cancel a subscription, the cancellation is non-refundable for the current period: you keep access until the period end, but you are not charged again, and we do not issue partial refunds" — Art. IV ✅
  - v3 also adds a `"Subscription record"` entry to `DataCategories` and a `"Recurring credit subscription billing (Wompi payment source)"` entry to `Purposes`.
  - `src/BuildCv.Application/Features/Consent/PrivacyPolicyQuery.cs` — unchanged; `Version` is nullable int and defaults to latest.
  - Tests: `tests/BuildCv.Application.Tests/Features/Consent/PrivacyPolicyQueryTests.cs:62-96` — two new tests:
    - `HandleAsync_returns_v3_policy_with_subscription_disclosure` (line 63) — asserts v3 contains "Subscriptions", "Wompi", "tokenized", "ARCO", "non-refundable" plus subscription entries in DataCategories/Purposes.
    - `HandleAsync_without_version_returns_latest_policy_v3` (line 88) — asserts `MaxBy(Version)` now returns 3.
- **Current behavior (verified)**: `GET /api/v1/privacy-policy` (without version) returns v3 (latest); `GET /api/v1/privacy-policy?version=3` returns the new v3 with full subscription disclosure. v1 and v2 remain accessible by version number for backward compat (existing tests at lines 22-44 and 47-60).
- **Edge cases already handled**:
  - Default (no version) returns v3 (latest) — `MaxBy(p => p.Version)`.
  - Explicit version 1, 2, 3 all work.
  - Non-existent version throws `KeyNotFoundException` → endpoint maps to 404.
- **Edge cases NOT explicitly tested (potential gaps)**:
  - Web frontend does not yet fetch v3 explicitly — relies on default. Should verify the BFF privacy route uses `PrivacyPolicyQuery()` without version; if it pins version=2, v3 won't be served. (Out of scope for API change but should be flagged in 017 verify.)
  - The DisclosureForSubscriptions text references pre-cancel + cascade-delete but does not enumerate which DIAN invoice fields are retained; consistent with v2 wording.
- **Risks**: Low. Pure documentation; no behavior change other than which version is served as "latest."
- **Complexity**: **simple** — already implemented; only documentation remains.
- **Recommended approach**: Reuse commit `5f8db66` as the canonical W3 fix. Recommend adding a frontend smoke check that the BFF privacy route uses `Version=null` (default = latest).

## Cross-WARNING concerns

1. **Single-commit cohesion**: All 3 fixes were committed in a 5-minute window (15:27 → 15:32) on the same day. They share a dependency: the `ISubscriptionProvider.CancelScheduledChargeAsync` port introduced in 016 is consumed by W1 and W2. The W3 fix is independent of subscriptions.
2. **DI wiring**: The W2 fix added two new constructor parameters (`ISubscriptionStore`, `ISubscriptionProvider`) to `DeleteUserDataHandler`. All 7 existing ARCO tests in `ArcoHandlerTests.cs` were updated to pass `new TestSubscriptionStore()` + `new TestSubscriptionProvider()` — confirms DI was updated in `Program.cs` at the time (no orphan DI registration). Worth re-verifying in `sdd-verify`.
3. **Backward compat verified by 018**: 018's verify-report and archive-report both list 017 in the backward-compat matrix and confirm all 017 tests pass. So 018's `+113 tests` baseline includes the 3 new tests from W1 (1 test) + W2 (3 tests) + W3 (2 tests) = 6 tests added by 017. This is the most reliable cross-feature confirmation that 017 is actually wired and passing.
4. **No cross-WARNING dependencies between fixes**: W1, W2, W3 touch disjoint code paths (subscription cancel, ARCO handler, privacy policy). Could be archived independently if needed.
5. **Documentation gap (dominant concern)**: The 016 verify-report.md was authored BEFORE the 3 fix commits landed but committed AFTER them (commit `4cad307` 19:02 < W1 15:27 etc.). So the verify-report describes the state PRE-fix, not POST-fix. The 016 verify-report.md should be retroactively updated to reflect the W1/W2/W3 ✅ PASS state (or alternatively, those R's should be marked "deferred to 017 which closed them on same day").
6. **INDEX inconsistency**: `000-INDEX.md` line 44 still shows 017 as `📋 PLANEADO`. This is wrong — code is shipped. Index must be updated.
7. **Spec convention violation**: Per `000-INDEX.md` rule "Cada feature nueva DEBE tener los 7 artifacts (spec, plan, research, data-model, quickstart, tasks, contracts)". Change 017 has zero artifacts. For a followup change that is purely 3 WARNING closures (not a new feature), the convention can arguably be relaxed to a shorter trail (proposal + verify-report + archive-report), but at minimum the verify-report + archive-report MUST be authored to legitimately close the change.

## Pre-existing tech debt affecting fixes

None of the 3 fixes introduce tech debt. They actually close small tech debts:

1. The "404 instead of 200 on second cancel" was a fragility in the HTTP contract — now closed.
2. The ARCO anonymize was incomplete relative to Art. IX (Habeas Data) — now compliant.
3. The privacy policy was missing explicit subscription disclosure required by Art. III/IV/IX — now complete.

Pre-existing tech debt NOT addressed by 017 (visible in 016 verify-report §SUGGESTION):

- `ISubscriptionService` interface defined but only `ISubscriptionStore`/`ISubscriptionProvider`/`ISubscriptionFeatureFlag` are wired.
- `SubscriptionEndpoints.cs` exception matching on string content (`ex.Message.Contains("already has")`) — fragile, a typed error code would be more robust.
- `SubscriptionReconciliationWorker` retries create a NEW scheduled charge via `provider.CreateScheduledChargeAsync` instead of re-attempting the existing Wompi scheduled charge — may create duplicate charges in production.

None of these block 017 archive. They are explicit "SUGGESTION, not WARNING" items in 016 verify-report and were deferred to v1.5.

## PR recommendation

**No new PR needed.** All 3 WARNINGs are already implemented and merged to `main` via the 3 historical commits (`caaaf35`, `cf958ec`, `5f8db66`). 018 already verified 017 backward compat.

Recommended next actions (in order):

1. **Skip sdd-propose** — there is no new scope to propose. The scope was already executed.
2. **Author the missing SDD artifacts in `specs/017-subscription-followups/`**:
   - `exploration.md` (this file) ✅
   - `proposal.md` (1-2 pages: scope = "close 3 WARNINGs from 016 verify", approach = "re-document the 3 fix commits as the canonical resolution")
   - `spec.md` (lightweight: 3 R's matching W1/W2/W3 acceptance, derived from 016 verify-report §Gaps)
   - `design.md` (optional: re-summarize the architecture decisions, link to 016 design.md)
   - `tasks.md` (3 tasks, each pointing at the existing commit hash; forecast 6 tests; actual 6 tests)
   - `verify-report.md` (run the gates: lint, typecheck, test, build, constitution-check; assert 18/18 relevant tests pass; reference 018 verify-report as the cross-feature backward-compat evidence)
   - `archive-report.md` (link 3 commits, summary, INDEX sync)
3. **Update `specs/000-INDEX.md` line 44**: change `017 | subscription-followups | 📋 PLANEADO` to `✅ SHIPPED + ARCHIVED` with tag (do not push tag per project rules).
4. **Retroactively update `specs/016-subscription-recurring/verify-report.md` §Gaps**: note that W1/W2/W3 were closed on the same day in change 017, with commit SHAs. Mark R5/R8/R10 from WARNING to PASS (referencing 017).
5. **Skip sdd-apply** — code is already on `main`.
6. **Skip sdd-verify beyond confirming test counts** — 018's verify-report already validates backward compat.
7. **sdd-archive**: commit the 7 artifacts + INDEX sync in one work-unit commit `chore(017): sdd-archive — 3 WARNINGs closed retroactively + INDEX sync`. Tag local-only as `017-subscription-followups-v1.0` at the most recent 017 fix commit hash (`5f8db66`).

**Combined into 1 PR or 2 PRs?** **1 commit / no PR needed** because there is no new code. All work is documentation authoring in a single repo. The 3 commits already exist on `main`. If we wanted to be strict about the 400-line budget, the 7 artifacts + INDEX update is well under 400 lines (rough estimate ~400 lines total for proposal+spec+tasks+verify+archive, no code), so even as a single PR it would be on budget.

**Why 1 commit, not 2?** The 3 fixes are completely independent code-wise (different handlers, different test files, different concerns), so they could have been 3 separate work-units. But since the code is already shipped and the remaining work is docs-only, splitting further adds churn for no reviewer benefit. Single docs commit is clean.

## Risk assessment

**Overall: low.**

- Code: ✅ shipped, tests green, 018 already validated backward compat.
- Spec: ⚠️ missing artifacts (proposal/spec/design/tasks/verify/archive). Risk is project-convention noncompliance, not technical correctness.
- Index: ⚠️ shows wrong status (PLANEADO vs shipped). Easy fix.
- 016 verify-report: ⚠️ stale (says WARNING, should say PASS or "closed by 017"). Easy fix.
- Constitution Art. IX compliance: ✅ W2 closes the Habeas Data pre-cancel gap; W3 closes the policy disclosure gap. 016 verify-report's Art. IX status can now move from "⚠️ PARTIAL" to "✅ PASS".
- Frontend: 🟡 W3 (privacy policy v3) needs frontend confirmation that the BFF route uses default-version (not pinned to v2). Not blocking — should be a smoke check during verify.

## Ready for next phase

**Recommendation: skip sdd-propose, go directly to sdd-spec (lightweight) → sdd-tasks (3 tasks, all "write artifact" type) → sdd-apply (docs-only, no code) → sdd-verify (test count + constitution check) → sdd-archive (INDEX sync + tag).**

Or, if the user prefers speed over ceremony: **skip everything except sdd-archive** (write proposal + verify-report + archive-report inline, sync INDEX, tag, done). The SDD skill chain is overkill for a pure documentation catch-up where code already shipped.

This is a "retroactive artifact authoring" change — the smallest valid sdd-archive flow is: write `proposal.md` (1 page) + `verify-report.md` (gate results + 18/18 tests + 3 commit SHAs) + `archive-report.md` (link to existing commits + INDEX sync note). Skip `design.md` and `tasks.md` entirely — neither adds value when the implementation is 3 historical commits.

## Files mapped

### Code (already shipped, on `main`)

| Path | Lines | Role |
|------|-------|------|
| `src/BuildCv.Application/Features/Subscriptions/CancelSubscriptionHandler.cs` | 11-34 | W1 idempotent path |
| `src/BuildCv.Api/Endpoints/SubscriptionEndpoints.cs` | 85-110 | W1 HTTP mapping (404 → 200 on already-canceled) |
| `src/BuildCv.Application/Features/Auth/DeleteUserDataHandler.cs` | 8-77 | W2 Wompi pre-cancel before cascade |
| `src/BuildCv.Application/Features/Consent/PrivacyPolicyQueryHandler.cs` | 61-104 | W3 v3 policy with subscription disclosure |
| `src/BuildCv.Infrastructure/Payments/WompiRecurringAdapter.cs` | 63-75 | `CancelScheduledChargeAsync` HTTP DELETE |

### Tests (already shipped, on `main`)

| Path | Lines | Test |
|------|-------|------|
| `tests/BuildCv.Application.Tests/Features/Subscriptions/CancelSubscriptionHandlerTests.cs` | 61-85 | `HandleAsync_returns_existing_canceled_subscription_when_called_twice` |
| `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs` | 369-415 | `DeleteUserDataHandler_Anonymize_PreCancelsWompiScheduledCharge_BeforeCascade` |
| `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs` | 417-457 | `DeleteUserDataHandler_Anonymize_WithoutSubscription_DoesNotCallProvider` |
| `tests/BuildCv.Application.Tests/Features/Auth/ArcoHandlerTests.cs` | 459-506 | `DeleteUserDataHandler_Anonymize_ContinuesEvenIfWompiCancelFails` |
| `tests/BuildCv.Application.Tests/Features/Consent/PrivacyPolicyQueryTests.cs` | 62-85 | `HandleAsync_returns_v3_policy_with_subscription_disclosure` |
| `tests/BuildCv.Application.Tests/Features/Consent/PrivacyPolicyQueryTests.cs` | 87-96 | `HandleAsync_without_version_returns_latest_policy_v3` |

### Docs (must be authored to close 017)

| Path | Status |
|------|--------|
| `specs/017-subscription-followups/exploration.md` | ✅ this file (created) |
| `specs/017-subscription-followups/proposal.md` | 🔲 to author |
| `specs/017-subscription-followups/spec.md` | 🔲 to author (lightweight) |
| `specs/017-subscription-followups/design.md` | 🔲 optional / skip |
| `specs/017-subscription-followups/tasks.md` | 🔲 to author (3 docs tasks) |
| `specs/017-subscription-followups/verify-report.md` | 🔲 to author |
| `specs/017-subscription-followups/archive-report.md` | 🔲 to author |
| `specs/000-INDEX.md` (line 44) | 🔲 update PLANEADO → SHIPPED + ARCHIVED |
| `specs/016-subscription-recurring/verify-report.md` (W1/W2/W3 sections) | 🔲 mark WARNING → PASS-closed-by-017 |

### Backward-compat evidence (already authored)

| Path | Line | Quote |
|------|------|-------|
| `specs/018-cv-iteration-loop/verify-report.md` | 161 | "017-subscription-followups | (in Application 251) | `PrivacyPolicyQueryHandler` + `DeleteUserDataHandler` + `CancelSubscriptionHandler` tests still pass" |
| `specs/018-cv-iteration-loop/archive-report.md` | 316 | "017-subscription-followups | (in Application 261) | No changes" |
| `specs/018-cv-iteration-loop/archive-report.md` | 318 | "**Total backward compat verified**: All 011-017 + 002/003/005/009/010 test suites pass unchanged. ✅" |
| `specs/018-cv-iteration-loop/archive-report.md` | 380 | "2. **017-subscription-followups** — close the 3 deferred WARNINGs from 016-subscription-recurring verify (W1 cancel idempotency, W2 ARCO anonymize pre-cancel Wompi charge, W3 privacy policy v3). ~200 lines / 1-2 PRs." |
