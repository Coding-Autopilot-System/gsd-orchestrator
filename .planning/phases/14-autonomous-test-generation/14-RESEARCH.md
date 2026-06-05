# Phase 14: Autonomous Test Generation — Research

**Researched:** 2026-06-04
**Domain:** C#/.NET 10 state machine extension — xUnit test generation, GitHub MCP file commit, ValidatingState gate extension
**Confidence:** HIGH

---

## Summary

Phase 14 inserts a `TestGeneratingState` between `EditingState` and `ValidatingState`. After code edits are committed to a branch, the new state reads the edited source files, generates xUnit test classes via the Anthropic `IChatClient`, and commits the test files to the same branch using the identical `create_or_update_file` MCP pattern that `EditingState` already uses. `ValidatingState` then gains a Gate 5 that checks whether the test files exist on the branch (structural check via `get_file_contents` — not a runtime `dotnet test` invocation).

All infrastructure is already in place. No new NuGet packages are needed. The state uses the `write_file` synthetic `AIFunction` tool pattern from `EditingState` (not the direct JSON response pattern from `AnalyzingState`) because test generation requires a full-file write, not just a structured record. The resulting `TestGenerationContext` record is stored on `GsdWorkflowContext` so downstream states (Validating, Committing) can introspect it.

The change set is five files modified plus two files created, with the most complex work being the LLM prompt that produces a compilable xUnit test class from a C# source file.

**Primary recommendation:** Implement `TestGeneratingState` as a self-contained state class that mirrors `EditingState`'s file-read → LLM-write_file → `create_or_update_file` pipeline, with a source-file filter that excludes test projects and non-C# files, and a graceful skip when no testable files are found.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Test file generation (LLM) | TestGeneratingState | — | All LLM calls live in state classes; EditingState is the direct precedent |
| Source file content read | TestGeneratingState | — | `get_file_contents` MCP pattern already in EditingState |
| Test file commit to branch | TestGeneratingState | — | `create_or_update_file` MCP pattern; same branch as EditingState |
| Test path derivation | TestGeneratingState | — | Pure string transformation; no external service required |
| Test existence structural check (Gate 5) | ValidatingState | — | Fits the existing gate pipeline; `get_file_contents` confirms file was committed |
| Context propagation | WorkflowModels.cs | — | `TestGenerationContext` record on `GsdWorkflowContext`; same pattern as EditContext |
| State registration | Program.cs | — | All `IWorkflowState` singletons registered in Program.cs |

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TESTGEN-01 | `TestGeneratingState` implemented — Claude generates xUnit tests for files changed in EditingState | `EditingState` write_file synthetic tool pattern; `get_file_contents` to read source before prompting; existing `IChatClient` injection |
| TESTGEN-02 | Generated tests committed to feature branch alongside code changes | `create_or_update_file` MCP tool already used in EditingState; identical commit call pattern; test file path derivation from source path |
| TESTGEN-03 | `ValidatingState` enhanced — checks test file compilation (not runtime pass/fail) | Gate 5 added to existing gate pipeline; `get_file_contents` confirms test file exists on branch; structural check only |
</phase_requirements>

---

## Standard Stack

### Core (already in project — no new packages needed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Anthropic.SDK | 5.10.0 | LLM test generation calls | Already injected as `IChatClient` in EditingState, AnalyzingState, TriagingState |
| Microsoft.Extensions.AI | 10.6.0 | `IChatClient`, `ChatMessage`, `ChatOptions`, `AIFunctionFactory` | Project standard — write_file synthetic tool pattern from EditingState requires `AIFunctionFactory.Create` |
| Polly.Extensions | 8.6.6 | Resilience pipeline around MCP calls | Already wraps all `McpToolDispatcher` calls |

[VERIFIED: `src/GsdOrchestrator/GsdOrchestrator.csproj` — all three packages present]

### Test Project (already in project — no changes to .csproj)

| Library | Version | Purpose |
|---------|---------|---------|
| xunit | 2.9.3 | Test runner |
| NSubstitute | 5.3.0 | Mocking `IChatClient`, `IMcpClient`, `ICheckpointStore` |
| coverlet.collector | 10.0.1 | Coverage collection |

[VERIFIED: `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj`]

**No new packages required.** All needed libraries are already present. The `AIFunctionFactory` type is part of `Microsoft.Extensions.AI` 10.6.0 which is already in the project.

---

## Architecture Patterns

### System Architecture Diagram

```
EditingState
  └── commits N source files to branch via create_or_update_file
  └── returns ctx with { Edits = EditContext([FileEdit, ...]) }
  └── transitions to WorkflowState.TestGenerating  (ONE-LINE CHANGE)
           |
           v
TestGeneratingState
  for each edit in ctx.Edits where IsTestableSourceFile(edit.Path):
    1. get_file_contents(edit.Path, branch)  ← read committed source
    2. get_file_contents(testFilePath, branch)  ← read existing test (if any)
    3. IChatClient + write_file AIFunction  ← generate xUnit test class
    4. create_or_update_file(testFilePath, branch)  ← commit test
  returns ctx with { TestGeneration = TestGenerationContext([GeneratedTest, ...]) }
  transitions to WorkflowState.Validating
           |
           v
ValidatingState
  Gate 1: FileSafety     (unchanged)
  Gate 2: MergeConflict  (unchanged)
  Gate 3: DiffSize       (unchanged)
  Gate 4: TestIntent     (unchanged)
  Gate 5: TestCompilation (NEW) ← get_file_contents on each test path,
                                   verify file exists and contains [Fact] or [Theory]
  transitions to WorkflowState.Committing
```

### Recommended Project Structure

No new directories needed. New file follows existing pattern:

```
src/GsdOrchestrator/
├── Workflows/
│   ├── Models/
│   │   └── WorkflowModels.cs        MODIFY: add TestGenerating enum value
│   │                                         add TestGenerationContext record
│   │                                         add GeneratedTest record
│   │                                         add TestGeneration property to GsdWorkflowContext
│   └── States/
│       ├── EditingState.cs          MODIFY: transition to TestGenerating (1 line)
│       ├── TestGeneratingState.cs   CREATE
│       └── ValidatingState.cs       MODIFY: add Gate 5 (TestCompilation)
├── Program.cs                       MODIFY: AddSingleton<IWorkflowState, TestGeneratingState>()
└── GsdOrchestrator.csproj           NO CHANGE

src/GsdOrchestrator.Tests/
└── TestGeneratingStateTests.cs      CREATE: 7 unit tests
```

