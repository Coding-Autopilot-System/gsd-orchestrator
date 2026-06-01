# Phase 13: Smarter Issue Triage — Research

**Researched:** 2026-06-01
**Domain:** C#/.NET 10 state machine extension — issue classification, GitHub MCP issue management
**Confidence:** HIGH

---

## Summary

Phase 13 inserts a `TriagingState` between `IdleState` and `AnalyzingState`. The new state calls the Anthropic SDK LLM (already injected as `IChatClient`) with a classification prompt and branches the workflow based on the result: actionable issues proceed to `AnalyzingState`, while duplicate or out-of-scope issues are closed/labelled via MCP and the workflow exits cleanly via `WorkflowState.Done`.

The entire state machine infrastructure is already in place. Adding `TriagingState` requires: (1) a new enum value `Triaging` in `WorkflowState`, (2) a new `TriagingState` class following the identical `IWorkflowState` pattern, (3) modifying `IdleState` to transition to `Triaging` instead of `Analyzing`, (4) a `--triage` CLI flag in `Program.cs` that uses a thin wrapper or flag to exit after triage, and (5) xUnit tests following the NSubstitute pattern from Phase 12.

**Primary recommendation:** Implement `TriagingState` as a self-contained class (no new interfaces) that uses the existing `IChatClient`, `McpToolDispatcher`, and `IConfiguration` injection. Add `Triaging` to the `WorkflowState` enum and register the new state as `IWorkflowState` singleton in `Program.cs`.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Issue classification (LLM) | TriagingState | — | All LLM calls live in state classes; AnalyzingState is the precedent |
| Duplicate detection | TriagingState | — | Requires MCP `list_issues` + `list_pull_requests` calls, same as watch mode pattern |
| Post triage comment | TriagingState | — | `add_issue_comment` pattern established in ReviewingState and GsdStateMachine |
| Close/label issue | TriagingState | — | New MCP tools: `update_issue`; belongs inside the state that triggers skip logic |
| --triage CLI mode | Program.cs | GsdStateMachine | Args parsing lives in Program.cs; state machine drives execution |
| State registration | Program.cs | — | All `IWorkflowState` singletons registered in Program.cs |
| Workflow exit after triage | WorkflowState enum + TriagingState | — | State transitions to Done or SkippedDuplicate/SkippedOutOfScope |

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TRIAGE-01 | `TriagingState` implemented — classifies issue via Claude (actionable / needs-info / duplicate / out-of-scope) | LLM classification via existing `IChatClient` injection; JSON response parsing pattern from `AnalyzingState` |
| TRIAGE-02 | Duplicate detection — checks open issues and PRs for similar titles before proceeding | `list_issues` MCP tool already used in watch mode; `list_pull_requests` used in PrCreatingState |
| TRIAGE-03 | `--triage` operating mode — runs triage only, posts classification comment, no code changes | `--watch` / `--issue` / `--resume` arg parsing pattern in Program.cs; triage mode exits after `TriagingState` |
| TRIAGE-04 | Skip logic — issues classified as out-of-scope or duplicate are closed/labelled with comment, workflow exits cleanly | `update_issue` MCP tool (close + label); `add_issue_comment` already used; transition to `Done` |
</phase_requirements>

---

## Standard Stack

### Core (already in project — no new packages needed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Anthropic.SDK | 5.10.0 | LLM classification calls | Already injected as `IChatClient` in all analyzing states |
| Microsoft.Extensions.AI | 10.6.0 | `IChatClient`, `ChatMessage`, `ChatOptions` | Project standard — used in `AnalyzingState`, `ReviewingState`, `EditingState` |
| Polly.Extensions | 8.6.6 | Resilience pipeline around MCP calls | Already wraps all `McpToolDispatcher` calls |

[VERIFIED: csproj read — `src/GsdOrchestrator/GsdOrchestrator.csproj`]

### Test Project (already in project)

| Library | Version | Purpose |
|---------|---------|---------|
| xunit | 2.9.3 | Test runner |
| NSubstitute | 5.3.0 | Mocking `IWorkflowState`, `IMcpClient`, `ICheckpointStore` |
| coverlet.collector | 10.0.1 | Coverage collection |

[VERIFIED: 12-03-SUMMARY.md — `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj`]

**No new packages required.** All needed libraries are already present.

---

## Architecture Patterns

### System Architecture Diagram

