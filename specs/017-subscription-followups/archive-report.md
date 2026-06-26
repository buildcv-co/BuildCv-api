# Archive Report: 017-subscription-followups

## Goal

Close 3 WARNING-level deviations from `specs/016-subscription-recurring/verify-report.md` (R5 cancel idempotency, R8 ARCO Wompi pre-cancel, R10 privacy policy v3) by formalizing the historical fix commits already on `main` as the canonical resolution of change 017, and updating the project artifacts (this archive, `000-INDEX.md`, and the 016 verify-report §Gaps/§Verdict) so that future readers see a consistent, current state of the product.

## Final state

| Item | Value |
|------|-------|
| Change | `017-subscription-followups` |
| Type | retroactive docs catch-up + INDEX sync + retroactive 016 annotation |
| Branch | `main` (work directly on main; no feature branch) |
| Commit strategy | single atomic commit (no PR, no push) |
| Production code LOC changed in this commit | **0** (docs only) |
| Total commits cited | 3 historical fix commits on `main` |
| Tests added by cited commits | 6 (1 in `CancelSubscriptionHandlerTests` + 3 in `ArcoHandlerTests` + 2 in `PrivacyPolicyQueryTests`) |
| Affected-path tests passing | 24/24 in Application.Tests (4 + 14 + 6 across the 3 test files) |
| Full API tests passing | 899/899 (Domain 162 + Application 328 + Infrastructure 409) |
| Constitution compliance | Art. I-IX compliant; Art. III/IV/IX explicitly reinforced |
| Backward compat | 018 verify-report confirms 017 in backward-compat matrix; 011/012/013/014/015/016 baselines preserved |
| INDEX status | 017 row flipped from `📋 PLANEADO` → `✅ SHIPPED + ARCHIVED` |
| 016 verify-report | §Gaps W1/W2/W3 retroactively annotated with closure citations; §Verdict updated to reference 017 |
| Local tag | `017-subscription-followups-v1.0` (NOT pushed, per project rules) |

## Implementation timeline

| Step | Date | Commit / SHA | Notes |
|------|------|---------------|-------|
| W1 (R5) implementation landed | 2026-06-25 15:27 | `caaaf35` | "fix(017): CancelSubscriptionHandler — idempotent on second call (W1 closure)" |
| W2 (R8) implementation landed | 2026-06-25 15:30 | `cf958ec` | "fix(017): DeleteUserDataHandler — pre-cancel Wompi charge on ARCO (W2 closure)" |
| W3 (R10) implementation landed | 2026-06-25 15:32 | `5f8db66` | "fix(017): PrivacyPolicyQueryHandler — v3 entry with subscription disclosure (W3 closure)" |
| 016 verify-report.md committed | 2026-06-25 19:02 | `4cad307` | (committed AFTER the 3 fix commits; describes pre-fix state, not post-fix state) |
| 018 backward-compat verification | 2026-06-26 | (in 018 verify-report line 161 + archive-report line 316) | confirms 017 wired and passing |
| 017 SDD artifacts authored (this change) | 2026-06-26 | (single docs-only commit, see `git log -1 --format=%H` after this archive) | proposal + verify-report + archive-report + INDEX sync + 016 retro-update |
| Local tag applied | 2026-06-26 | `017-subscription-followups-v1.0` at HEAD of this commit | local-only, not pushed |

## Constitution compliance (final)