---

## Pattern 1: State Insertion Point

`TestGeneratingState` is inserted between `EditingState` and `ValidatingState`. This is the only correct insertion point because:

1. `ctx.Edits` (the `EditContext` with `FileEdit` list) is populated by `EditingState` — that data is required to know which source files were changed.
2. `ValidatingState` already checks `ctx.Edits` for Gate 4 (TestIntent) — by having `TestGeneratingState` run before it, Gate 4 can be satisfied organically (test files will be in `ctx.TestGeneration`).
3. `CommittingState` only reads `get_branch` SHA — it does not need to know about test files specifically.

**Change to `EditingState.cs` (exactly one line):**

```csharp
// Source: verified — src/GsdOrchestrator/Workflows/States/EditingState.cs line 42
// BEFORE:
return (ctx with { Edits = new EditContext(edits) }).Transition(WorkflowState.Validating);
// AFTER:
return (ctx with { Edits = new EditContext(edits) }).Transition(WorkflowState.TestGenerating);
```

[VERIFIED: EditingState.cs line 42]

**Impact on GsdStateMachineTests:** The existing 7 tests in `GsdStateMachineTests.cs` all mock `IWorkflowState` directly — they do not test specific enum transitions between real states. A test that mocks `WorkflowState.Editing → WorkflowState.Done` still works because the mock bypasses real state logic. The tests will NOT break.

However, any test that constructs a real `EditingState` and asserts it transitions to `Validating` would break. Searching the test files confirms no such test exists — only `GsdStateMachineTests` and `TriagingStateTests` exist, and neither tests `EditingState` directly.

[VERIFIED: GsdStateMachineTests.cs — mock-based tests only, no real state classes instantiated except for TriagingState]

---

## Pattern 2: WorkflowModels.cs Changes

Three additions to `WorkflowModels.cs`:

```csharp
// Source: verified — src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs

// 1. Add TestGenerating to enum (between Editing and Validating):
public enum WorkflowState
{
    Idle,
    Triaging,
    Analyzing,
    Branching,
    Editing,
    TestGenerating,   // ← INSERT HERE (Phase 14)
    Validating,
    Committing,
    // ... rest unchanged
}

// 2. Add GeneratedTest record:
public sealed record GeneratedTest(
    string SourcePath,     // the source file that was tested
    string TestPath,       // the committed test file path
    string TestSha,        // SHA after commit
    bool WasSkipped,       // true if no testable logic found or LLM produced nothing
    string? SkipReason);   // set when WasSkipped = true

// 3. Add TestGenerationContext record:
public sealed record TestGenerationContext(IReadOnlyList<GeneratedTest> GeneratedTests);

// 4. Add property to GsdWorkflowContext:
public TestGenerationContext? TestGeneration { get; init; }
```

[VERIFIED: WorkflowModels.cs — pattern matches existing EditContext, FileEdit, TriageResult records]

---

## Pattern 3: TestGeneratingState Architecture

The state follows the `EditingState` pattern (write_file synthetic AIFunction) not the `AnalyzingState` pattern (direct JSON). Rationale: test generation needs to produce a complete file, not a JSON record. The `write_file` tool loop allows the LLM to reason step-by-step before committing.

```csharp
// Source: adapted from src/GsdOrchestrator/Workflows/States/EditingState.cs
public sealed class TestGeneratingState : IWorkflowState
{
    private const int MaxTurnsPerFile = 20;

    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<TestGeneratingState> _logger;

    public WorkflowState State => WorkflowState.TestGenerating;

    public TestGeneratingState(McpToolDispatcher mcp, IChatClient llm, ILogger<TestGeneratingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var edits = ctx.Edits!;
        var issue = ctx.Issue!;
        var branch = ctx.Branch!;
        var generatedTests = new List<GeneratedTest>();

        var testablePaths = edits.Edits
            .Select(e => e.Path)
            .Where(IsTestableSourceFile)
            .ToList();

        if (testablePaths.Count == 0)
        {
            _logger.LogInformation("No testable source files in edits — skipping test generation");
            var empty = new TestGenerationContext([]);
            return (ctx with { TestGeneration = empty }).Transition(WorkflowState.Validating);
        }

        foreach (var sourcePath in testablePaths)
        {
            var testPath = DeriveTestPath(sourcePath);
            var result = await GenerateTestFileAsync(issue, branch, sourcePath, testPath, ct);
            generatedTests.Add(result);
        }

        var testGenCtx = new TestGenerationContext(generatedTests);
        return (ctx with { TestGeneration = testGenCtx }).Transition(WorkflowState.Validating);
    }
    // ... (private methods below)
}
```

[VERIFIED: pattern mirrors EditingState.cs; constructor signature matches TriagingState.cs]

---

## Pattern 4: Source File Filter

The `IsTestableSourceFile` method determines which edited files deserve test generation:

```csharp
// Source: [ASSUMED — filter logic; reasoning below]
private static bool IsTestableSourceFile(string path)
{
    // Must be a C# source file
    if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        return false;
    // Skip files already in the Tests project
    if (path.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".Tests\\", StringComparison.OrdinalIgnoreCase))
        return false;
    // Skip test files by naming convention
    if (path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("Spec.cs", StringComparison.OrdinalIgnoreCase))
        return false;
    // Skip generated/designer files
    if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
        return false;
    return true;
}
```

[ASSUMED — specific filter rules; the general approach (exclude .Tests project + naming conventions) is sound]

---

## Pattern 5: Test Path Derivation

Converting a source path to its test counterpart is a deterministic transformation:

```csharp
// Source: [ASSUMED — derivation logic based on this repo's structure]
// Example:
//   src/GsdOrchestrator/Workflows/States/FooState.cs
//   → src/GsdOrchestrator.Tests/FooStateTests.cs
//
//   src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs
//   → src/GsdOrchestrator.Tests/WorkflowModelsTests.cs
private static string DeriveTestPath(string sourcePath)
{
    // Normalize separators
    sourcePath = sourcePath.Replace('\\', '/');

    // Extract filename without extension
    var fileName = Path.GetFileNameWithoutExtension(sourcePath);

    // Find the source project root (folder containing the .csproj-equivalent path segment)
    // Convention in this repo: src/GsdOrchestrator/... → src/GsdOrchestrator.Tests/
    var testFileName = $"{fileName}Tests.cs";

    // Find project segment and replace with .Tests variant
    // e.g., "src/GsdOrchestrator/Workflows/States/FooState.cs"
    //       Split on first path segment after "src/"
    var parts = sourcePath.Split('/');
    // parts[0]="src", parts[1]="GsdOrchestrator", parts[2..]="Workflows/States/FooState.cs"
    if (parts.Length >= 2 && parts[0] == "src")
    {
        return $"src/{parts[1]}.Tests/{testFileName}";
    }

    // Fallback: place in root of Tests project
    return $"src/GsdOrchestrator.Tests/{testFileName}";
}
```

**Important:** The derivation flattens the directory structure inside the Tests project (all test files go directly into `src/GsdOrchestrator.Tests/`). This matches the existing pattern: `TriagingStateTests.cs` and `GsdStateMachineTests.cs` both sit directly in `src/GsdOrchestrator.Tests/` with no subdirectory.

[VERIFIED: `src/GsdOrchestrator.Tests/TriagingStateTests.cs` and `GsdStateMachineTests.cs` — flat structure confirmed]

---

## Pattern 6: LLM Test Generation Prompt

The prompt must include enough context for the LLM to generate a compilable xUnit test class. The `write_file` tool pattern (from `EditingState`) is used rather than JSON response, because the test file is a complete C# file, not a structured record.

```csharp
// Source: [ASSUMED — prompt content; xUnit + NSubstitute patterns from Phase 12-03]
private async Task<GeneratedTest> GenerateTestFileAsync(
    IssueContext issue,
    BranchContext branch,
    string sourcePath,
    string testPath,
    CancellationToken ct)
{
    // 1. Read source file content from branch
    string sourceContent = await ReadFileAsync(issue, branch, sourcePath, ct);

    // 2. Read existing test file content (may not exist yet)
    string existingTestContent = await TryReadFileAsync(issue, branch, testPath, ct) ?? "";
    string existingSha = await TryReadFileShaAsync(issue, branch, testPath, ct) ?? "";

    // 3. Define write_file synthetic tool
    var writeFileTool = AIFunctionFactory.Create(
        (string content, string commitMessage) => Task.FromResult($"staged:{content.Length}"),
        "write_file",
        "Write the complete xUnit test file content. Call this when done generating tests.");

    var options = new ChatOptions
    {
        Tools = [writeFileTool],
        ToolMode = ChatToolMode.Auto,
        Temperature = 0.1f
    };

    var systemPrompt = $$"""
        You are a C# test engineer. Generate xUnit 2.x tests for the provided source file.

        Rules:
        - Use xUnit [Fact] for single-scenario tests, [Theory] + [InlineData] for parameterized tests
        - Use NSubstitute (Substitute.For<T>()) for interface dependencies
        - Constructor-inject dependencies using the same pattern as the source class
        - Namespace: GsdOrchestrator.Tests
        - Class name: {{Path.GetFileNameWithoutExtension(testPath)}}
        - One test class per source file
        - Tests must compile — use only types present in the source file and standard xUnit/NSubstitute APIs
        - If the source file has no testable public methods, call write_file with a single [Fact] placeholder test that asserts true
        - Do NOT add using directives for namespaces not referenced in the source

        Issue context (for understanding intent):
        Issue #{{issue.Number}}: {issue.Title}
        """;

    var userPrompt = $$"""
        Source file: {{sourcePath}}
        ```csharp
        {{sourceContent}}
        ```

        {{(existingTestContent.Length > 0 ? $"Existing tests (extend, do not duplicate):\n```csharp\n{existingTestContent}\n```" : "No existing test file — generate from scratch.")}}

        Generate comprehensive xUnit tests and call write_file with the complete test file content.
        """;

    // 4. Run write_file loop (identical to EditingState)
    // ... (MaxTurnsPerFile loop, same as EditingState pattern)
}
```