```
CLI args (--triage / --issue / --watch)
        |
        v
Program.cs args parser
        |
        +--[--triage]----> sm.RunAsync()  ──> IdleState
        |                                       |
        +--[--issue]-----> sm.RunAsync()        v
        |                                 TriagingState
        +--[--watch]-----> RunWatchModeAsync     |
                                          classify via IChatClient
                                                 |
                              +--[actionable]----+--[needs-info]
                              |                  |
                              v                  v
                        AnalyzingState      add_comment
                              |             transition Done
                       (existing chain)
                              |
                    +--[duplicate]--+--[out-of-scope]
                    |               |
               add_comment     add_comment
               close issue     close + label issue
               transition Done transition Done
```

### Recommended Project Structure

No new directories needed. New file follows existing pattern:

```
src/GsdOrchestrator/
├── Workflows/
│   ├── Models/
│   │   └── WorkflowModels.cs       ← ADD: Triaging to WorkflowState enum
│   │                                  ADD: TriageResult record
│   └── States/
│       ├── IWorkflowState.cs       ← no change
│       ├── IdleState.cs            ← MODIFY: transition to Triaging not Analyzing
│       └── TriagingState.cs        ← CREATE: new state
├── Program.cs                      ← MODIFY: --triage flag + TriagingState registration
└── GsdOrchestrator.csproj          ← no change

src/GsdOrchestrator.Tests/
└── TriagingStateTests.cs           ← CREATE: unit tests
```

---

## Pattern 1: IWorkflowState Implementation

Every state follows this exact pattern — constructor injects dependencies, `State` property returns the enum value, `ExecuteAsync` returns a new `GsdWorkflowContext` via the `Transition()` record method.

```csharp
// Source: verified — src/GsdOrchestrator/Workflows/States/AnalyzingState.cs
public sealed class TriagingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<TriagingState> _logger;

    public WorkflowState State => WorkflowState.Triaging;

    public TriagingState(McpToolDispatcher mcp, IChatClient llm, ILogger<TriagingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        // ... classify, post comment, branch on result
        return ctx.Transition(WorkflowState.Analyzing);  // or Done
    }
}
```

[VERIFIED: src/GsdOrchestrator/Workflows/States/AnalyzingState.cs, ReviewingState.cs, BranchingState.cs]

---

## Pattern 2: WorkflowState Enum Extension

The `WorkflowState` enum is in `WorkflowModels.cs`. `Triaging` must be inserted between `Idle` and `Analyzing` (ordering is cosmetic — the dictionary lookup in `GsdStateMachine` is by value, not by ordinal).

```csharp
// Source: verified — src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs
public enum WorkflowState
{
    Idle,
    Triaging,      // ← INSERT HERE
    Analyzing,
    Branching,
    // ... rest unchanged
    Done,
    Failed
}
```

[VERIFIED: WorkflowModels.cs]

---

## Pattern 3: LLM Classification — JSON Response Pattern

`AnalyzingState` demonstrates the retry-on-parse-failure pattern. `TriagingState` should use the same approach with a simpler JSON envelope: just a `classification` string and a `reason` string.

```csharp
// Source: adapted from src/GsdOrchestrator/Workflows/States/AnalyzingState.cs
private static string BuildTriagePrompt(IssueContext issue) =>
    $$"""
    You are a software issue triage bot. Classify the following GitHub issue.

    Issue #{{issue.Number}}: {{issue.Title}}
    Body:
    {{issue.Body}}
    Labels: {{string.Join(", ", issue.Labels)}}

    Return ONLY a JSON object (no markdown, no explanation):
    {
      "classification": "actionable" | "needs-info" | "duplicate" | "out-of-scope",
      "reason": "one sentence explanation",
      "duplicateNumber": null | <issue number if duplicate>
    }

    Definitions:
    - actionable: clear, specific, reproducible — ready for implementation
    - needs-info: too vague, missing steps to reproduce, or requires clarification
    - duplicate: same problem as another open issue (duplicateNumber required)
    - out-of-scope: feature request outside project goals, or spam
    """;
```

[ASSUMED — prompt content; classification taxonomy matches TRIAGE-01 requirement wording]

Parse failure retry: use the same `for (int attempt = 1; attempt <= 3; attempt++)` pattern from `AnalyzingState`.

---

## Pattern 4: State Registration in Program.cs

All states are registered as singletons in the DI container. `TriagingState` follows the same line:

```csharp
// Source: verified — src/GsdOrchestrator/Program.cs lines 89-98
builder.Services.AddSingleton<IWorkflowState, TriagingState>();
// Insert AFTER IdleState registration, BEFORE AnalyzingState registration (cosmetic ordering)
```

The `GsdStateMachine` constructor receives `IEnumerable<IWorkflowState>` and builds a `Dictionary<WorkflowState, IWorkflowState>` — order of registration does not affect correctness.

[VERIFIED: GsdStateMachine.cs line 25, Program.cs lines 89-98]

---

## Pattern 5: CLI Mode Parsing

The existing `--issue`, `--resume`, `--watch` flags are parsed with a simple `for` loop before `Host.CreateApplicationBuilder`. `--triage` follows the same pattern and requires `--issue <N>` to also be provided:

```csharp
// Source: verified — src/GsdOrchestrator/Program.cs lines 17-35
bool triageMode = false;
// ...
if (args[i] == "--triage") triageMode = true;
// Validation:
if (triageMode && issueNumber is null)
    // error: --triage requires --issue <N>
```

The `--triage` flag does NOT need a separate `sm.RunAsync` call path. Instead, `TriagingState` itself checks a flag on `GsdWorkflowContext` (or uses the classification result) to decide whether to transition to `Done` directly. The simplest approach: pass `triageMode` into the DI container via `IConfiguration` or a dedicated options record, so `TriagingState` knows to always exit to `Done` regardless of classification outcome.

Alternative approach (no context change needed): `TriagingState` always classifies; in `--triage` mode it posts a comment and exits to `Done`; in normal mode only `duplicate`/`out-of-scope` exit to `Done`.

**Recommended:** Add `bool TriageModeOnly` to `GsdWorkflowContext` (default `false`). Set it in `Program.cs` before calling `sm.RunAsync`. This keeps the flag visible in checkpoints and avoids global state.

[ASSUMED — triageMode propagation approach; multiple valid implementations exist]

---

## Pattern 6: GitHub MCP Tools for Issue Management

### Tools already verified in codebase

| Tool Name | Used In | Args |
|-----------|---------|------|
| `add_issue_comment` | GsdStateMachine.cs, ReviewingState.cs | `owner`, `repo`, `issue_number`, `body` |
| `list_issues` | Program.cs (watch mode) | `owner`, `repo`, `state`, `perPage` |
| `list_pull_requests` | PrCreatingState.cs | `owner`, `repo`, `state`, `head` |
| `get_issue` | IdleState.cs | `owner`, `repo`, `issue_number` |

[VERIFIED: grep of CallAsync across all state files]

### Tools needed for Phase 13 (not yet used)

| Tool Name | Purpose | Expected Args |
|-----------|---------|---------------|
| `update_issue` | Close issue and/or add labels | `owner`, `repo`, `issue_number`, `state` ("closed"), `labels` |
| `add_issue_comment` | Post triage classification comment | already used — no change |

[ASSUMED — `update_issue` tool name; GitHub MCP server exposes standard GitHub API tools. The GitHub MCP server binary is present at `C:/GithubMCP/github-mcp-server.exe`. Tool names follow GitHub REST API naming conventions.]

**Risk:** The GitHub MCP server may use `close_issue` or a different tool name instead of `update_issue`. The plan should include a `list_tools` probe step (or use `update_issue` defensively with a try/catch fallback).

---

## Pattern 7: Duplicate Detection

The watch mode in `Program.cs` already demonstrates `list_issues` pagination. For duplicate detection, `TriagingState` should:

1. Call `list_issues` with `state=open` and `perPage=50` to get titles of recently open issues
2. Pass the list of titles + current issue title to the LLM as part of the classification prompt
3. The LLM returns `classification=duplicate` + `duplicateNumber` if it finds a match

This is simpler and more robust than string-similarity algorithms. The LLM handles fuzzy matching naturally.

```csharp
// Source: verified pattern from src/GsdOrchestrator/Program.cs lines 154-168
var issuesResult = await _mcp.CallAsync("list_issues", new JsonObject
{
    ["owner"] = issue.RepoOwner,
    ["repo"] = issue.RepoName,
    ["state"] = "open",
    ["perPage"] = 50
}, ct);
var openIssues = issuesResult.ParseInnerJson()?.AsArray() ?? [];
// Extract number + title for prompt injection
```

[VERIFIED: Program.cs lines 154-168 for `list_issues` call pattern]

---

## Pattern 8: Clean Workflow Exit via Done State

