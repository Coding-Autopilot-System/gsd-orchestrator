---
phase: 15-pr-review-loop
phase_number: 15
generated: "2026-06-05"
mode: discuss
---

# Phase 15: PR Review Loop - Context

**Gathered:** 2026-06-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a `--pr <N>` operating mode that reads an open PR's diff via the GitHub MCP, invokes Claude to produce structured inline review findings (`[{file, line, severity, message}]`), posts each finding as a true inline PR comment anchored to its file+line, and submits a formal `REQUEST_CHANGES` (if blocking findings exist) or `COMMENT` (if no blocking findings) review action. The existing issue-to-PR pipeline (including the existing `ReviewingState` self-review step) is untouched.

**Requirements:** REV-01, REV-02, REV-03

</domain>

<decisions>
## Implementation Decisions

### State Machine Entry (REV-01)

- **D-01:** `GsdStateMachine` gets a new method `RunPrReviewAsync(string owner, string repo, int prNumber, CancellationToken ct)`. This is the third entry point alongside `RunAsync` (issue flow) and `ResumeAsync` (checkpoint resume). Program.cs parses `--pr <N>` and calls this method directly when detected.
- **D-02:** `Program.cs` args parsing: add `int? prReviewNumber = null;` and `if (args[i] == "--pr" && i + 1 < args.Length && int.TryParse(args[i + 1], out var p)) prReviewNumber = p;`. Add a `--pr` usage line in the existing error block. Route to `RunPrReviewAsync` when `prReviewNumber is not null`.
- **D-03:** `RunPrReviewAsync` boots a `GsdWorkflowContext` with `CurrentState = WorkflowState.PrReviewing`. The PR number is passed into context via a new `int PrReviewNumber` property on `GsdWorkflowContext` (not via `PullRequestContext` — avoids reusing a result record as an input vessel).
- **D-04:** No checkpointing for `--pr <N>` runs. The workflow is short-lived (read diff → review → post → done). Re-running `--pr <N>` is sufficient recovery if interrupted.

### New State: PrReviewingState (REV-02)

- **D-05:** New `WorkflowState.PrReviewing` enum value added to `WorkflowModels.cs`. Place it after `Done` and before `Failed` — it is a terminal-adjacent state for the PR-review operating mode, not part of the issue pipeline.
- **D-06:** New `PrReviewingState.cs` in `src/GsdOrchestrator/Workflows/States/`. Existing `ReviewingState.cs` is **unchanged** — it continues to post the self-review bot comment after automation creates a PR. Phase 15 adds a parallel operating mode; it does not modify the issue-to-PR pipeline.
- **D-07:** `PrReviewingState` handles the full PR review in a single state:
  1. Read PR diff via `pull_request_read` MCP tool
  2. Call Claude with the diff to produce structured findings JSON
  3. Post each finding as an inline comment via `pull_request_review_write`
  4. Submit the review action (REQUEST_CHANGES or COMMENT)
  5. Transition to `WorkflowState.Done`
- **D-08:** New output record: `PrReviewResult(int PrNumber, IReadOnlyList<PrFinding> Findings, string ReviewAction)` and `PrFinding(string File, int Line, string Severity, string Message)`. Add to `WorkflowModels.cs`. Store on `GsdWorkflowContext` as `PrReviewContext? PrReview { get; init; }`.

### Inline Comment Format (REV-03)

- **D-09:** Diff is read via the `pull_request_read` MCP tool (reads PR details including diff). The full diff text is included in the Claude prompt.
- **D-10:** Claude produces structured JSON: `[{"file": "...", "line": N, "severity": "blocking|warning|info", "message": "..."}]`. The prompt must request **only** valid JSON with no markdown fences, same pattern as `PrCreatingState.GeneratePrDraftAsync`.
- **D-11:** True inline comments: each finding is posted as an inline comment anchored to `file` + `line` via `pull_request_review_write`. If `pull_request_review_write` supports batching (submitting all comments in one review event), prefer that over N individual calls. Planner should check the MCP tool schema.
- **D-12:** Claude LLM retry pattern: 3 attempts, same as `TriagingState` and `TestGeneratingState`. Temperature 0.1f for structured JSON output.

### Approve/Request-Changes Logic

- **D-13:** Severity scale: `blocking` / `warning` / `info`. Consistent with `ValidationStatus` vocabulary (Block/Warn/Pass) used in `ValidatingState`. Claude's prompt must be instructed to use exactly these three values.
- **D-14:** Review action: submit `REQUEST_CHANGES` if any `blocking` findings exist; submit `COMMENT` for all other cases (zero findings, warnings only, or info only). Never submit autonomous `APPROVE` — human must explicitly approve.
- **D-15:** When `blocking` findings exist, the review body (if `pull_request_review_write` supports a top-level body) summarizes: "Found N blocking issue(s) — please address before merging." When no blocking findings: "Found N warning(s) / M info item(s) — no blockers detected, human review recommended."

### Claude's Discretion

