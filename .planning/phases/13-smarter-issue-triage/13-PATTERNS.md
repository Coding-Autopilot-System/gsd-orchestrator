# Phase 13: Smarter Issue Triage — Pattern Map

**Mapped:** 2026-06-01
**Files analyzed:** 5 (3 new, 2 modified)
**Analogs found:** 5 / 5

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` | model | CRUD | self (existing file) | exact |
| `src/GsdOrchestrator/Workflows/States/IdleState.cs` | state | request-response | self (single-line change) | exact |
| `src/GsdOrchestrator/Workflows/States/TriagingState.cs` | state | request-response + event-driven | `src/GsdOrchestrator/Workflows/States/AnalyzingState.cs` | exact |
| `src/GsdOrchestrator/Program.cs` | config / entry-point | request-response | self (existing file) | exact |
| `src/GsdOrchestrator.Tests/TriagingStateTests.cs` | test | CRUD | `GsdStateMachineTests.cs` (described in 12-03-SUMMARY.md) | role-match |

---

## Pattern Assignments

### `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` (model, CRUD)

**Analog:** self — existing file read at lines 1-102

**Three changes required in this file:**

**Change 1 — Enum extension** (lines 5-18, insert `Triaging` between `Idle` and `Analyzing`):
```csharp
public enum WorkflowState
{
    Idle,
    Triaging,      // INSERT HERE — between Idle and Analyzing
    Analyzing,
    Branching,
    Editing,
    Validating,
    Committing,
    PrCreating,
    Reviewing,
    Documenting,
    Done,
    Failed
}
```

**Change 2 — New TriageResult record** (add after existing per-state output models, before `GsdWorkflowContext`):
```csharp
public sealed record TriageResult(
    string Classification,
    string Reason,
    int? DuplicateNumber);
```

**Change 3 — GsdWorkflowContext property addition** (add inside the `GsdWorkflowContext` record body, after `PullRequest` on line 89, following the exact `init`-property pattern of lines 83-93):
```csharp
public TriageResult? Triage { get; init; }
public bool TriageModeOnly { get; init; } = false;
```

The `Transition()` method on lines 95-101 is unchanged — `TriagingState` calls it identically to every other state.

---

### `src/GsdOrchestrator/Workflows/States/IdleState.cs` (state, request-response)

**Analog:** self — existing file read at lines 1-66

**Single-line change** (line 64 — last statement of `ExecuteAsync`):

Before:
```csharp
return (ctx with { Issue = issue }).Transition(WorkflowState.Analyzing);
```

After:
```csharp
return (ctx with { Issue = issue }).Transition(WorkflowState.Triaging);
```

No other changes. All imports, constructor, `_owner`/`_repo` config resolution, MCP calls, and logging remain identical.

---

### `src/GsdOrchestrator/Workflows/States/TriagingState.cs` (state, request-response + event-driven) — CREATE

**Analog:** `src/GsdOrchestrator/Workflows/States/AnalyzingState.cs` (exact role + data flow match)

**Imports pattern** (copy from `AnalyzingState.cs` lines 1-8, add `IConfiguration`-free variant since triage does not need `_owner`/`_repo` from config — those come from `ctx.Issue`):
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;
```

**Class declaration and constructor** (copy from `AnalyzingState.cs` lines 10-23, replace type names):
```csharp
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
```

**ExecuteAsync skeleton** (follows `AnalyzingState.cs` lines 25-57 structure):
```csharp
    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        _logger.LogInformation("Triaging issue #{Number}: {Title}", issue.Number, issue.Title);

        // 1. Fetch open issues for duplicate context (list_issues pattern from Program.cs lines 155-168)
        // 2. Build classification prompt
        // 3. LLM retry loop (copy from AnalyzingState.cs lines 36-50)
        // 4. Branch on classification result
        // 5. Return ctx.Transition(...)
    }
```

**LLM retry loop pattern** (copy verbatim from `AnalyzingState.cs` lines 36-50 — exact same structure):
```csharp
        TriageResult? triageResult = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var response = await _llm.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Temperature = 0.1f },
                ct);

            var text = response.Text ?? "";
            triageResult = TryParseTriageResult(text);

            if (triageResult is not null) break;

            _logger.LogWarning("TriageResult parse failed on attempt {Attempt}/3", attempt);
            prompt += $"\n\nAttempt {attempt} failed to parse. Return ONLY valid JSON, no markdown fences.";
        }

        if (triageResult is null)
            throw new InvalidOperationException("LLM failed to produce a valid TriageResult after 3 attempts.");
```