The `GsdStateMachine` loop exits cleanly when `CurrentState` is `Done` (or `Failed`). So transitioning to `WorkflowState.Done` from `TriagingState` for skipped issues is the correct exit path. The machine then calls `_checkpoints.ArchiveAsync` (not `PostFailureCommentAsync`) — which is the correct behavior for skipped issues (not a failure).

```csharp
// Source: verified — GsdStateMachine.cs lines 60, 92-99
while (ctx.CurrentState is not WorkflowState.Done and not WorkflowState.Failed)
// ...
if (ctx.CurrentState == WorkflowState.Failed)
    await PostFailureCommentAsync(ctx, ct);
else
    await _checkpoints.ArchiveAsync(ctx.WorkflowId, ct);
```

Skipped issues MUST transition to `Done` (not `Failed`) so that:
- No failure comment is posted
- The checkpoint is archived cleanly
- Watch mode marks the issue as processed (using its `processedIssues` HashSet)

[VERIFIED: GsdStateMachine.cs lines 60, 92-99]

---

## Anti-Patterns to Avoid

- **Do not add a new terminal state** (`SkippedDuplicate`, `SkippedOutOfScope`): The state machine loop only exits on `Done` or `Failed`. Adding a third terminal state requires modifying `GsdStateMachine.ExecuteLoopAsync`. Use `Done` for all clean exits.
- **Do not modify `GsdStateMachine.ExecuteLoopAsync`**: The loop is generic. All classification logic belongs in `TriagingState`. Adding triage-specific logic to the machine itself breaks separation of concerns.
- **Do not add `--triage` as a fully separate code path** that bypasses `sm.RunAsync`: It creates duplication and loses checkpointing, Serilog logging, and the Polly circuit breaker for free.
- **Do not use string similarity for duplicate detection**: The LLM is already available and produces better fuzzy matching. Levenshtein distance at the orchestrator level is unnecessary complexity.
- **Do not hard-code classification labels as enum values on context**: Store as `string` on a new `TriageResult` record to remain flexible if classifications are extended in future phases.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Fuzzy duplicate detection | Custom string similarity | LLM classification prompt with issue list | LLM handles synonyms, rephrasing, and partial matches naturally |
| JSON response parsing | Custom parser | `JsonNode.Parse` + null-safe navigation (established pattern) | Already used in AnalyzingState — consistent |
| Retry on LLM parse failure | Custom retry loop | Existing `for (attempt = 1; attempt <= 3)` pattern from AnalyzingState | Consistent and tested |
| MCP resilience | New Polly pipeline | Existing `McpToolDispatcher` with `mcp-tools` pipeline | Already has circuit breaker + retry |

---

## How to Add TriagingState — Minimal Change Set

This is a precise change inventory for the planner:

### File 1: `WorkflowModels.cs`
- Add `Triaging` to `WorkflowState` enum (between `Idle` and `Analyzing`)
- Add `TriageResult` record: `(string Classification, string Reason, int? DuplicateNumber)`
- Add `TriageResult? Triage { get; init; }` property to `GsdWorkflowContext`

### File 2: `IdleState.cs`
- Change last line from `.Transition(WorkflowState.Analyzing)` to `.Transition(WorkflowState.Triaging)`

### File 3: `TriagingState.cs` (CREATE)
- Implements `IWorkflowState`
- `State => WorkflowState.Triaging`
- Injects: `McpToolDispatcher`, `IChatClient`, `ILogger<TriagingState>`
- Calls `list_issues` to get open issue titles for duplicate context
- Calls `_llm.GetResponseAsync` with classification prompt
- Parses `TriageResult` from JSON
- Branches:
  - `actionable`: transition to `Analyzing`
  - `needs-info`: post comment, transition to `Done` (or `Analyzing` — see Open Questions)
  - `duplicate`: post comment with `#duplicateNumber`, call `update_issue` to close, transition to `Done`
  - `out-of-scope`: post comment, call `update_issue` to close + add label `out-of-scope`, transition to `Done`
- In `--triage` mode (`ctx.TriageModeOnly == true`): always post comment and transition to `Done`

### File 4: `Program.cs`
- Add `bool triageModeOnly = false;` to args parsing block
- Add `if (args[i] == "--triage") triageModeOnly = true;` to the loop
- Add validation: `--triage` requires `--issue`
- Set `ctx.TriageModeOnly` before calling `sm.RunAsync` — pass via initial `GsdWorkflowContext`
- Add `builder.Services.AddSingleton<IWorkflowState, TriagingState>();`
- Update usage message