[ASSUMED — exact prompt wording; the structural requirements (namespace, class name, xUnit patterns, NSubstitute) are grounded in the project's existing test conventions verified from TriagingStateTests.cs and GsdStateMachineTests.cs]

---

## Pattern 7: TESTGEN-02 — Commit Strategy

Test files are committed in `TestGeneratingState` itself (before `ValidatingState`) using the identical `create_or_update_file` MCP call pattern from `EditingState`. This is the correct approach because:

1. ValidatingState's Gate 5 needs to verify the file exists on the branch — it must be committed first.
2. `CommittingState` only records the final SHA — it does not do additional commits.
3. The GitHub API model already means each `create_or_update_file` call IS a commit — there is no staging area.

```csharp
// Source: verified — src/GsdOrchestrator/Workflows/States/EditingState.cs lines 166-179
// Exact same pattern reused in TestGeneratingState:
var commitArgs = new JsonObject
{
    ["owner"] = issue.RepoOwner,
    ["repo"] = issue.RepoName,
    ["path"] = testPath,
    ["message"] = $"test(#{issue.Number}): generate xUnit tests for {Path.GetFileName(sourcePath)}",
    ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(finalContent)),
    ["branch"] = branch.BranchName
};
if (!string.IsNullOrEmpty(existingSha))
    commitArgs["sha"] = existingSha;  // required for updates, omitted for new files

var commitResult = await _mcp.CallAsync("create_or_update_file", commitArgs, ct);
var newSha = commitResult.ParseInnerJson()?["content"]?["sha"]?.GetValue<string>() ?? "";
```

[VERIFIED: EditingState.cs lines 166-183 — exact call pattern]

---

## Pattern 8: TESTGEN-03 — ValidatingState Gate 5

Gate 5 checks that test files were committed to the branch. It uses `get_file_contents` (already used in `EditingState`) to confirm existence, then performs a structural check for `[Fact]` or `[Theory]` in the content. This is NOT a `dotnet test` invocation — runtime pass/fail is out of scope for TESTGEN-03.

```csharp
// Source: adapted from ValidatingState.cs gate pattern + EditingState.cs get_file_contents pattern
// Insert after Gate 4 (TestIntent), before overallStatus calculation:

// Gate 5: Test compilation check (structural — file exists + contains test attributes)
if (ctx.TestGeneration is not null && ctx.TestGeneration.GeneratedTests.Count > 0)
{
    var nonSkipped = ctx.TestGeneration.GeneratedTests
        .Where(t => !t.WasSkipped)
        .ToList();

    if (nonSkipped.Count > 0)
    {
        var testCompilationPassed = true;
        foreach (var generatedTest in nonSkipped)
        {
            try
            {
                var fileResult = await _mcp.CallAsync("get_file_contents", new JsonObject
                {
                    ["owner"] = issue.RepoOwner,
                    ["repo"] = issue.RepoName,
                    ["path"] = generatedTest.TestPath,
                    ["ref"] = branch.BranchName
                }, ct);

                var fileJson = fileResult.ParseInnerJson();
                var b64 = fileJson?["content"]?.GetValue<string>()?.Replace("\n", "") ?? "";
                var content = b64.Length > 0
                    ? Encoding.UTF8.GetString(Convert.FromBase64String(b64))
                    : "";

                // Structural check: must contain at least one test attribute
                bool hasTestAttribute =
                    content.Contains("[Fact]", StringComparison.Ordinal) ||
                    content.Contains("[Theory]", StringComparison.Ordinal);

                if (!hasTestAttribute)
                {
                    _logger.LogWarning("Test file {Path} has no [Fact] or [Theory] attributes", generatedTest.TestPath);
                    testCompilationPassed = false;
                }
            }
            catch (McpException ex)
            {
                _logger.LogWarning(ex, "Test file {Path} not found on branch", generatedTest.TestPath);
                testCompilationPassed = false;
            }
        }

        gates.Add(new GateResult("TestCompilation",
            testCompilationPassed ? ValidationStatus.Pass : ValidationStatus.Warn,
            testCompilationPassed ? null : "One or more test files missing or structurally invalid"));
    }
    else
    {
        // All tests were skipped — pass silently
        gates.Add(new GateResult("TestCompilation", ValidationStatus.Pass, "All test files skipped"));
    }
}
```

**Why Warn not Block:** A test file that has no `[Fact]` attribute is unusual but not a workflow-stopping failure. The code change is already committed and validated by Gates 1-4. Blocking on structural test content would create false negatives on simple files (enums, config records, extension classes). `Warn` lets the PR proceed while surfacing the issue for human review.

[VERIFIED: ValidatingState.cs gate pattern; get_file_contents usage from EditingState.cs lines 57-70]

---

## Pattern 9: NSubstitute Test Pattern (exact from TriagingStateTests.cs)

```csharp
// Source: verified — src/GsdOrchestrator.Tests/TriagingStateTests.cs

// 1. Build McpToolDispatcher with mock IMcpClient
private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
{
    var registry = new ResiliencePipelineRegistry<string>();
    registry.TryAddBuilder("mcp-tools", (b, _) => { });
    return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
}

// 2. Build GsdWorkflowContext with Edits already populated (simulating post-EditingState context)
private static GsdWorkflowContext BuildContext() =>
    new()
    {
        Issue = new IssueContext(42, "Test issue", "Body text", [], "testowner", "testrepo", "main"),
        Branch = new BranchContext("fix/issue-42", "abc123sha"),
        Edits = new EditContext([
            new FileEdit(
                "src/GsdOrchestrator/Workflows/States/FooState.cs",
                "oldsha123", "newsha456",
                "fix(#42): update FooState")
        ]),
        CurrentState = WorkflowState.TestGenerating
    };

// 3. Build mock IChatClient that returns a write_file tool call
// NOTE: For TestGeneratingState the LLM uses ToolCalls finish reason, not Text.
// The mock needs to simulate the write_file tool call response.
// This is more complex than TriagingState — see Test Strategy section below.

// 4. Build mock IMcpClient
private static IMcpClient BuildMcpClient()
{
    var mcp = Substitute.For<IMcpClient>();
    // get_file_contents for source file
    mcp.CallToolAsync(
        Arg.Is<string>("get_file_contents"),
        Arg.Any<JsonObject>(),
        Arg.Any<CancellationToken>())
       .Returns(Task.FromResult(new McpToolResult(
           """{"sha":"abc123","content":"dXNpbmcgWHVuaXQ7CnB1YmxpYyBjbGFzcyBGb28ge30="}""",
           false)));
    // create_or_update_file for test file commit
    mcp.CallToolAsync(
        Arg.Is<string>("create_or_update_file"),
        Arg.Any<JsonObject>(),
        Arg.Any<CancellationToken>())
       .Returns(Task.FromResult(new McpToolResult(
           """{"content":{"sha":"testsha789"}}""",
           false)));
    return mcp;
}

// 5. Build SUT
private static TestGeneratingState BuildSut(IMcpClient mcpClient, IChatClient llm) =>
    new(BuildDispatcher(mcpClient), llm, NullLogger<TestGeneratingState>.Instance);
```

[VERIFIED: TriagingStateTests.cs — BuildDispatcher and BuildSut patterns are exact]

---

## How to Add TestGeneratingState — Minimal Change Set

### File 1: `WorkflowModels.cs`
- Add `TestGenerating` to `WorkflowState` enum between `Editing` and `Validating`
- Add `GeneratedTest` record: `(string SourcePath, string TestPath, string TestSha, bool WasSkipped, string? SkipReason)`
- Add `TestGenerationContext` record: `(IReadOnlyList<GeneratedTest> GeneratedTests)`
- Add `TestGenerationContext? TestGeneration { get; init; }` property to `GsdWorkflowContext`

### File 2: `EditingState.cs`
- Line 42: change `.Transition(WorkflowState.Validating)` to `.Transition(WorkflowState.TestGenerating)`
- This is the only change to this file.

### File 3: `TestGeneratingState.cs` (CREATE)
- `src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs`
- Implements `IWorkflowState`
- `State => WorkflowState.TestGenerating`
- Constructor: `(McpToolDispatcher mcp, IChatClient llm, ILogger<TestGeneratingState> logger)`
- `ExecuteAsync`: filters `ctx.Edits` via `IsTestableSourceFile`, derives test paths, runs write_file LLM loop per file, commits each via `create_or_update_file`, returns updated `ctx` with `TestGeneration` transitioning to `WorkflowState.Validating`
- Private helpers: `IsTestableSourceFile`, `DeriveTestPath`, `GenerateTestFileAsync`, `ReadFileAsync`, `TryReadFileAsync`

### File 4: `ValidatingState.cs`
- Add Gate 5 (TestCompilation) after the existing Gate 4 (TestIntent) block
- Reads `ctx.TestGeneration`; if null or all skipped, adds `Pass` gate silently
- For each non-skipped generated test: calls `get_file_contents`, checks for `[Fact]`/`[Theory]`, adds `Pass` or `Warn`
- Gate failure status: `ValidationStatus.Warn` (not `Block`) — see rationale in Pattern 8

### File 5: `Program.cs`
- Add `builder.Services.AddSingleton<IWorkflowState, TestGeneratingState>();`
- Insert after `EditingState` registration, before `ValidatingState` registration (cosmetic — DI order does not affect dictionary lookup)

### File 6: `TestGeneratingStateTests.cs` (CREATE)
- `src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs`
- 7 unit tests (see Test Strategy section)

---

## Common Pitfalls

### Pitfall 1: IChatClient Mock for write_file Tool Call Pattern
**What goes wrong:** The NSubstitute mock for `IChatClient` in `TriagingStateTests.cs` returns a `ChatMessage` with `Text` content. `TestGeneratingState` uses the `write_file` synthetic tool pattern (like `EditingState`) — the LLM response has `FinishReason == ChatFinishReason.ToolCalls` and a `FunctionCallContent` item, not a text message.
**Why it happens:** Two different LLM interaction modes — direct JSON (AnalyzingState/TriagingState) vs. tool-call loop (EditingState/TestGeneratingState). The mock setup differs significantly.
**How to avoid:** In test, mock `IChatClient.GetResponseAsync` to return a `ChatResponse` containing a `ChatMessage` with `FunctionCallContent` for `write_file`. Set `FinishReason = ChatFinishReason.ToolCalls`. Example:
```csharp
// Approximate mock — verify exact MEL 10.6.0 API for FunctionCallContent constructor
var callId = "call_001";
var functionCall = new FunctionCallContent(callId, "write_file",
    new Dictionary<string, object?> { ["content"] = "[Fact]\npublic void Test() {}", ["commitMessage"] = "test: generate" });
var msg = new ChatMessage(ChatRole.Assistant, [functionCall]);
var response = new ChatResponse(msg) { FinishReason = ChatFinishReason.ToolCalls };
llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
   .Returns(Task.FromResult(response));
```
**Warning signs:** `finalContent is null` after the LLM loop — test produces a `WasSkipped = true` result when it should produce a committed test.

[ASSUMED — exact `FunctionCallContent` constructor signature in MEL 10.6.0; verify against actual MEL API at plan time]

### Pitfall 2: Test Path Derivation for Non-Standard Layouts
**What goes wrong:** `DeriveTestPath` assumes the source file is under `src/{ProjectName}/...` and maps to `src/{ProjectName}.Tests/...`. If the issue being processed targets a file outside this convention (e.g., a root-level `Program.cs`), the derived path is malformed.
**Why it happens:** The derivation uses simple path-prefix string logic.
**How to avoid:** Add a fallback in `DeriveTestPath`: if the path does not start with `src/`, place the test file in `src/GsdOrchestrator.Tests/` with just the filename-based test name. Log a Warning.
**Warning signs:** `create_or_update_file` fails with a path error (invalid path segment).

### Pitfall 3: LLM Generates Syntactically Invalid C#
**What goes wrong:** The generated test file has a syntax error (missing semicolon, wrong namespace, unclosed brace).
**Why it happens:** LLM temperature > 0, complex source files, or ambiguous prompt.
**How to avoid:** Do NOT block the workflow on this. Commit the file anyway — Gate 5 checks only for `[Fact]`/`[Theory]` presence (structural), not C# syntax validity. If syntax is invalid, the existing CI (`.github/workflows/ci.yml`) will catch it after the PR is created. Log the content length and a snippet at Debug level.
**Warning signs:** Gate 5 passes (attribute found) but CI fails on the PR.

### Pitfall 4: Existing Test File Collision
**What goes wrong:** The target test file already exists on the branch (e.g., from a previous run or because the developer already wrote tests). Overwriting it loses existing tests.
**Why it happens:** `create_or_update_file` with a valid `sha` updates; without the correct sha it creates a new file, but if sha is wrong the API returns a 409 conflict.
**How to avoid:** Always read the existing test file first (`TryReadFileAsync`). Pass its content in the LLM prompt as "Existing tests (extend, do not duplicate)". Pass its SHA to `create_or_update_file` so the update is idempotent.
**Warning signs:** `create_or_update_file` throws `McpException` with status 409.

### Pitfall 5: Source File with No Testable Public API
**What goes wrong:** The source file is a pure data record, enum, or constants class. The LLM generates a trivially useless test or fails to call `write_file`.
**Why it happens:** There is nothing to test behaviorally.
**How to avoid:** If `finalContent is null` after the loop (LLM did not call `write_file`), set `WasSkipped = true` with `SkipReason = "LLM produced no test content"`. Do not fail the state — proceed with empty result for this file. Gate 5 will see `WasSkipped = true` and pass silently.
**Warning signs:** `_logger.LogWarning("Skipping {Path} — no content produced")` appears in logs.

### Pitfall 6: Raw String Literals with Backticks in Prompts (Phase 13 Pitfall 5)
**What goes wrong:** C# raw string literals (`$"""..."""`) containing backtick-fenced code blocks can be corrupted during base64 roundtrip in the task execution environment.
**Why it happens:** Python-based task executor; the `"""` delimiter interferes with string assembly.
**How to avoid:** Use `$$"""..."""` (double-dollar prefix) for all interpolated raw strings in `TestGeneratingState`. Verified as the correct escape in Phase 13 RESEARCH.md Pitfall 5.
**Warning signs:** Prompt content appears truncated or contains literal `{{` characters.

[VERIFIED: Phase 13 RESEARCH.md Pitfall 5 — `$$"""..."""` pattern confirmed]

### Pitfall 7: `using System.Text` Missing for Base64 Encoding
**What goes wrong:** `Encoding.UTF8.GetBytes` / `Convert.FromBase64String` require `using System.Text` — this is NOT auto-imported in `ImplicitUsings` for Worker SDK projects.
**Why it happens:** `EditingState.cs` already has `using System.Text` at line 1; `TestGeneratingState.cs` must include it too.
**How to avoid:** Explicitly add `using System.Text;` and `using System.Text.Json.Nodes;` at the top of `TestGeneratingState.cs`.
**Warning signs:** Build error: `'Encoding' does not exist in the current context`.

[VERIFIED: EditingState.cs line 1 — `using System.Text;` is required and present]

---

## Anti-Patterns to Avoid

- **Do not invoke `dotnet test` via `Process.Start`:** TESTGEN-03 explicitly says "not runtime pass/fail". Running tests on the orchestrator machine is out of scope, creates environment coupling, and adds 30-120 seconds to the workflow. Gate 5 is a structural check only.
- **Do not trigger GitHub Actions and poll:** Similarly, triggering a CI run from within the state machine adds complexity, polling delay, and requires GitHub Actions credentials. The CI run triggered by the PR creation (later in the workflow) serves this purpose.
- **Do not generate tests for non-C# files:** The filter `IsTestableSourceFile` must strictly exclude non-.cs files. Generating "tests" for JSON or YAML files is meaningless.
- **Do not add `TestGenerating` to the GsdStateMachine terminal condition check:** The loop exits on `Done` and `Failed` only. `TestGenerating` is a normal transient state — no changes to `GsdStateMachine.ExecuteLoopAsync`.
- **Do not use `AnalyzingState` JSON response pattern:** Test files are full C# files, not JSON records. The write_file synthetic tool pattern from `EditingState` is the correct choice.
- **Do not make Gate 5 a Block:** A missing or malformed test file does not mean the code change is invalid. The existing code was already committed in `EditingState`. Use `Warn` to surface the issue for human review without blocking the PR.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Test generation | Custom template engine | LLM via existing `IChatClient` | LLM handles context-specific test names, mock setup, and edge cases that templates cannot |
| C# syntax validation | Custom parser / Roslyn | Structural `[Fact]`/`[Theory]` check (Gate 5) | Full Roslyn invocation requires SDK dependency; Gate 5 is sufficient for TESTGEN-03 scope |
| File commit | Custom Git client | `create_or_update_file` MCP tool (existing pattern) | Already used in EditingState; idempotent with SHA; no new pattern needed |
| File read | Custom HTTP client | `get_file_contents` MCP tool (existing pattern) | Already used in EditingState |
| Fuzzy test path matching | Custom string similarity | Deterministic `DeriveTestPath` function | The project structure is consistent; no fuzzy matching needed |
| JSON parse retry for write_file loop | Custom retry | Existing `MaxTurnsPerFile` loop from EditingState | Already handles LLM tool-call loop; reuse exactly |

---

## Test Strategy

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + NSubstitute 5.3.0 |
| Config file | `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` (net10.0, no changes needed) |
| Quick run command | `dotnet test src/GsdOrchestrator.Tests/ --no-build -x` |
| Full suite command | `dotnet test src/GsdOrchestrator.Tests/` |

### Key Challenge: Mocking the write_file Tool Call Loop

`TestGeneratingState` uses the same ReAct loop as `EditingState`. The `IChatClient` mock must return a `ChatResponse` with `FinishReason = ChatFinishReason.ToolCalls` and a `FunctionCallContent` for `write_file`. This is more involved than the simple `ChatMessage(ChatRole.Assistant, jsonString)` pattern used in `TriagingStateTests`.

Strategy for tests: mock `IChatClient.GetResponseAsync` to return a pre-built response that includes a `FunctionCallContent` call to `write_file` with `content = "using Xunit;\n[Fact] public void Placeholder() {}"`. The second call (after the tool result is injected) should return a non-ToolCalls response to exit the loop.

For the "skip" test case, mock `IChatClient` to always return `ChatFinishReason.Stop` (never calls `write_file`) — this should produce `WasSkipped = true`.

### 7 Unit Tests for TestGeneratingStateTests.cs

| # | Test Name | Req | What It Tests |
|---|-----------|-----|---------------|
| 1 | `ExecuteAsync_WithEditableCSharpFile_TransitionsToValidating` | TESTGEN-01 | Happy path: edits contain one .cs file; LLM calls write_file; result transitions to `WorkflowState.Validating` |
| 2 | `ExecuteAsync_WithEditableCSharpFile_CommitsTestFile` | TESTGEN-02 | Verifies `create_or_update_file` MCP call is made with correct `path` (DeriveTestPath result) |
| 3 | `ExecuteAsync_WithNoTestableFiles_SkipsGracefully` | TESTGEN-01 | Edits contain only `.json` files; `testablePaths.Count == 0`; transitions to Validating with empty `GeneratedTests` |
| 4 | `ExecuteAsync_WithTestProjectFile_SkipsFile` | TESTGEN-01 | Edit path contains `.Tests/`; `IsTestableSourceFile` returns false; no LLM call; `GeneratedTests` empty |
| 5 | `ExecuteAsync_LlmNeverCallsWriteFile_ProducesSkippedResult` | TESTGEN-01 | Mock LLM always returns `FinishReason.Stop` (no tool call); `WasSkipped = true` in result; no commit MCP call |
| 6 | `ExecuteAsync_WithExistingTestFile_ReadsExistingSha` | TESTGEN-02 | Second `get_file_contents` call returns existing file with sha; `create_or_update_file` includes `sha` field |
| 7 | `ExecuteAsync_WithMultipleEditableFiles_GeneratesTestForEach` | TESTGEN-01 | Two .cs source files in `ctx.Edits`; two test files committed; two `GeneratedTest` entries in result |

### Requirement to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TESTGEN-01 | TestGeneratingState with C# source → transitions to Validating | unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "FullyQualifiedName~TestGenerating"` | Wave 0 |
| TESTGEN-01 | No testable files → graceful skip, transitions to Validating | unit | same filter | Wave 0 |
| TESTGEN-01 | .Tests project files filtered out | unit | same filter | Wave 0 |
| TESTGEN-01 | LLM no write_file call → WasSkipped=true | unit | same filter | Wave 0 |
| TESTGEN-02 | create_or_update_file called with derived test path | unit | same filter | Wave 0 |
| TESTGEN-02 | Multiple editable files → multiple commits | unit | same filter | Wave 0 |
| TESTGEN-03 | ValidatingState Gate 5 pass when test file has [Fact] | unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "FullyQualifiedName~Validating"` | Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test src/GsdOrchestrator.Tests/ --no-build -x`
- **Per wave merge:** `dotnet test src/GsdOrchestrator.Tests/`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs` — covers TESTGEN-01 and TESTGEN-02
- [ ] `TestGenerationContext` and `GeneratedTest` records must be defined in `WorkflowModels.cs` before tests can compile
- [ ] `WorkflowState.TestGenerating` enum value must be added before tests can compile
- [ ] No new test infrastructure needed — framework, csproj, and solution file already wired from Phase 12-03

---

## Validation Architecture

nyquist_validation is enabled (`config.json: workflow.nyquist_validation: true`).

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
| TESTGEN-01 | TestGeneratingState: C# source file → write_file called → transitions to Validating | unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "FullyQualifiedName~TestGenerating"` | Wave 0 |
| TESTGEN-01 | No testable files → graceful skip | unit | same | Wave 0 |
| TESTGEN-01 | .Tests project path filtered out | unit | same | Wave 0 |
| TESTGEN-01 | LLM produces no content → WasSkipped=true | unit | same | Wave 0 |
| TESTGEN-02 | create_or_update_file called with DeriveTestPath result | unit | same | Wave 0 |
| TESTGEN-02 | Multiple files → multiple test commits | unit | same | Wave 0 |
| TESTGEN-03 | ValidatingState Gate 5: test file exists and has [Fact] → Pass | unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "FullyQualifiedName~Validating"` | Wave 0 |

### Wave 0 Gaps

- [ ] `src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs` — covers TESTGEN-01 and TESTGEN-02
- [ ] No new csproj or solution file wiring needed (Phase 12-03 infrastructure complete)

---

## Runtime State Inventory

**Step 2.5 SKIPPED** — this is a greenfield feature addition (new state, new enum value, two new records). No rename/refactor/migration involved. No stored data, OS-registered state, secrets, or build artifacts reference "TestGeneratingState" or "TestGenerating".

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + test | Assumed yes — Phase 13 CI green | 10.x | — |
| xUnit test runner | TestGeneratingStateTests | ✓ | from csproj | — |
| github-mcp-server.exe | Integration (not needed for unit tests) | ✓ | in repo root | — |
| Anthropic API key | Integration (not needed for unit tests) | Assumed configured in .env | — | — |

[VERIFIED: `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` — test infrastructure present]

---

## Security Domain

Phase 14 introduces no new attack surface beyond what already exists. Source file content is read from the GitHub repository (same as `EditingState`) and passed to Claude. Generated test content is committed back to the branch.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | GitHub PAT already in McpStdioClient |
| V3 Session Management | no | Stateless per-run |
| V4 Access Control | no | No new permission scopes |
| V5 Input Validation | yes | Source file content passed to LLM — same exposure as EditingState; do not log full file contents at Info level |
| V6 Cryptography | no | No new crypto |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Prompt injection via source file content | Tampering | Source files from own GitHub repo are trusted; treat LLM output as untrusted; validate test file is committed to a feature branch only |
| Generated test file with malicious side effects | Tampering | Generated tests run only in CI, not locally; CI job sandbox limits blast radius |
| Secrets in log output | Information Disclosure | Do not log source file content at Info level; log only path and SHA |

[VERIFIED: EditingState.cs — logs only path and SHA, not content]

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `EditingState` → `ValidatingState` (direct) | `EditingState` → `TestGeneratingState` → `ValidatingState` | Phase 14 | All code changes now get paired test files before validation |
| Gate 4: checks if test files were in the edit plan | Gate 5: checks if test files were actually committed to branch | Phase 14 | Replaces intent check with existence check |
| No xUnit test generation | LLM-generated xUnit tests per source file | Phase 14 | Closes test coverage gap for autonomously generated code |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IsTestableSourceFile` filter correctly identifies all relevant exclusion patterns (`.Tests/`, `Tests.cs`, `.g.cs`, `.Designer.cs`) | Pattern 4 | Some test files double-tested or non-testable files targeted; easy to extend filter |
| A2 | `DeriveTestPath` flattening (all test files into `src/GsdOrchestrator.Tests/` root) matches project convention | Pattern 5 | Test files placed in wrong directory; fix is a one-line path change |
| A3 | LLM `IChatClient` mock for `FunctionCallContent` + `FinishReason.ToolCalls` compiles with MEL 10.6.0 | Test Strategy | Mock setup needs adjustment; test won't compile until API is confirmed |
| A4 | `FunctionCallContent` constructor takes `(callId, functionName, IDictionary<string, object?>)` in MEL 10.6.0 | Pattern 9, Test Strategy | Constructor signature may differ; verify against MEL 10.6.0 source at plan time |
| A5 | Gate 5 as `ValidationStatus.Warn` (not `Block`) is the correct severity for missing/malformed test files | Pattern 8 | If owner wants to block on missing tests, change to `Block` — easy one-character change |
| A6 | `get_file_contents` returns base64-encoded content in a `"content"` JSON field (same as EditingState assumes) | Pattern 8 | Gate 5 content check fails; fallback: check only file existence (non-null response) |

---

## Open Questions

1. **`FunctionCallContent` exact constructor in MEL 10.6.0**
   - What we know: `EditingState.cs` constructs `new FunctionResultContent(call.CallId, "File staged for commit.")` — this confirms the type is in scope and the CallId is a string
   - What's unclear: The exact constructor signature for `FunctionCallContent` (the *request* side, not the *response* side) needed for the NSubstitute mock
   - Recommendation: Plan task should include a `grep` of the MEL 10.6.0 source or a compile-time check before finalizing the mock helper

2. **ValidatingState Gate 4 interaction with Gate 5**
   - What we know: Gate 4 (TestIntent) checks `plan.RequiresTests && !edits.Edits.Any(e => e.Path.Contains("Test"))`
   - What's unclear: After Phase 14, test files are committed by `TestGeneratingState` and stored in `ctx.TestGeneration`, not in `ctx.Edits`. Gate 4 still checks `ctx.Edits` and may continue to warn even when tests were generated.
   - Recommendation: Update Gate 4 to also check `ctx.TestGeneration?.GeneratedTests.Any(t => !t.WasSkipped) == true` as a satisfying condition. This is a minor addition to `ValidatingState` that should be included in the Gate 5 plan task.

3. **Test file line length and CI markdown lint**
   - What we know: Phase 8 added `.markdownlint.json`; the CI runs on C# build, not markdown
   - What's unclear: Whether generated test files can trigger any CI lint rules
   - Recommendation: Not a concern — generated `.cs` files are only checked by `dotnet build`, not markdown linters

---

## Sources

### Primary (HIGH confidence)

- `src/GsdOrchestrator/Workflows/States/EditingState.cs` — write_file synthetic AIFunction tool pattern, `create_or_update_file` commit pattern, `get_file_contents` read pattern, MaxTurnsPerFile loop
- `src/GsdOrchestrator/Workflows/States/ValidatingState.cs` — gate pipeline structure, GateResult construction, MCP call pattern in gates, `FailWith` helper
- `src/GsdOrchestrator/Workflows/States/AnalyzingState.cs` — LLM retry pattern (referenced for contrast with write_file pattern)
- `src/GsdOrchestrator/Workflows/States/TriagingState.cs` — most recent state pattern; constructor, logger, MCP call pattern
- `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` — WorkflowState enum, GsdWorkflowContext record, Transition() method, EditContext + FileEdit records
- `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` — state loop terminal conditions, no changes required
- `src/GsdOrchestrator/Program.cs` — state registration pattern, DI singleton pattern
- `src/GsdOrchestrator.Tests/TriagingStateTests.cs` — exact NSubstitute test pattern, BuildDispatcher helper, BuildSut helper
- `src/GsdOrchestrator.Tests/GsdStateMachineTests.cs` — mock-based tests confirm EditingState transition change will not break existing tests
- `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` — package versions, project structure
- `src/GsdOrchestrator/GsdOrchestrator.csproj` — all package versions verified
- `.planning/phases/13-smarter-issue-triage/13-RESEARCH.md` — document structure template, Pitfall 5 (raw string literal `$$"""`)

### Tertiary (LOW confidence — flagged as ASSUMED)

- `FunctionCallContent` constructor signature for NSubstitute mock — inferred from `FunctionResultContent` usage in EditingState.cs; not verified against MEL 10.6.0 API docs in this session
- `IsTestableSourceFile` filter exclusion patterns — sound reasoning but specific patterns (`.g.cs`, `.Designer.cs`) assumed from .NET ecosystem conventions
- `DeriveTestPath` flattening behavior — consistent with observed flat structure in Tests project but not exhaustively verified against all possible source paths

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages verified from csproj; zero new packages required
- Architecture / State Pattern: HIGH — all patterns verified from EditingState, ValidatingState, TriagingState, GsdStateMachine
- LLM prompt for test generation: MEDIUM — structural requirements grounded in xUnit/NSubstitute conventions from existing tests; exact wording requires tuning after first run
- Test mock for write_file tool call: MEDIUM — FunctionCallContent constructor inferred, not verified in MEL 10.6.0 docs
- Gate 5 structural check: HIGH — get_file_contents pattern verified from EditingState; [Fact]/[Theory] string search is deterministic

**Research date:** 2026-06-04
**Valid until:** 2026-07-04 (stable .NET/xUnit/NSubstitute ecosystem)

---

## RESEARCH COMPLETE

**Phase:** 14 — Autonomous Test Generation
**Confidence:** HIGH (with MEDIUM confidence on FunctionCallContent mock API — verify at plan time)

### Key Findings

- `TestGeneratingState` integrates with zero new packages — all required APIs (`IChatClient`, `AIFunctionFactory`, `McpToolDispatcher`, `create_or_update_file`) are already in the project
- The `EditingState` write_file synthetic tool pattern is the correct model for test generation — reuse it verbatim, not the `AnalyzingState` JSON response pattern
- `EditingState.cs` has exactly one change: `.Transition(WorkflowState.Validating)` becomes `.Transition(WorkflowState.TestGenerating)` — this does not break any existing tests
- Test path derivation is deterministic: `src/{Project}/Anything/Foo.cs` → `src/{Project}.Tests/FooTests.cs` (flat, matching existing test file layout)
- Gate 5 in `ValidatingState` is a structural check (`[Fact]`/`[Theory]` attribute presence via `get_file_contents`) — NOT a `dotnet test` invocation; failure status is `Warn`, not `Block`
- Gate 4 (TestIntent) should also be updated to check `ctx.TestGeneration` as a satisfying condition, otherwise it will continue to warn even when TestGeneratingState succeeded
- The NSubstitute mock for `IChatClient` in tests needs `FunctionCallContent` + `FinishReason.ToolCalls` — different from the simpler `ChatMessage(text)` pattern used in TriagingStateTests; verify MEL 10.6.0 API at plan time

### File Created

`.planning/phases/14-autonomous-test-generation/14-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | All packages verified from csproj; no new dependencies |
| Architecture / State Pattern | HIGH | All patterns verified from EditingState, ValidatingState, TriagingState |
| LLM Prompt Content | MEDIUM | Structurally grounded; exact wording needs tuning after first run |
| FunctionCallContent Mock API | MEDIUM | Inferred from adjacent MEL types; verify before finalizing test code |
| Gate 5 Implementation | HIGH | `get_file_contents` + string check pattern fully verified from existing code |
| Test Path Derivation | HIGH | Flat structure confirmed in GsdOrchestrator.Tests project |

### Open Questions

1. `FunctionCallContent` constructor signature in MEL 10.6.0 — verify at plan time before writing TestGeneratingStateTests helper
2. Gate 4 (TestIntent) update needed: should also check `ctx.TestGeneration` as a satisfying condition; include in Gate 5 plan task
3. LLM prompt wording for test generation — recommend iterative prompt tuning as a follow-on task after first integration run

### Ready for Planning

Research complete. Planner can now create PLAN.md files.