**JSON parse helper** (copy from `AnalyzingState.cs` lines 105-129 pattern — strip fences, parse with null-safe navigation):
```csharp
    private static TriageResult? TryParseTriageResult(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```")) text = string.Join('\n', text.Split('\n').Skip(1).SkipLast(1));

        try
        {
            var node = JsonNode.Parse(text.Trim());
            if (node is null) return null;

            var classification = node["classification"]?.GetValue<string>() ?? "";
            if (classification is not ("actionable" or "needs-info" or "duplicate" or "out-of-scope"))
            {
                // Pitfall 1 mitigation: unknown classification falls back to actionable (conservative)
                // Log at Warning level — do NOT log issue body content
                return new TriageResult("actionable", $"Unknown classification '{classification}' — defaulting to actionable", null);
            }

            return new TriageResult(
                Classification: classification,
                Reason: node["reason"]?.GetValue<string>() ?? "",
                DuplicateNumber: node["duplicateNumber"]?.GetValue<int?>());
        }
        catch { return null; }
    }
```

**MCP list_issues call for duplicate context** (pattern from `Program.cs` lines 155-168):
```csharp
        var issuesResult = await _mcp.CallAsync("list_issues", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["state"] = "open",
            ["perPage"] = 50
        }, ct);
        var openIssues = issuesResult.ParseInnerJson()?.AsArray() ?? [];
```

**add_issue_comment call** (pattern from `ReviewingState.cs` lines 43-49 and `GsdStateMachine.cs` lines 121-127):
```csharp
        await _mcp.CallAsync("add_issue_comment", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["issue_number"] = issue.Number,
            ["body"] = $"..triage comment text.."
        }, ct);
```

**update_issue close call** (assumed tool name — wrap in try/catch per Pitfall 2):
```csharp
        try
        {
            await _mcp.CallAsync("update_issue", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["issue_number"] = issue.Number,
                ["state"] = "closed"
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "update_issue call failed for #{Number} — comment was posted, continuing to Done", issue.Number);
        }
```

**Transition pattern — clean skip exits** (GsdStateMachine.cs line 60 + lines 92-99 confirm `Done` is the correct terminal state for skipped issues):
```csharp
        // Actionable (or TriageModeOnly = true): store triage result on context, branch
        return (ctx with { Triage = triageResult }).Transition(
            ctx.TriageModeOnly || triageResult.Classification != "actionable"
                ? WorkflowState.Done
                : WorkflowState.Analyzing);
```

**Prompt raw string literal note** (Phase 12 Pitfall verified in RESEARCH.md): use `$$"""..."""` (double-dollar) for the classification prompt body so `{{issue.Number}}`-style interpolation works. Do NOT use plain `$"""..."""` with backtick content.

---

### `src/GsdOrchestrator/Program.cs` (config / entry-point, request-response)

**Analog:** self — existing file read at lines 1-224

**Change 1 — Args parsing block** (add `triageModeOnly` after `watchMode` on line 19, and add parse clause in the `for` loop):

Before (lines 17-26):
```csharp
int? issueNumber = null;
string? resumeId = null;
bool watchMode = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--issue" && i + 1 < args.Length && int.TryParse(args[i + 1], out var n)) issueNumber = n;
    if (args[i] == "--resume" && i + 1 < args.Length) resumeId = args[i + 1];
    if (args[i] == "--watch") watchMode = true;
}
```

After:
```csharp
int? issueNumber = null;
string? resumeId = null;
bool watchMode = false;
bool triageModeOnly = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--issue" && i + 1 < args.Length && int.TryParse(args[i + 1], out var n)) issueNumber = n;
    if (args[i] == "--resume" && i + 1 < args.Length) resumeId = args[i + 1];
    if (args[i] == "--watch") watchMode = true;
    if (args[i] == "--triage") triageModeOnly = true;
}
```

**Change 2 — Validation guard** (extend the existing null check on lines 28-35 to require `--issue` when `--triage` is set):
```csharp
if (triageModeOnly && issueNumber is null)
{
    Console.Error.WriteLine("Error: --triage requires --issue <number>");
    Environment.Exit(1);
}

