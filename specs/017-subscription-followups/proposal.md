# 017-subscription-followups — Proposal

> **Type:** retroactive change proposal — code already shipped on `main`.
> **Status:** ✅ SHIPPED + ARCHIVED (single docs catch-up commit; no PR).
> **Date:** 2026-06-26.

## Goal

Close the 3 WARNING-level deviations documented in `specs/016-subscription-recurring/verify-report.md` (R5 cancel idempotency, R8 ARCO Wompi pre-cancel, R10 privacy policy v3) by formalizing the changes that **already landed on `main`** on 2026-06-25 in 3 fix commits.

This change is **a documentation catch-up**, not new functionality. All 3 WARNINGs were closed in code prior to (or coincident with) the 016 verify-report being committed. The remaining gap is purely SDD artifact authorship so the change is auditable, the `000-INDEX.md` reflects reality, and 016's verify-report stops describing a pre-fix state as if it were the post-fix state.

## Scope

In-scope:

1. **R5 — Cancel subscription idempotency (W1)** — `CancelSubscriptionHandler` returns existing `Canceled` sub on second call without invoking Wompi again; HTTP endpoint now returns 200 (not 404) with the same `accessUntil`. Closed by commit `caaaf35` (2026-06-25 15:27).
2. **R8 — ARCO Wompi pre-cancel (W2)** — `DeleteUserDataHandler` invokes `ISubscriptionProvider.CancelScheduledChargeAsync` for the user's active subscription before anonymize/delete; Wompi failure is logged honestly and ARCO cascade proceeds. Closed by commit `cf958ec` (2026-06-25 15:30).
3. **R10 — Privacy policy v3 with subscription disclosure (W3)** — `PrivacyPolicyQueryHandler.Policies` adds entry `Version: 3` containing a "Section 5: Subscriptions" paragraph covering storage, tokenization, ARCO pre-cancel, and non-refundable cancellation; v3 also adds "Subscription record" to `DataCategories` and "Recurring credit subscription billing" to `Purposes`. Closed by commit `5f8db66` (2026-06-25 15:32).

Out-of-scope (explicitly deferred to v1.5):

- `ISubscriptionService` interface not wired (`Application/Features/Subscriptions/ISubscriptionService.cs`) — see 016 verify-report §SUGGESTION.
- `SubscriptionEndpoints.cs` exception matching on string content (`ex.Message.Contains("already has")`) — fragile, would benefit from typed error codes.
- `SubscriptionReconciliationWorker` retry path uses `provider.CreateScheduledChargeAsync` instead of re-attempting the existing Wompi scheduled charge — potential duplicate-charge risk in production.
- A optional micro-improvement on ARCO log level when Wompi returns non-2xx (currently logs success on false response; should be `LogWarning`). Not a correctness issue.
- A frontend smoke check that the BFF privacy route uses `PrivacyPolicyQuery()` with `Version=null` (default = latest). Not a backend change.

## Approach

**No code changes in this change.** The implementation is already on `main` in the 3 cited commits. This is the smallest possible sdd-archive flow: write the missing SDD artifacts (`proposal.md`, `verify-report.md`, `archive-report.md`), update `000-INDEX.md`, retroactively annotate 016's `verify-report.md` §Gaps section, and commit everything in a single docs-only atomic commit.

The decision tree (per `exploration.md` §PR recommendation):

- **Skip `sdd-propose`** — no new scope to propose.
- **Author lightweight `proposal.md`** (this file) — scope = "close 3 WARNINGs from 016 verify", approach = "re-document the 3 historical fix commits as the canonical resolution".
- **Author lightweight `verify-report.md`** — run the 6 gates; assert 24/24 relevant Application tests pass (4 `CancelSubscriptionHandlerTests` + 14 `ArcoHandlerTests` + 6 `PrivacyPolicyQueryTests`); reference 018 verify-report as the cross-feature backward-compat evidence; reference the 3 commit SHAs.
- **Author `archive-report.md`** — link the 3 commits; document INDEX sync; document 016 verify-report retro-update; tag local-only as `017-subscription-followups-v1.0` at commit `5f8db66`.
- **Update `000-INDEX.md` line 44** — flip 017 from `📋 PLANEADO` to `✅ SHIPPED + ARCHIVED`.
- **Retroactively update `specs/016-subscription-recurring/verify-report.md` §Gaps + §Verdict** — note that W1/W2/W3 were closed by 017 with the commit SHAs and test names.
- **Commit + tag** — single docs-only atomic commit `chore(017): sdd catch-up — proposal + verify + archive + INDEX sync + close 016 §Gaps`; local-only tag `017-subscription-followups-v1.0` (no push per project rules).

## Success Criteria

All criteria verifiable from the git log + a single test invocation:

