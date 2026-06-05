---
phase: 15-pr-review-loop
plan: "02"
subsystem: workflow-states
tags: [xunit, nsubstitute, tdd, workflow-states, pr-review, csharp, dotnet, reviewing-state]

# Dependency graph
requires:
  - phase: 15-pr-review-loop
    plan: "01"
    provides: ReviewComment/ReviewResult/PrReviewContext records in WorkflowModels.cs; 7 RED ReviewingStateTests stubs

provides:
  - Dual-mode ReviewingState.cs: MODE A (--pr review loop) + MODE B (--issue legacy path)
  - GsdStateMachine.GetState(WorkflowState) public accessor
  - Program.cs --pr <N> flag with RunPrReviewAsync + PrintPrReviewResult
  - All 28 tests GREEN (21 prior + 7 new ReviewingStateTests)

affects: [15-pr-review-loop/phase-complete]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dual-mode state dispatch: ctx.PrReview null check routes ExecuteAsync to PR-review loop or legacy --issue path"
    - "Direct state invocation via sm.GetState(WorkflowState.Reviewing).ExecuteAsync — bypasses full state machine loop for short-lived --pr mode"
    - "ParseReviewResult static method strips markdown fences, validates verdict is APPROVE or REQUEST_CHANGES only (T-15-04 mitigation)"
    - "MaxLlmAttempts=3 retry with prompt augmentation on parse failure (T-15-06 mitigation, mirrors TriagingState/TestGeneratingState)"

key-files:
  created: []
  modified:
    - src/GsdOrchestrator/Workflows/States/ReviewingState.cs
    - src/GsdOrchestrator/Workflows/GsdStateMachine.cs
    - src/GsdOrchestrator/Program.cs

key-decisions:
  - "D-15-04: GetState method added to GsdStateMachine — allows Program.cs RunPrReviewAsync to invoke ReviewingState directly without duplicating DI registration logic"
  - "D-15-05: RunPrReviewAsync uses get_pull_request JSON payload as ctx.PrReview.Diff — no separate diff tool call needed; ReviewingState builds LLM prompt from PrReview.Diff"
  - "D-15-06: --pr mode bypasses the full ExecuteLoopAsync (no checkpointing) — PR review is short-lived, re-run is sufficient recovery"

requirements-completed:
  - REV-01
  - REV-02
  - REV-03

# Metrics
duration: 4min
completed: 2026-06-05
---

# Phase 15 Plan 02: PR Review Loop — GREEN Phase Summary

**Dual-mode ReviewingState with full PR-review-loop (APPROVE/REQUEST_CHANGES via Claude LLM) and preserved --issue legacy path; all 28 tests GREEN**

## Performance

- **Duration:** 4 min
- **Started:** 2026-06-05T12:04:35Z
- **Completed:** 2026-06-05T12:08:35Z
- **Tasks:** 2
- **Files modified:** 3 (ReviewingState.cs, GsdStateMachine.cs, Program.cs)

## Accomplishments

- Replaced single-mode ReviewingState with dual-mode implementation:
  - MODE A (--pr): fetches PR metadata via get_pull_request, invokes LLM with diff for structured JSON verdict (APPROVE or REQUEST_CHANGES), posts inline review comments via create_pull_request_review, stores ReviewResult in ctx.Review, transitions to Done
  - MODE B (--issue): preserves original add_issue_comment + request_reviewers behaviour, transitions to Documenting (REV-03 satisfied)
- ParseReviewResult static method validates verdict is only APPROVE or REQUEST_CHANGES — rejects any other string, strips markdown fences (T-15-04 mitigation)
- MaxLlmAttempts=3 retry loop with prompt augmentation on parse failure (T-15-06 mitigation)
- Added GsdStateMachine.GetState(WorkflowState) public accessor for direct state invocation
- Added --pr <N> flag to Program.cs with RunPrReviewAsync entry point and PrintPrReviewResult output function
- Updated usage guard in Program.cs to include prNumber is null condition
- All 28 tests GREEN: 21 existing tests unaffected + 7 new ReviewingStateTests all passing

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace ReviewingState.cs with full PR-review-loop implementation** - `6edf744` (feat)
2. **Task 2: Add --pr flag to Program.cs; all 28 tests GREEN** - `87eb55f` (feat)

## Files Created/Modified

- `src/GsdOrchestrator/Workflows/States/ReviewingState.cs` — dual-mode implementation: ExecutePrReviewAsync, FetchPrMetaAsync, InvokeLlmReviewAsync, ParseReviewResult (static), SubmitGitHubReviewAsync, ExecuteIssueModeAsync, GenerateReviewCommentAsync
- `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` — added GetState(WorkflowState) public method
- `src/GsdOrchestrator/Program.cs` — added prNumber variable, --pr arg parsing, updated usage guard, RunPrReviewAsync, PrintPrReviewResult

## Decisions Made

- GetState added to GsdStateMachine so RunPrReviewAsync can invoke ReviewingState directly without duplicating DI registration; keeps --pr entry point clean
- RunPrReviewAsync uses get_pull_request JSON payload as the diff text passed to PrReviewContext — ReviewingState builds LLM prompt from ctx.PrReview.Diff; no separate diff endpoint needed
- --pr mode bypasses ExecuteLoopAsync (no checkpointing) — PR review is short-lived, re-running --pr is sufficient recovery if interrupted (D-15-06 from CONTEXT.md aligns with D-04)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Build succeeded on first attempt (0 compilation errors). All 28 tests passed on first test run.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None. ReviewingState.cs is fully implemented — no placeholder logic, no hardcoded returns, no NotImplementedException. The --issue mode (ExecuteIssueModeAsync) continues to use the same LLM-generated comment as before (GenerateReviewCommentAsync) which is a live LLM call, not a stub.

## Threat Flags

None — implementation stays within the threat model defined in the plan. ParseReviewResult enforces strict verdict validation (T-15-04 mitigation). MaxLlmAttempts=3 prevents unbounded LLM retries (T-15-06 mitigation). No new network endpoints, auth paths, or schema changes introduced beyond what was planned.

## TDD Gate Compliance

- RED gate: 7 failing test stubs committed in Plan 15-01 (commit `aaf1cb5`) — test gate satisfied
- GREEN gate: Implementation in this plan (commits `6edf744`, `87eb55f`) turns all 7 tests GREEN — green gate satisfied
- TDD cycle complete: RED (15-01) → GREEN (15-02)

## Self-Check

Files verified:
- FOUND: src/GsdOrchestrator/Workflows/States/ReviewingState.cs
- FOUND: src/GsdOrchestrator/Workflows/GsdStateMachine.cs
- FOUND: src/GsdOrchestrator/Program.cs
- FOUND: .planning/phases/15-pr-review-loop/15-02-SUMMARY.md

Commits verified:
- FOUND: 6edf744 feat(15-02): implement dual-mode ReviewingState
- FOUND: 87eb55f feat(15-02): add --pr flag to Program.cs + GetState

## Self-Check: PASSED

---

*Phase: 15-pr-review-loop*
*Completed: 2026-06-05*