### File 5: `WorkflowModels.cs` (same file as #1)
- Add `bool TriageModeOnly { get; init; }` property to `GsdWorkflowContext` (default `false`)

---

## Common Pitfalls

### Pitfall 1: LLM Returns Unknown Classification String
**What goes wrong:** LLM returns `"unclear"`, `"spam"`, or other value not in the expected set.
**Why it happens:** Temperature > 0 + prompt ambiguity.
**How to avoid:** Parse with a fallback: treat anything not in `{actionable, needs-info, duplicate, out-of-scope}` as `actionable` (proceed conservatively). Log the unexpected value as a Warning.
**Warning signs:** Workflow skipping actionable issues or failing to classify.

### Pitfall 2: `update_issue` Tool Name Wrong
**What goes wrong:** MCP call throws `McpException` because the tool is named differently.
**Why it happens:** GitHub MCP server tool naming is not verified from the local binary.
**How to avoid:** Wrap `update_issue` call in try/catch, log a Warning on failure, but continue to `Done` (the comment was already posted). Closing can be done manually.
**Warning signs:** `McpException` on triage close step.

### Pitfall 3: Watch Mode Re-Processes Triaged (Closed) Issues
**What goes wrong:** Watch mode calls `list_issues` with `state=open`. After `TriagingState` closes an issue, it disappears from the list — this is actually correct behavior. But if the close MCP call fails, the issue stays open and watch mode will re-triage it.
**How to avoid:** Store the `processedIssues` HashSet in watch mode (already done). The issue number is added to `processedIssues` after `sm.RunAsync` returns, regardless of outcome.
**Warning signs:** Same issue triaged twice in one watch cycle.