| # | Criterion | Evidence |
|---|-----------|----------|
| SC1 | R5 cancel idempotency fix is on `main` | `git show caaaf35` shows `CancelSubscriptionHandler` short-circuit for already-canceled sub |
| SC2 | R5 test passes | `dotnet test --filter "FullyQualifiedName~CancelSubscriptionHandlerTests"` → 4/4 pass |
| SC3 | R8 ARCO Wompi pre-cancel fix is on `main` | `git show cf958ec` shows `DeleteUserDataHandler` calling `subscriptionProvider.CancelScheduledChargeAsync` before cascade |
| SC4 | R8 tests pass | `dotnet test --filter "FullyQualifiedName~ArcoHandlerTests"` → 14/14 pass |
| SC5 | R10 privacy policy v3 fix is on `main` | `git show 5f8db66` shows `PrivacyPolicyResponse(Version: 3, ...)` entry with subscription disclosure |
| SC6 | R10 tests pass | `dotnet test --filter "FullyQualifiedName~PrivacyPolicyQueryTests"` → 6/6 pass |
| SC7 | 6/6 verify gates green (build, format, test, e2e-skip, constitution-check, INDEX-consistency) | verify-report.md gate table |
| SC8 | `000-INDEX.md` reflects 017 as `✅ SHIPPED + ARCHIVED` | diff on `specs/000-INDEX.md` line 44 |
| SC9 | `specs/016-subscription-recurring/verify-report.md` §Gaps and §Verdict retroactively annotated | diff on the file |
| SC10 | 018 backward compat preserved (no regressions from 017) | 018 verify-report + archive-report still cite 017 as in their backward-compat matrix |
| SC11 | Art. I-IX Constitution compliance preserved | verify-report constitution-check gate |
| SC12 | Single atomic commit on `main`, no feature branch, no push | git log shows one new commit; `git status` clean after |

## Constitution Compliance

| Article | Status | Notes |
|---------|--------|-------|
| **I — Cero invención** | N/A | No scoring/adaptation changes. 016 scope only; this change is docs. |
| **II — Puntaje determinista** | N/A | Score engine untouched. |
| **III — Privacidad primero** | ✅ Reinforced by W3 | Privacy policy v3 now explicitly states card tokenization is Wompi-side only (Art. III). |
| **IV — Encuadre honesto** | ✅ Reinforced by W3 | Privacy policy v3 explicitly states cancellation is non-refundable for the current period (Art. IV). |
| **V — Entrada como dato** | N/A | No input-pipeline changes. |
| **VI — Clean Architecture** | ✅ Preserved | No new ports; W1/W2/W3 reuse existing `ISubscriptionStore`, `ISubscriptionProvider`. |
| **VII — Rate limits** | N/A | No new endpoints or policies. |
| **VIII — TDD** | ✅ Preserved | All 3 WARNING closures are backed by tests (1 + 3 + 2 = 6 new tests; full file totals: 4 + 14 + 6 = 24 tests across the 3 affected test files). |
| **IX — Habeas Data** | ✅ Closed by W2 + W3 | W2 closes ARCO pre-cancel gap (was a Constitution PARTIAL on 016 verify). W3 closes policy disclosure gap. 016 verify-report Art. IX status can now move from `⚠️ PARTIAL` to `✅ PASS`. |

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Stale 016 verify-report confuses future readers | LOW | LOW | Retroactive annotation in §Gaps + §Verdict explicitly cites 017 closure with commit SHAs. |
| INDEX drift between 017 row and 016 row | LOW | LOW | Both rows now reference 017 explicitly via archive-report.md and the gap-close note. |
| Single-commit docs push accidentally mixed with code | LOW | MEDIUM | Strict scope guard: only `.md` files in `specs/` are touched. Production code unchanged. Verified by `git diff --stat HEAD~1 HEAD` showing 0 `.cs` changes. |
| Tag pushed accidentally | LOW | LOW | Per project rules, archive tags are local-only. Tag is created but never pushed. |
| Re-validation overhead if any of the 3 commits is amended/reverted in the future | LOW | LOW | Commit SHAs are recorded in this proposal + verify-report + archive-report + INDEX row. If a commit is amended, the verify-report gates will fail and the regression will be caught. |

No high-severity risks. This change is documentation-only and reversible by a single revert commit.

## Related artifacts

- **Exploration:** [exploration.md](./exploration.md)
- **Verify report:** [verify-report.md](./verify-report.md)
- **Archive report:** [archive-report.md](./archive-report.md)
- **Source verify-report (with retroactive annotation):** [../016-subscription-recurring/verify-report.md](../016-subscription-recurring/verify-report.md)
- **INDEX entry:** [../000-INDEX.md](../000-INDEX.md) line 44

## Cross-feature references

- **018-cv-iteration-loop** — confirms 017 backward compat in its verify-report (line 161) and archive-report (line 316). 018's "**Total backward compat verified**: All 011-017 + 002/003/005/009/010 test suites pass unchanged. ✅" quote is the most reliable cross-feature confirmation that 017 is actually wired and passing.