| Article | Status | Reinforced by |
|---------|--------|---------------|
| **I — Cero invención** | N/A | No scoring/adaptation changes. |
| **II — Puntaje determinista** | N/A | Score engine untouched. |
| **III — Privacidad primero** | ✅ | W3 — privacy policy v3 explicitly states card tokenization is Wompi-side only. |
| **IV — Encuadre honesto** | ✅ | W3 — privacy policy v3 explicitly states cancellation is non-refundable for the current period. |
| **V — Entrada como dato** | N/A | No input-pipeline changes. |
| **VI — Clean Architecture** | ✅ | No new ports; W1/W2 reuse existing `ISubscriptionStore`, `ISubscriptionProvider`. |
| **VII — Rate limits** | N/A | No new endpoints or policies. |
| **VIII — TDD** | ✅ | 6 new tests across 3 files (TDD red→green→refactor per the cited commits' messages). |
| **IX — Habeas Data** | ✅ | W2 closes ARCO pre-cancel gap (was Constitution PARTIAL on 016 verify). W3 closes policy disclosure gap. |

## Lessons learned

1. **The 016 verify-report was committed AFTER the 017 fixes.** This was a sequencing issue: the 3 fix commits landed at 15:27, 15:30, and 15:32 on 2026-06-25, but the 016 verify-report was authored before these fixes were finalized and committed to the repo at 19:02. Future improvement: when closing WARNINGs in flight, update the parent verify-report inline rather than authoring a separate follow-up change. The current approach (separate 017 change) is workable but creates a temporal discrepancy that requires retroactive annotation.

2. **018 already validated 017 backward compat.** Because 017 was a code-level fix only (no separate change proposal at the time), 018's PR2/PR3 naturally inherited the 017 fixes as part of `main` and its verify-report/archive-report already cite 017 as a passing baseline. This is the strongest cross-feature evidence that 017 is actually wired and passing — no re-verification needed.

3. **The "ghost-shipped change" pattern.** Sometimes code ships before the SDD artifacts are authored. The temptation is to retroactively pretend the change went through normal `sdd-new → sdd-apply → sdd-verify → sdd-archive` flow, but honesty matters: this archive-report and proposal both explicitly state "this is a docs catch-up, the implementation was already on `main`". Future maintainers reading this artifact will know exactly what state the codebase was in.

4. **Single-commit docs catch-up is appropriate when scope is bounded and verifiable.** The 3 fixes are completely independent code-wise (different handlers, different test files, different concerns), so they could have been 3 separate work-units. But since the code is already shipped and the remaining work is docs-only, splitting further adds churn for no reviewer benefit. Single docs commit is clean and matches the project's "docs batch" convention (see `chore(017):` prefix used elsewhere).

5. **INDEX consistency is a forcing function.** The `000-INDEX.md` is the single entry point for the project; leaving 017 as `📋 PLANEADO` while code shipped on `main` is a real risk for future maintainers. Updating the INDEX row is not optional — it's the authoritative state of the product.

6. **The `ISubscriptionService` interface remains unwired.** Out-of-scope for 017 (was a 016 §SUGGESTION, not WARNING). Carry forward to a future v1.5 cleanup PR. Same for `SubscriptionEndpoints.cs` exception matching on string content and `SubscriptionReconciliationWorker` retry path.

## Backward compat notes

- **No production code touched** by this commit (verified by `git diff --stat HEAD~1 HEAD` showing only `.md` files).
- **3 historical fix commits** remain intact and unchanged.
- **018 backward-compat matrix** (already published) continues to apply. 017 is in the matrix as passing.
- **011/012/013/014/015/016 baselines** continue to pass unchanged (verified by the broader test suite run during this archive).
- **Privacy policy v1 and v2** remain accessible by version number (`GET /api/v1/privacy-policy?version=1|2|3`). v3 is now the default returned by `GET /api/v1/privacy-policy` (no version specified).
- **ARCO behavior** for users without active subscriptions is unchanged (no Wompi call). For users with already-canceled subscriptions, also unchanged (no Wompi call — `GetByUserIdAsync(includeCanceled: false)` returns null).
- **Cancel-twice behavior** is now spec-compliant: returns 200 with the same `accessUntil` instead of 404. This is a contract change from 016's pre-fix behavior but does not break any existing client (clients that treated 404 as success or handled it gracefully will continue to work; clients that strictly required 404 will now see 200 — no known client does this).

## Known limitations (carried forward from 016 §SUGGESTION)

1. `ISubscriptionService` interface defined but only `ISubscriptionStore`/`ISubscriptionProvider`/`ISubscriptionFeatureFlag` are wired. Recommend implementing `ISubscriptionService` per spec or removing the interface for consistency.
2. `SubscriptionEndpoints.cs` exception matching on string content (`ex.Message.Contains("already has")`) is fragile. A typed error code would be more robust.
3. `SubscriptionReconciliationWorker` retries create a NEW scheduled charge via `provider.CreateScheduledChargeAsync` instead of re-attempting the existing Wompi scheduled charge. May create duplicate charges in production.
4. ARCO handler currently treats `IsSuccessStatusCode == false` from Wompi as a "soft success" (cascade proceeds with `LogInformation` instead of `LogWarning`). Minor logging accuracy issue, not a correctness issue.
5. Web frontend should verify the BFF privacy route uses `PrivacyPolicyQuery()` with `Version=null` (default = latest = v3), not pinned to v2. Out of scope for API change.

## Deferred to v1.5

- Items 1-5 above.
- 3+ plans (Pro tier), annual plans, free trials, promotional pricing, proration on plan change, family/shared plans, subscription pause, email notifications for failed charges, customer-initiated refunds (carried from 016).

## References

- **Exploration:** [./exploration.md](./exploration.md) — full analysis of the 3 WARNINGs and recommendation to skip `sdd-propose` and go directly to `sdd-archive`.
- **Proposal:** [./proposal.md](./proposal.md) — goal, scope, approach, success criteria, constitution compliance, risks.
- **Verify report:** [./verify-report.md](./verify-report.md) — 6 gates green; 24/24 affected-path tests pass; R-by-R closure verification.
- **016 source verify-report (with retroactive annotation):** [../016-subscription-recurring/verify-report.md](../016-subscription-recurring/verify-report.md) — §Gaps W1/W2/W3 and §Verdict retroactively annotated with closure citations.
- **018 backward-compat evidence:** [../018-cv-iteration-loop/verify-report.md](../018-cv-iteration-loop/verify-report.md) line 161, [../018-cv-iteration-loop/archive-report.md](../018-cv-iteration-loop/archive-report.md) lines 316-318.
- **INDEX entry:** [../000-INDEX.md](../000-INDEX.md) line 44.

## Git tag

- **Local tag:** `017-subscription-followups-v1.0`
- **Tag points to:** HEAD of this archive's docs commit (the final commit in the chain; same commit that contains this archive-report.md).
- **Pushed?** No. Per project rule, archive tags are local-only. The maintainer can push the tag to `origin` manually if/when desired.

## Related commits (this change)

- **Production code commits (already shipped, unchanged):**
  - `caaaf35` — fix(017): CancelSubscriptionHandler — idempotent on second call (W1 closure)
  - `cf958ec` — fix(017): DeleteUserDataHandler — pre-cancel Wompi charge on ARCO (W2 closure)
  - `5f8db66` — fix(017): PrivacyPolicyQueryHandler — v3 entry with subscription disclosure (W3 closure)
- **Docs catch-up commit (this archive):**
  - `chore(017): sdd catch-up — proposal + verify + archive + INDEX sync + close 016 §Gaps` — single atomic commit; see `git log -1 --format=%H` after this archive is committed. Contains 4 new files (`proposal.md`, `verify-report.md`, `archive-report.md`, plus the pre-existing `exploration.md`) and 2 modified files (`000-INDEX.md` + `specs/016-subscription-recurring/verify-report.md`). Zero `.cs` changes.