if (issueNumber is null && resumeId is null && !watchMode)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run -- --issue <number>               Run workflow for a specific issue");
    Console.Error.WriteLine("  dotnet run -- --issue <number> --triage      Classify issue only (no code changes)");
    Console.Error.WriteLine("  dotnet run -- --resume <workflow-id>         Resume an interrupted workflow");
    Console.Error.WriteLine("  dotnet run -- --watch                        Poll open issues and process them automatically");
    Environment.Exit(1);
}
```

**Change 3 — State registration** (add `TriagingState` after `IdleState` on line 89, following the exact singleton pattern):
```csharp
// ── Workflow states ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IWorkflowState, IdleState>();
builder.Services.AddSingleton<IWorkflowState, TriagingState>();   // ADD THIS LINE
builder.Services.AddSingleton<IWorkflowState, AnalyzingState>();
// ... rest unchanged
```

**Change 4 — Pass TriageModeOnly into RunAsync** (modify the `else` branch on lines 133-136):
```csharp
else
{
    var result = await sm.RunAsync(owner, repo, issueNumber!.Value, triageModeOnly, cts.Token);
    PrintResult(result);
}
```

This requires `GsdStateMachine.RunAsync` to accept a `bool triageModeOnly` parameter and set it on the initial `GsdWorkflowContext`. Alternatively, pass it via context initialization directly — see `GsdStateMachine.cs` lines 31-43 for the `RunAsync` context construction pattern.

---

### `src/GsdOrchestrator.Tests/TriagingStateTests.cs` (test, CRUD) — CREATE

**Analog:** `GsdStateMachineTests.cs` — described in `12-03-SUMMARY.md` (file committed to CI but not present in working tree on current branch `phase/1-foundation`)

**Test project infrastructure** (from 12-03-SUMMARY.md — already exists on main, must be merged or recreated for this branch):

- `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` — targets `net10.0`, packages: xunit 2.9.3, NSubstitute 5.3.0, coverlet.collector 10.0.1, Microsoft.NET.Test.Sdk 18.6.0, xunit.runner.visualstudio 3.1.5
- References main project via `<ProjectReference>`

**NSubstitute mock setup pattern** (from 12-03-SUMMARY.md technical approach — McpToolDispatcher requires concrete construction with pass-through pipeline):
```csharp
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

public class TriagingStateTests
{
    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        // Pass-through no-op pipeline — same approach as GsdStateMachineTests
        var registry = new ResiliencePipelineRegistry<string>();
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    private static GsdWorkflowContext BuildContext(bool triageModeOnly = false) =>
        new GsdWorkflowContext
        {
            Issue = new IssueContext(42, "Test issue", "Some body", [], "owner", "repo", "main"),
            CurrentState = WorkflowState.Triaging,
            TriageModeOnly = triageModeOnly
        };
```

**IChatClient mock pattern for returning JSON** (from RESEARCH.md Test Strategy section):
```csharp
    private static IChatClient BuildLlm(string jsonResponse)
    {
        var llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, jsonResponse)]));
        return llm;
    }
```

**Test cases to implement** (7 facts covering all TRIAGE-01 through TRIAGE-04 requirements):

```csharp
    [Fact]
    public async Task ExecuteAsync_ActionableClassification_TransitionsToAnalyzing() { }

    [Fact]
    public async Task ExecuteAsync_NeedsInfoClassification_TransitionsToDone() { }

    [Fact]
    public async Task ExecuteAsync_OutOfScopeClassification_TransitionsToDone() { }

    [Fact]
    public async Task ExecuteAsync_DuplicateClassification_TransitionsToDoneAndCallsUpdateIssue() { }

    [Fact]
    public async Task ExecuteAsync_TriageModeOnlyTrue_ActionableStillTransitionsToDone() { }

    [Fact]
    public async Task ExecuteAsync_LlmParseFailureAllAttempts_ThrowsInvalidOperationException() { }

    [Fact]
    public async Task ExecuteAsync_AnyClassification_PostsCommentViaAddIssueComment() { }
