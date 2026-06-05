---
phase: 15-pr-review-loop
plan: "01"
subsystem: testing
tags: [xunit, nsubstitute, tdd, workflow-models, pr-review, csharp, dotnet]

# Dependency graph
requires:
  - phase: 14-autonomous-test-generation
    provides: TestGeneratingState LLM structured JSON pattern and NSubstitute mock infrastructure reused in test stubs
  - phase: 13-smarter-issue-triage
    provides: TriagingState LLM retry pattern and NSubstitute mock constructor patterns reused

provides:
  - ReviewComment(Path, Line, Side, Severity, Body) record in WorkflowModels.cs
  - ReviewResult(Verdict, Summary, Comments) record in WorkflowModels.cs
  - PrReviewContext(PrNumber, Owner, Repo, Diff) record in WorkflowModels.cs
  - GsdWorkflowContext.Review (ReviewResult?) property
  - GsdWorkflowContext.PrReview (PrReviewContext?) property
  - ReviewingStateTests.cs with 7 RED [Fact] stubs covering APPROVE/REQUEST_CHANGES/LLM-failure scenarios

affects: [15-pr-review-loop/15-02]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED phase: test stubs compile against current ReviewingState(McpToolDispatcher, IChatClient, IConfiguration, ILogger) but fail at runtime — existing state throws NullReferenceException on PrReview context input"
    - "PrReviewContext as carrier property on GsdWorkflowContext separates input vessel from result vessel (ReviewResult)"

key-files:
  created:
    - src/GsdOrchestrator.Tests/ReviewingStateTests.cs
  modified:
    - src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs

key-decisions:
  - "D-15-01: PrReview (PrReviewContext?) added alongside Review (ReviewResult?) — input carrier separate from result vessel, per CONTEXT.md D-03"
  - "D-15-02: WorkflowState enum unchanged — WorkflowState.Reviewing already existed since original codebase; no new enum value needed in this plan"
  - "D-15-03: Test stubs compile against CURRENT ReviewingState 4-arg constructor — Plan 15-02 will replace the implementation; 15-01 only establishes the test contract"

patterns-established:
  - "ReviewingStateTests follows TriagingStateTests BuildDispatcher/BuildSut pattern exactly: NSubstitute IMcpClient + ResiliencePipelineRegistry stub"
  - "BuildPrContext helper sets PrReview but no IssueContext — makes --pr mode context explicit and distinct from issue pipeline context"

requirements-completed:
  - REV-01
  - REV-02

# Metrics
duration: 4min
completed: 2026-06-05
---

# Phase 15 Plan 01: PR Review Loop — RED Phase Summary

**ReviewComment/ReviewResult/PrReviewContext data contracts added to WorkflowModels.cs; 7 RED xUnit stubs in ReviewingStateTests.cs define the full --pr review contract before any implementation**

## Performance

- **Duration:** 4 min
- **Started:** 2026-06-05T11:57:28Z
- **Completed:** 2026-06-05T12:00:32Z
- **Tasks:** 2
- **Files modified:** 2 (1 modified, 1 created)

## Accomplishments

- Added 3 new records (ReviewComment, ReviewResult, PrReviewContext) and 2 new context properties (Review, PrReview) to WorkflowModels.cs — all additive, no existing code modified
- Created ReviewingStateTests.cs with 7 RED [Fact] stubs covering: APPROVE→Done, REQUEST_CHANGES→Done, APPROVE submits correct review event, REQUEST_CHANGES submits correct review event, inline comments array non-empty, ctx.Review stored on context, LLM parse failure throws
- All 7 new tests FAIL (RED) — current ReviewingState dereferences ctx.Issue/PullRequest which are null in --pr mode context
- All 21 existing tests (TriagingStateTests + TestGeneratingStateTests + GsdStateMachineTests) remain GREEN
- Both production project and test project build succeeded (0 errors, 0 compilation errors)

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend WorkflowModels.cs with PR-review data contracts** - `594402f` (feat)
2. **Task 2: Write 7 RED test stubs in ReviewingStateTests.cs** - `aaf1cb5` (test)

## Files Created/Modified

- `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` — added ReviewComment, ReviewResult, PrReviewContext records; added Review and PrReview properties to GsdWorkflowContext
- `src/GsdOrchestrator.Tests/ReviewingStateTests.cs` — 7 RED [Fact] stubs with BuildDispatcher/BuildPrContext/BuildLlmApprove/BuildLlmRequestChanges/BuildLlmBadJson/BuildMcpClient/BuildSut helpers

## Decisions Made

- PrReview (PrReviewContext?) added as input carrier alongside Review (ReviewResult?) as result vessel — separates --pr mode input from output per CONTEXT.md D-03
- WorkflowState enum unchanged — WorkflowState.Reviewing already exists; no new enum value needed in this plan
- Test stubs compile against CURRENT ReviewingState 4-arg constructor — full replacement happens in Plan 15-02

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- First `dotnet build` with `--no-incremental` flag and second build attempt each showed a stale cache file error (`MSB3492: Could not read existing file .msCoverageSourceRootsMapping`) that is a known .NET SDK build artifact issue. Resolved by running build twice or without `--no-incremental`; actual compilation succeeded with 0 errors.

## User Setup Required

None - no external service configuration required.

## Known Stubs

The 7 test stubs are intentional RED stubs. They are not production code stubs — they are the TDD contract that Plan 15-02 must satisfy. Each stub throws implicitly (via NullReferenceException in current ReviewingState) rather than via NotImplementedException, which is expected and satisfies the RED requirement.

## Threat Flags

None — only additive data record definitions and test code added. No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries.

## Next Phase Readiness

- ReviewingStateTests.cs RED contract is locked: 7 tests define exactly what Plan 15-02 must implement
- WorkflowModels.cs data contracts are stable and ready for use in PrReviewingState (Plan 15-02 will replace ReviewingState or add --pr dispatch)
- No blockers — all 21 existing tests GREEN, build clean

---
*Phase: 15-pr-review-loop*
*Completed: 2026-06-05*
