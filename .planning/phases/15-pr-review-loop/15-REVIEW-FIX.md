---
phase: 15-pr-review-loop
fixed_at: 2026-06-05T12:30:00Z
review_path: .planning/phases/15-pr-review-loop/15-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 15: Code Review Fix Report

**Fixed at:** 2026-06-05T12:30:00Z
**Source review:** .planning/phases/15-pr-review-loop/15-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7 (3 Critical + 4 Warning)
- Fixed: 7
- Skipped: 0

All 28 tests pass after fixes. Build produces 0 warnings, 0 errors.

## Fixed Issues

### CR-01: Null-dereference in ExecuteIssueModeAsync

**Files modified:** `src/GsdOrchestrator/Workflows/States/ReviewingState.cs`
**Commit:** 21b3086
**Applied fix:** Added an explicit null guard at the top of `ExecuteIssueModeAsync` that throws `InvalidOperationException` with a descriptive message if any of `ctx.Issue`, `ctx.PullRequest`, `ctx.Plan`, or `ctx.Edits` are null. Replaced the four `!`-suppressed assignments with plain assignments (null-forgiving operators removed).

---

### CR-02: Dead 4-parameter GsdStateMachine.RunAsync overload removed

**Files modified:** `src/GsdOrchestrator/Workflows/GsdStateMachine.cs`, `src/GsdOrchestrator.Tests/GsdStateMachineTests.cs`
**Commits:** bd5fec2 (overload removal), ce1358c (test caller updates)
**Applied fix:** Removed the 4-parameter `RunAsync(string, string, int, CancellationToken)` overload entirely. Updated all 5 call sites in `GsdStateMachineTests.cs` to use the 5-parameter signature with explicit `triageModeOnly: false`. Note: the REVIEW.md stated the overload was never called anywhere, but it was in fact used by 5 existing unit tests — those were updated as part of this fix.

---

### CR-03: McpToolResult.IsError never checked

**Files modified:** `src/GsdOrchestrator/Workflows/States/ReviewingState.cs`, `src/GsdOrchestrator/Program.cs`
**Commit:** 8c22278
**Applied fix:**
- `FetchPrMetaAsync`: after `CallAsync("get_pull_request", ...)`, check `result.IsError` and return fallback `($"PR #{prCtx.PrNumber}", "")` with a `LogWarning`.
- `SubmitGitHubReviewAsync`: capture return value of `CallAsync("create_pull_request_review", ...)` in `submitResult`; if `submitResult.IsError`, throw `McpException` with `isTransient: false` so the review-missing state is surfaced immediately.
- `Program.cs RunPrReviewAsync`: after `CallAsync("get_pull_request", ...)`, check `diffResult.IsError` and throw `InvalidOperationException` before using the result.

---

### WR-01: OperationCanceledException retried in catch(Exception)

**Files modified:** `src/GsdOrchestrator/Workflows/States/ReviewingState.cs`
**Commit:** c96cca6
**Applied fix:** Added a `catch (OperationCanceledException) { throw; }` clause before the broad `catch (Exception ex)` inside the LLM retry loop in `InvokeLlmReviewAsync`. Cancellation now propagates immediately instead of being swallowed and retried up to `MaxLlmAttempts` times.

---

### WR-02: Prompt injection via unsanitised diff

**Files modified:** `src/GsdOrchestrator/Workflows/States/ReviewingState.cs`
**Commit:** 1c7de55
**Applied fix:** Added a `MaxDiffLength = 40_000` constant (approx. 10k tokens) and truncation logic before diff interpolation in `InvokeLlmReviewAsync`. If `prCtx.Diff.Length > MaxDiffLength`, the diff is sliced to `MaxDiffLength` characters with a `[diff truncated — too large]` sentinel appended. The truncated `safeDiff` is used in the prompt instead of the raw `prCtx.Diff`.

---

### WR-03: Final checkpoint uses cancellable token

**Files modified:** `src/GsdOrchestrator/Workflows/GsdStateMachine.cs`
**Commit:** 0211ef6
**Applied fix:** Changed the three post-loop calls in `ExecuteLoopAsync` from using `ct` to `CancellationToken.None`:
- `await _checkpoints.SaveAsync(ctx, CancellationToken.None)`
- `await PostFailureCommentAsync(ctx, CancellationToken.None)`
- `await _checkpoints.ArchiveAsync(ctx.WorkflowId, CancellationToken.None)`

This matches the existing pattern used inside the `catch (OperationCanceledException)` block and ensures a racing `Ctrl+C` cannot cause the final state to be silently lost.

---

### WR-04: Test 2 labelled REV-01 instead of REV-02

**Files modified:** `src/GsdOrchestrator.Tests/ReviewingStateTests.cs`
**Commit:** 5340e72
**Applied fix:** Changed the comment on test 2 from `// ── Test 2: REV-01 — REQUEST_CHANGES verdict transitions to Done` to `// ── Test 2: REV-02 — REQUEST_CHANGES verdict transitions to Done` to correctly distinguish it from test 1 (REV-01 = APPROVE) and align with requirement traceability.

---

_Fixed: 2026-06-05T12:30:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