```

**IMcpClient mock for list_issues** (pattern follows `McpToolResult` return for `ParseInnerJson()` calls — mock must return a valid JSON array):
```csharp
        var mcpClient = Substitute.For<IMcpClient>();
        mcpClient.CallToolAsync(
            Arg.Is<string>(t => t == "list_issues"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(new McpToolResult { Text = "[{\"number\":1,\"title\":\"Other issue\"}]" });

        // add_issue_comment — no return value needed, just verify call
        mcpClient.CallToolAsync(
            Arg.Is<string>(t => t == "add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(new McpToolResult { Text = "" });
```

**Assertion pattern** (from GsdStateMachineTests described in 12-03-SUMMARY.md — check `CurrentState` on returned context):
```csharp
        var result = await state.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Analyzing, result.CurrentState);
        Assert.Equal("actionable", result.Triage?.Classification);
```

---

## Shared Patterns

### IWorkflowState Contract
**Source:** `src/GsdOrchestrator/Workflows/States/IWorkflowState.cs` lines 1-9
**Apply to:** `TriagingState.cs`
```csharp
public interface IWorkflowState
{
    WorkflowState State { get; }
    Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct);
}
```
`TriagingState` must implement both members. `State` returns `WorkflowState.Triaging`. Constructor injection is not part of the interface — follows the concrete pattern from `AnalyzingState`.

### State Transition via `with` + `Transition()`
**Source:** `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` lines 95-101
**Apply to:** `TriagingState.cs`
```csharp
public GsdWorkflowContext Transition(WorkflowState to, string? detail = null) =>
    this with
    {
        History = [.. History, new StateTransitionEvent(CurrentState, to, DateTimeOffset.UtcNow, detail)],
        CurrentState = to,
        RetryCount = 0
    };
```
Always use `(ctx with { Triage = triageResult }).Transition(nextState)` — never set `CurrentState` directly.

### McpToolDispatcher.CallAsync Signature
**Source:** `src/GsdOrchestrator/Mcp/McpToolDispatcher.cs` lines 28-30
**Apply to:** `TriagingState.cs` — all MCP calls
```csharp
public async Task<McpToolResult> CallAsync(
    string tool, JsonObject args, CancellationToken ct = default)
```
All MCP calls go through `_mcp.CallAsync`, never `_client.CallToolAsync` directly. The Polly retry pipeline is already wired.

### ParseInnerJson() Extension Pattern
**Source:** Used in `IdleState.cs` lines 37, 48 and `Program.cs` line 163
**Apply to:** `TriagingState.cs` — parsing `list_issues` response
```csharp
var openIssues = issuesResult.ParseInnerJson()?.AsArray() ?? [];
```
Returns `JsonNode?` — always use null-safe `?.` navigation and `?? []` fallback.

### Done Terminal State (clean exit)
**Source:** `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` lines 60, 92-99
**Apply to:** `TriagingState.cs` — all non-actionable exit paths
```csharp
// Loop exits cleanly on Done
while (ctx.CurrentState is not WorkflowState.Done and not WorkflowState.Failed)

// Done path calls ArchiveAsync (no failure comment)
else
    await _checkpoints.ArchiveAsync(ctx.WorkflowId, ct);
```
Duplicate, out-of-scope, needs-info, and triage-mode-only exits MUST use `WorkflowState.Done` (not `Failed`).

### Logging — No Issue Body Content at Info Level
**Source:** Security threat pattern verified in 12-01-SUMMARY (T-12-01) + `AnalyzingState.cs` line 28
**Apply to:** `TriagingState.cs`
```csharp
// Correct: log issue number and classification only
_logger.LogInformation("Triaging issue #{Number}: {Title}", issue.Number, issue.Title);
_logger.LogInformation("Triage result: #{Number} = {Classification}", issue.Number, triageResult.Classification);

// Wrong: do not log issue.Body at Info level (prompt injection risk)
```

---

## No Analog Found

All 5 files have close analogs. No entries in this section.

---

## Notes for Planner

1. **Test project may not exist on current branch** (`phase/1-foundation`). The `GsdOrchestrator.Tests` project was created in Phase 12-03 commits that are on `main` but not on this branch. The plan must either merge `main` first or recreate the csproj + solution entry before adding `TriagingStateTests.cs`.

2. **`GsdStateMachine.RunAsync` signature change** — if `TriageModeOnly` is passed at context construction time in `Program.cs`, the `RunAsync` method in `GsdStateMachine.cs` (lines 29-44) needs an additional `bool triageModeOnly = false` parameter OR the caller constructs the initial context with `TriageModeOnly = true` before passing it in. The latter approach avoids touching `GsdStateMachine` at all.

3. **`update_issue` tool name** — LOW confidence per RESEARCH.md. The plan task should call `_mcp.ListToolsAsync()` at the start of implementation to confirm the exact name, then wrap the call in try/catch regardless.

4. **Raw string literal pitfall** — use `$$"""..."""` for the LLM prompt in `TriagingState.cs`. The `{{variable}}` syntax with double braces is required for interpolation in raw string literals. Verified in Phase 12 Plan 01 SUMMARY (D-03).

---

## Metadata

**Analog search scope:** `src/GsdOrchestrator/Workflows/` (all states, models, machine), `src/GsdOrchestrator/Program.cs`, `src/GsdOrchestrator/Mcp/`
**Files read:** WorkflowModels.cs, IdleState.cs, AnalyzingState.cs, ReviewingState.cs, GsdStateMachine.cs, IWorkflowState.cs, McpToolDispatcher.cs, Program.cs, 12-03-SUMMARY.md
**Pattern extraction date:** 2026-06-01