- Exact MCP tool parameter names for `pull_request_read` (verify via MCP schema at runtime)
- Whether `pull_request_review_write` supports batched inline comments (one review event with array of comments) or requires individual calls — use batched if available
- LLM prompt phrasing for the code review (follow the pattern in `TestGeneratingState.GenerateTestFileAsync`)
- `PrintResult` in `Program.cs` — add a `WorkflowState.PrReviewing` branch to print the review summary

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Requirements
- `.planning/REQUIREMENTS.md` §"PR Review Loop (REV)" — REV-01, REV-02, REV-03 definitions
- `.planning/ROADMAP.md` §"Phase 15 — PR Review Loop" — goal, success criteria

### Existing Codebase (must read before planning)
- `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` — add `WorkflowState.PrReviewing`, `PrFinding` record, `PrReviewResult` record, `GsdWorkflowContext.PrReview` property, `GsdWorkflowContext.PrReviewNumber` property
- `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` — add `RunPrReviewAsync` method here; follow `RunAsync` pattern for state dispatch loop
- `src/GsdOrchestrator/Program.cs` — add `--pr <N>` arg parsing; call `RunPrReviewAsync`; add `WorkflowState.PrReviewing` branch to `PrintResult`
- `src/GsdOrchestrator/Workflows/States/ReviewingState.cs` — read to understand self-review pattern; DO NOT MODIFY (issue pipeline stays intact)
- `src/GsdOrchestrator/Workflows/States/TriagingState.cs` — LLM retry loop pattern (3 attempts, Temperature 0.1f, same JSON parse error handling)
- `src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs` — LLM structured JSON output pattern and `ParseInnerJson()` usage
- `src/GsdOrchestrator/Workflows/States/PrCreatingState.cs` — `GeneratePrDraftAsync` pattern for Claude JSON prompt + parse with fallback

### Prior Phase Context
- `.planning/phases/14-autonomous-test-generation/14-02-SUMMARY.md` — TestGeneratingState implementation patterns reused in this phase
- `.planning/phases/12-robustness-foundation/12-CONTEXT.md` §"Unit Tests (ROB-02)" — NSubstitute mock patterns for new state unit tests

### No external ADRs
No external specs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `McpToolDispatcher.CallAsync(toolName, JsonObject, ct)` — call `pull_request_read` and `pull_request_review_write` with this exact pattern
- `ParseInnerJson()` extension — already used in all states to parse MCP tool responses; use it on `pull_request_read` result to extract diff text
- `GsdWorkflowContext.Transition(WorkflowState)` — `PrReviewingState.ExecuteAsync` returns `ctx.Transition(WorkflowState.Done)` on success
- NSubstitute + `NullLogger<T>.Instance` — test setup pattern established in `TestGeneratingStateTests.cs` and `TriagingStateTests.cs`

### Established Patterns
- **LLM structured JSON output:** `GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Temperature = 0.1f }, ct)` → `response.Text` → `JsonNode.Parse(text)` with try/catch fallback. Used in `PrCreatingState`, `TriagingState`, `TestGeneratingState`.
- **3-attempt retry loop with prompt augmentation on failure:** Exactly as in `TriagingState` and `TestGeneratingState` — attempt counter, augment prompt on attempt 2+, log attempt number.
- **DI registration:** `builder.Services.AddSingleton<IWorkflowState, PrReviewingState>()` in Program.cs — but only used when `--pr` mode is active (state machine doesn't dispatch to it in the issue pipeline).
- **State constructor injection:** `McpToolDispatcher _mcp`, `IChatClient _llm`, `ILogger<T> _logger` — all states follow this pattern.

### Integration Points
- `Program.cs` routes to `sm.RunPrReviewAsync(owner, repo, prReviewNumber.Value, cts.Token)` — analogous to how `sm.RunAsync(..., triageModeOnly, ...)` was added for `--triage`
- `GsdStateMachine.RunPrReviewAsync` creates initial context, runs the state dispatch loop limited to `PrReviewingState` → `Done`, no checkpoint save/load
- `WorkflowModels.cs` gets new types: `PrFinding`, `PrReviewResult` records and `PrReviewNumber`/`PrReview` properties on `GsdWorkflowContext`
- `PrReviewingState` registered in DI but GsdStateMachine dispatch for `RunPrReviewAsync` only ever routes to `WorkflowState.PrReviewing` and `Done` — not into the issue pipeline states

</code_context>

<specifics>
## Specific Ideas

- The review body (overall review comment, not per-line) should use the same "🤖 **GSD Orchestrator**" prefix established in `ReviewingState.GenerateReviewCommentAsync` for brand consistency
- Severity `blocking` → ❌ prefix on inline comment, `warning` → ⚠️, `info` → ℹ️ — optional but makes comments scannable
- `PrintResult` for PR review mode should print the PR URL and finding counts, similar to how triage prints the classification

</specifics>

<deferred>
## Deferred Ideas

- Auto-approve when Claude finds zero findings — deferred by design (D-14). Never autonomous APPROVE.
- `--watch` mode for PR review (poll for open PRs needing review) — deferred to Phase 16 or future milestone
- Re-review after PR is updated (re-run `--pr <N>` clears old bot comments before posting new ones) — deferred; Phase 15 just appends new comments on each run

</deferred>

---

*Phase: 15-pr-review-loop*
*Context gathered: 2026-06-05*
