# Apply Progress: Fail-Closed Security Containment

## Current Batch

| Field | Value |
|---|---|
| Work unit | A0 — Iteration API Containment |
| Mode | Strict TDD |
| Delivery | Force-chained, stacked-to-main, A0 only |
| Status | A0 implemented; full regression blocked by unrelated local environment failures |
| Review budget | 400 changed lines; no size exception |

## Cumulative History

1. The first safety-net run stopped before RED with 6 passed and 2 failed existing tests. Anonymous POST returned 200 instead of 401; anonymous GET returned 404 instead of 401. No production changes were made.
2. A read-only incident audit identified ignored Development configuration leaking `LocalAuth` and an unpinned AI provider into the iteration fixture. Production authorization was confirmed correct.
3. The approved task 1.2 prerequisite pinned `LocalAuth:Enabled=false` and `Ai:Provider=Stub` only in `IterationTestWebApplicationFactory`. The exact safety net then passed 8/8.
4. A0 proceeded through behavioral RED, minimum GREEN registration gating, documentation/configuration, and required verification commands.

## Completed Tasks

- [x] 1.1 Added missing/false containment integration coverage with malformed POST, GET, and poison `IIterationService` registration.
- [x] 1.2 Added compatibility-only true configuration while preserving existing authentication, rate-limit, contract, and behavior tests.
- [x] 1.3 Added the fail-closed composition-root gate and Boolean-only startup log.
- [x] 1.4 Proved focused missing/false closure and explicit-true compatibility.
- [x] 1.5 Kept test configuration hermetic, documented compatibility-only enablement, set Render false, and kept appsettings key-free.
- [x] 1.6 Ran every required verification command and recorded the unrelated full-suite environment failures.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `IterationEndpointsContainmentTests.cs` | Integration | ✅ Existing suite 8/8 | ✅ 2 containment cases failed; 8 passed | ✅ Focused suite 10/10 | ✅ Missing/false, POST/GET, poison service | ➖ No code refactor needed |
| 1.2 | `IterationEndpointsTests.cs` | Integration | ✅ 8/8 after approved fixture prerequisite | ✅ Combined suite remained RED on 2 containment cases | ✅ Existing 8 compatibility cases pass with explicit true | ✅ Auth, contracts, and application behavior retained | ✅ Pinned LocalAuth off and Stub provider |
| 1.3 | `Program.cs` | Integration | ✅ RED tests written first | ✅ Native route absence not implemented | ✅ 10/10 after minimum registration gate | ✅ Missing, false, and true paths | ➖ Direct configuration read is already minimal |
| 1.4 | `IterationEndpoints*.cs` | Integration | ✅ 8/8 | ✅ Recorded before implementation | ✅ Final focused suite 10/10 | ✅ 3 gate states | ➖ Verification task |
| 1.5 | `README.md`, `render.yaml` | Structural | ✅ Focused suite 10/10 | Covered by prior containment RED | ✅ Focused suite remained 10/10 | ➖ Configuration evidence only | ✅ No global true default added |
| 1.6 | Solution | Verification | ✅ Release build passed | N/A — verification task | ⚠️ Full suite environment-blocked | N/A | ✅ Format and dependency checks passed |

## RED and GREEN Evidence

Safety-net command:

```bash
dotnet test tests/BuildCv.Api.IntegrationTests/BuildCv.Api.IntegrationTests.csproj --filter "FullyQualifiedName~IterationEndpointsTests"
```

- Initial attempt: 6 passed, 2 failed, 0 skipped.
- After approved fixture prerequisite: 8 passed, 0 failed, 0 skipped.

RED/GREEN command:

```bash
dotnet test tests/BuildCv.Api.IntegrationTests/BuildCv.Api.IntegrationTests.csproj --filter "FullyQualifiedName~IterationEndpoints"
```

- RED: 8 passed, 2 failed; both missing/false cases received 401 instead of 404/405.
- GREEN: 10 passed, 0 failed, 0 skipped.
- Final focused rerun: 10 passed, 0 failed, 0 skipped.

## Verification

| Command | Result |
|---|---|
| `dotnet build BuildCv.slnx -c Release` | Passed; 0 warnings, 0 errors |
| `dotnet test` | Environment-blocked; Domain 162/162 and Application 353/353 passed; Infrastructure 429 passed/14 failed; API 138 passed/18 failed |
| `dotnet format --verify-no-changes` | Passed |
| `dotnet list src/BuildCv.Domain package references` | CLI rejected obsolete plural syntax |
| `dotnet list src/BuildCv.Domain package` | Passed; no packages |
| `dotnet list src/BuildCv.Domain reference` | Passed; no project references |
| `git diff --check` plus new-file check | Passed |
| Changed-file suppression scan | Passed; no suppressions added |
| `appsettings*.json` gate scan | Passed; key remains absent |

Full-suite failures are unrelated to A0: PostgreSQL rejected the workstation `postgres` credentials, and other existing API fixtures inherited ignored Development LocalAuth/provider/rate-limit configuration.

## Files Changed

- `src/BuildCv.Api/Program.cs` — fail-closed iteration route registration and Boolean-only log.
- `tests/BuildCv.Api.IntegrationTests/IterationEndpointsContainmentTests.cs` — missing/false route absence and poison-service coverage.
- `tests/BuildCv.Api.IntegrationTests/IterationEndpointsTests.cs` — hermetic fixture prerequisite and compatibility-only true gate.
- `README.md` — compatibility and rollback guidance.
- `render.yaml` — explicit production false value.
- `openspec/changes/025-security-containment/tasks.md` — A0 task progress.
- `openspec/changes/025-security-containment/apply-progress.md` — cumulative evidence.

## Review Size

- A0 implementation, tests, docs, and deployment configuration: 81 changed lines (80 additions, 1 deletion).
- SDD progress/checklist artifacts remain below the 400-line hard stop when added to the A0 product diff.
- No size exception was used.

## Remaining Work

- [ ] 1.7 Merge/deploy A0 before B0; this executor did not commit, push, create a PR, merge, or deploy.
- [ ] B0 tasks 2.1–2.9 remain pending and untouched.

## Blockers and Risks

- Full regression cannot be declared green in the current workstation environment.
- A0 must be merged/deployed before any B0 work under the stacked-to-main strategy.
