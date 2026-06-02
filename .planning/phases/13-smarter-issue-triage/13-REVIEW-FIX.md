---
phase: 13-smarter-issue-triage
fixed_at: 2026-06-01T00:00:00Z
review_path: .planning/phases/13-smarter-issue-triage/13-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 13: Code Review Fix Report

**Fixed at:** 2026-06-01T00:00:00Z
**Source review:** .planning/phases/13-smarter-issue-triage/13-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7 (3 Critical + 4 Warning)
- Fixed: 7
- Skipped: 0

## Fixed Issues

### CR-01: `GetValue<int?>()` on `JsonNode` throws `InvalidOperationException` at runtime

**Files modified:** `src/GsdOrchestrator/Workflows/States/TriagingState.cs`
**Commit:** c0e365f
**Applied fix:** Replaced `node["duplicateNumber"]?.GetValue<int?>()` with a `JsonValue` pattern match: `node["duplicateNumber"] is JsonValue dupVal ? dupVal.GetValue<int>() : (int?)null`. This avoids the `InvalidOperationException` that `GetValue<T>()` throws when `T` is a nullable value type on a non-null JSON number node.

---

### CR-02: Checkpoint save after cancellation uses the already-cancelled token — checkpoint is never written

**Files modified:** `src/GsdOrchestrator/Workflows/GsdStateMachine.cs`
**Commit:** f10cb33
**Applied fix:** Changed `await _checkpoints.SaveAsync(ctx, ct)` to `await _checkpoints.SaveAsync(ctx, CancellationToken.None)` inside the `catch (OperationCanceledException)` block. The user-facing `ct` is already cancelled at that point; using `CancellationToken.None` ensures the checkpoint write completes so resume is accurate.

---

### CR-03: `needs-info` issues are not closed despite the comment claiming they are

**Files modified:** `src/GsdOrchestrator/Workflows/States/TriagingState.cs`
**Commit:** a0fea69
**Applied fix:** Added `or "needs-info"` to the classification guard at line 63, so `TryCloseIssueAsync` is called for `"duplicate"`, `"out-of-scope"`, and `"needs-info"` classifications — consistent with the inline comment and TRIAGE-04 requirements.

---

### WR-01: Unknown LLM classification silently defaults to `actionable`

**Files modified:** `src/GsdOrchestrator/Workflows/States/TriagingState.cs`
**Commit:** feea3db
**Applied fix:** Replaced the fallback `return new TriageResult("actionable", ...)` block for unrecognised classifications with `return null`, so the retry loop fires instead of silently promoting a bad LLM response to the most expensive code path.

---

### WR-02: Issue body and title passed unsanitised into the LLM prompt — prompt injection risk

**Files modified:** `src/GsdOrchestrator/Workflows/States/TriagingState.cs`
**Commit:** f2ed0e8
**Applied fix:** Converted `BuildTriagePrompt` from an expression-bodied method to a block-bodied method. Added `sanitisedTitle` (truncated at 200 chars) and `sanitisedBody` (truncated at 2000 chars) local variables before embedding values in the raw string literal. Surrounding code and build verify no regression.

---

### WR-03: `processedIssues` in watch mode is never pruned — issues closed externally are never reprocessed

**Files modified:** `src/GsdOrchestrator/Program.cs`
**Commit:** 78b675a
**Applied fix:** Added `processedIssuesCapacity = 500` and `processedIssuesEvictCount = 100` constants. When the set reaches capacity, the oldest 100 entries are removed before adding the new number. Also changed the `sm.RunAsync(owner, repo, num, ct)` call to `sm.RunAsync(owner, repo, num, triageModeOnly: false, ct)` to make the intended watch-mode behaviour explicit.

---

### WR-04: `PrintResult` gives incorrect output for triage-only workflows that end in `Done`

**Files modified:** `src/GsdOrchestrator/Program.cs`
**Commit:** d5b40af
**Applied fix:** Added a branch inside the `WorkflowState.Done` path: when `result.Triage is not null && result.PullRequest is null` (triage-only run), print `"Triage complete: [{classification}] {reason}"`. Full-workflow runs continue to print the PR URL and docs-updated lines.

---

## Skipped Issues

None — all findings were fixed.

---

_Fixed: 2026-06-01T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