### Pitfall 4: `TriageModeOnly` Flag Lost on Resume
**What goes wrong:** If a triage workflow is checkpointed mid-state and resumed, `TriageModeOnly` is `false` by default and the workflow proceeds to full analysis.
**Why it happens:** `--triage` flag only set in `Program.cs` at startup — resume path does not re-apply it.
**How to avoid:** `--triage` does not checkpoint (it's a fast one-state operation). If resume is attempted for a triage workflow, it runs the full analysis. This is acceptable behavior — document it as a known limitation.

### Pitfall 5: Raw String Literals in LLM Prompt (known from Phase 12)
**What goes wrong:** C# raw string literals (`$"""..."""`) with backtick content can be corrupted during base64 roundtrip in task execution.
**Why it happens:** Python-based task executor wraps file content in base64; the `"""` delimiter confuses the string assembly.
**How to avoid:** Use `$$"""..."""` (double-dollar for interpolation), or use `$"..." + $"..."` concatenation for the prompt body if the raw string literal contains backticks.
[VERIFIED: Phase 12 Plan 01 SUMMARY — D-03 PostFailureCommentAsync fix]

---

## Code Examples

### State Transition via Done (clean skip exit)

```csharp
// Source: verified — GsdStateMachine.cs lines 92-99
// Transitioning to Done causes ArchiveAsync, NOT PostFailureCommentAsync
return (ctx with { Triage = triageResult }).Transition(WorkflowState.Done);
```

### Posting a Comment via MCP

```csharp
// Source: verified — GsdStateMachine.cs lines 121-127 + ReviewingState.cs lines 43-49
await _mcp.CallAsync("add_issue_comment", new JsonObject
{
    ["owner"] = issue.RepoOwner,
    ["repo"] = issue.RepoName,
    ["issue_number"] = issue.Number,
    ["body"] = $"🤖 **GSD Triage** — Classification: `{triageResult.Classification}`\n\n{triageResult.Reason}"
}, ct);
```

### Closing an Issue via MCP (assumed tool name)

```csharp
// Source: [ASSUMED — update_issue tool name based on GitHub MCP server conventions]
await _mcp.CallAsync("update_issue", new JsonObject
{
    ["owner"] = issue.RepoOwner,
    ["repo"] = issue.RepoName,
    ["issue_number"] = issue.Number,
    ["state"] = "closed"
}, ct);
```

### LLM Response Parse Pattern (from AnalyzingState)

```csharp
// Source: verified — src/GsdOrchestrator/Workflows/States/AnalyzingState.cs lines 36-53
for (int attempt = 1; attempt <= 3; attempt++)
{
    var response = await _llm.GetResponseAsync(
        [new ChatMessage(ChatRole.User, prompt)],
        new ChatOptions { Temperature = 0.1f },
        ct);
    var text = response.Text ?? "";
    triageResult = TryParseTriageResult(text);
    if (triageResult is not null) break;
    prompt += $"\n\nAttempt {attempt} failed to parse. Return ONLY valid JSON.";
}
```

---

## Test Strategy

### Test Framework (from Phase 12)

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + NSubstitute 5.3.0 |
| Config file | GsdOrchestrator.Tests.csproj (net10.0) |
| Quick run command | `dotnet test src/GsdOrchestrator.Tests/ --no-build -x` |
| Full suite command | `dotnet test src/GsdOrchestrator.Tests/` |

### NSubstitute Pattern (from Phase 12-03)

```csharp
// Source: verified — 12-03-SUMMARY.md technical approach
// McpToolDispatcher is constructed with a no-op ResiliencePipelineRegistry
// IMcpClient, ICheckpointStore, IWorkflowState all mocked via NSubstitute
// NullLogger<T>.Instance used for all logger injection
```

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | Notes |
|--------|----------|-----------|-------------------|-------|
| TRIAGE-01 | `actionable` classification transitions to `Analyzing` | unit | `dotnet test --filter "FullyQualifiedName~TriagingState"` | Mock IChatClient returns `{"classification":"actionable",...}` |
| TRIAGE-01 | `needs-info` classification exits to `Done` | unit | same filter | Mock returns `{"classification":"needs-info",...}` |
| TRIAGE-01 | `out-of-scope` classification exits to `Done` | unit | same filter | Mock returns `{"classification":"out-of-scope",...}` |
| TRIAGE-01 | LLM parse failure retries 3 times then throws | unit | same filter | Mock returns unparseable string 3 times |
| TRIAGE-02 | `duplicate` classification with duplicateNumber exits to `Done` | unit | same filter | Mock returns `{"classification":"duplicate","duplicateNumber":42,...}` |
| TRIAGE-03 | `TriageModeOnly=true` always exits to `Done` even for actionable | unit | same filter | ctx.TriageModeOnly = true, mock returns actionable |
| TRIAGE-04 | Duplicate triggers close comment posted to MCP | unit | same filter | Verify `_mcp.CallAsync("add_issue_comment", ...)` called + `update_issue` called |

### Wave 0 Gaps

- [ ] `src/GsdOrchestrator.Tests/TriagingStateTests.cs` — covers all TRIAGE-01 through TRIAGE-04
- [ ] No new test infrastructure needed — framework, csproj, and solution file already wired from Phase 12-03

### Mocking IChatClient for Tests

`IChatClient` is an interface from `Microsoft.Extensions.AI`. It can be mocked via NSubstitute:

```csharp
// Source: [ASSUMED — NSubstitute pattern, IChatClient interface from MEL]
var llm = Substitute.For<IChatClient>();
llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
   .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant,
       """{"classification":"actionable","reason":"Clear bug report.","duplicateNumber":null}""")]));
```

Note: the exact `IChatClient.GetResponseAsync` return type is `ChatCompletion` — verify the constructor against MEL 10.x source if needed.

[VERIFIED: IChatClient used in AnalyzingState.cs, ReviewingState.cs — interface already in project]
[ASSUMED: exact NSubstitute mock setup for `ChatResponse` return value — verify MEL 10.x API]

---

## Runtime State Inventory

**Step 2.5 SKIPPED** — this is a greenfield feature addition (new state, new enum value, new CLI flag). No rename/refactor/migration involved. No stored data, OS-registered state, or secrets reference the string "TriagingState" or "triage".

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + test | [ASSUMED: yes — Phase 12 CI was green] | 10.x | — |
| xUnit test runner | TriagingStateTests | ✓ | from csproj | — |
| github-mcp-server.exe | Integration (not needed for unit tests) | ✓ | in repo root | — |
| Anthropic API key | Integration (not needed for unit tests) | [ASSUMED: configured in .env] | — | — |

[VERIFIED: github-mcp-server.exe present at C:/GithubMCP/github-mcp-server.exe]

---

## Validation Architecture

nyquist_validation is enabled (config.json `workflow.nyquist_validation: true`).

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + NSubstitute 5.3.0 |
| Config file | `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` |
| Quick run command | `dotnet test src/GsdOrchestrator.Tests/ --no-build -x` |
| Full suite command | `dotnet test src/GsdOrchestrator.Tests/` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TRIAGE-01 | TriagingState actionable → transitions to Analyzing | unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "FullyQualifiedName~Triaging"` | Wave 0 |
| TRIAGE-01 | TriagingState needs-info → transitions to Done | unit | same | Wave 0 |
| TRIAGE-01 | TriagingState out-of-scope → transitions to Done | unit | same | Wave 0 |
| TRIAGE-01 | LLM parse failure retries then throws | unit | same | Wave 0 |
| TRIAGE-02 | duplicate classification → posts comment + closes issue | unit | same | Wave 0 |
| TRIAGE-03 | TriageModeOnly=true → always Done regardless of classification | unit | same | Wave 0 |
| TRIAGE-04 | out-of-scope → update_issue called to close and label | unit | same | Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test src/GsdOrchestrator.Tests/ --no-build -x`
- **Per wave merge:** `dotnet test src/GsdOrchestrator.Tests/`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `src/GsdOrchestrator.Tests/TriagingStateTests.cs` — covers all TRIAGE-01 through TRIAGE-04
- [ ] `TriageResult` record must be defined in `WorkflowModels.cs` before tests can compile

*(Existing test infrastructure from Phase 12-03 covers framework setup — no new csproj or solution wiring needed)*

---

## Security Domain

Phase 13 introduces no new attack surface beyond what already exists. The triage classification prompt includes issue title and body (user-controlled content from GitHub). This content is already being passed to Claude in `AnalyzingState` — the security boundary is unchanged.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | GitHub PAT already in McpStdioClient |
| V3 Session Management | no | Stateless per-run |
| V4 Access Control | no | No new permission scopes |
| V5 Input Validation | yes | Issue title/body passed to LLM — same exposure as AnalyzingState |
| V6 Cryptography | no | No new crypto |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Prompt injection via issue body | Tampering | Treat LLM output as untrusted; parse JSON strictly; only use `classification` field for branching |
| Secrets in log output | Information Disclosure | Do not log issue body content at Info level; log only issue number and classification result |

[VERIFIED: existing pattern — Phase 12-01 SUMMARY threat surface scan: "T-12-01 (no secrets in log calls) verified"]

---

## Open Questions

1. **`update_issue` vs `close_issue` tool name**
   - What we know: GitHub MCP server is present locally; other tool names follow GitHub API naming
   - What's unclear: Whether the close/label operation is `update_issue` with `state=closed` or a separate `close_issue` tool
   - Recommendation: Plan task should probe `list_tools` at the start of the TriagingState implementation task, or wrap in try/catch with a fallback comment

2. **`needs-info` outcome: Done or Analyzing?**
   - What we know: TRIAGE-01 lists it as a classification type; TRIAGE-04 only mentions closing for `out-of-scope` and `duplicate`
   - What's unclear: Should `needs-info` post a comment and stop (same as `duplicate`) or continue to `Analyzing` and let the LLM work with partial info?
   - Recommendation: `needs-info` exits to `Done` with a comment asking the author for more info. This is safer — no code changes on vague issues. Planner should codify this.

3. **`update_issue` labels parameter format**
   - What we know: `add_issue_comment` uses `["body"]`; GitHub REST API uses `labels` as array
   - What's unclear: Whether the MCP tool wraps labels as `["labels"]` array or `["label"]` string
   - Recommendation: Wrap the label-add call in try/catch and log Warning on failure; labelling is non-critical

4. **`TriageModeOnly` propagation — context property vs IConfiguration**
   - What we know: `GsdWorkflowContext` is a record with `init`-only properties; checkpoint serialization is JSON-based
   - What's unclear: Whether adding a new property to `GsdWorkflowContext` causes deserialization issues on `ResumeAsync` for existing checkpoints
   - Recommendation: Use `bool TriageModeOnly { get; init; } = false;` with JSON default. Since `--triage` never checkpoints (single-state fast path), resume of triage workflows is an edge case that can be documented as unsupported.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `IdleState` → `AnalyzingState` (direct) | `IdleState` → `TriagingState` → `AnalyzingState` | Phase 13 | All workflows now classified before planning |
| No triage mode | `--triage` exits after classification | Phase 13 | Enables dry-run validation without code changes |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `update_issue` is the correct GitHub MCP tool name for closing issues | Pattern 6, Code Examples | Plan task fails at MCP call; workaround is try/catch + warning |
| A2 | `needs-info` classification exits to `Done` (not `Analyzing`) | Pattern 3, Open Questions | Actionable issues incorrectly halted; easy to change |
| A3 | `TriageModeOnly` stored as property on `GsdWorkflowContext` | Pattern 5 | If JSON deserialization breaks resumes, refactor to IConfiguration |
| A4 | LLM classification prompt taxonomy matches TRIAGE-01 intent | Pattern 3 | Prompt may need tuning after initial test run |
| A5 | `ChatResponse` constructor accepts `IEnumerable<ChatMessage>` for NSubstitute mock | Test Strategy | Mock setup code may need adjustment for MEL 10.x exact API |
| A6 | .NET 10 SDK available in local environment (Phase 12 CI was green on remote) | Environment Availability | Local build may fail if SDK not installed; CI is the source of truth |

---

## Sources

### Primary (HIGH confidence)

- `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` — state loop, Done/Failed terminal states, `add_issue_comment` pattern
- `src/GsdOrchestrator/Workflows/States/IWorkflowState.cs` — interface contract
- `src/GsdOrchestrator/Workflows/States/IdleState.cs` — constructor pattern, `McpToolDispatcher` usage, `get_issue`/`get_repository` tool names
- `src/GsdOrchestrator/Workflows/States/AnalyzingState.cs` — IChatClient usage, JSON response parsing with retry, `ChatOptions { Temperature = 0.1f }`
- `src/GsdOrchestrator/Workflows/States/ReviewingState.cs` — `add_issue_comment` pattern, IChatClient injection
- `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` — WorkflowState enum, GsdWorkflowContext record, Transition() method
- `src/GsdOrchestrator/Program.cs` — CLI args parsing pattern, state registration, `list_issues` MCP tool usage
- `src/GsdOrchestrator/Mcp/McpToolDispatcher.cs` — `CallAsync` signature
- `src/GsdOrchestrator/GsdOrchestrator.csproj` — installed packages
- `.planning/phases/12-robustness-foundation/12-03-SUMMARY.md` — NSubstitute test pattern, xUnit project structure

### Tertiary (LOW confidence — flagged as ASSUMED)

- `update_issue` tool name — inferred from GitHub REST API naming conventions; not verified against live MCP server tool list
- `ChatResponse` mock setup — inferred from MEL 10.x patterns; exact constructor signature not verified in this session

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages verified from csproj
- Architecture: HIGH — all patterns verified from existing state implementations
- GitHub MCP tools (close/label): LOW — `update_issue` name assumed, not probed from running server
- Test strategy: HIGH — NSubstitute pattern verified from Phase 12-03 SUMMARY

**Research date:** 2026-06-01
**Valid until:** 2026-07-01 (stable .NET/xUnit/NSubstitute ecosystem)

---

## RESEARCH COMPLETE

**Phase:** 13 — Smarter Issue Triage
**Confidence:** HIGH (with LOW confidence on `update_issue` tool name — verify at plan time)

### Key Findings

- `TriagingState` integrates with zero new packages — `IChatClient`, `McpToolDispatcher`, and all supporting infrastructure already injected
- `IdleState.cs` has exactly one change: `.Transition(WorkflowState.Analyzing)` becomes `.Transition(WorkflowState.Triaging)`
- LLM classification follows the same retry-on-parse-failure pattern as `AnalyzingState` with a simpler JSON envelope
- Clean skip exit uses `WorkflowState.Done` (not a new enum value) — state machine's `ArchiveAsync` path handles it correctly
- `--triage` CLI mode is a single bool flag; propagated via `GsdWorkflowContext.TriageModeOnly` so it survives checkpointing
- The only unverified item is the exact GitHub MCP tool name for closing issues (`update_issue` assumed) — plan task should probe `list_tools` first

### File Created

`.planning/phases/13-smarter-issue-triage/13-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | All packages verified from csproj |
| Architecture / State Pattern | HIGH | All patterns verified from existing state implementations |
| MCP Tool Names (close issue) | LOW | `update_issue` assumed — not probed from live binary |
| Test Strategy | HIGH | NSubstitute + xUnit pattern verified from Phase 12-03 |
| CLI Mode Implementation | HIGH | Args parsing pattern verified from Program.cs |

### Open Questions

1. `update_issue` vs `close_issue` — tool name needs live probe at plan time
2. `needs-info` outcome — research recommends `Done` + comment; planner should confirm
3. `TriageModeOnly` propagation approach — context property recommended; planner may prefer IConfiguration

### Ready for Planning

Research complete. Planner can now create PLAN.md files.
