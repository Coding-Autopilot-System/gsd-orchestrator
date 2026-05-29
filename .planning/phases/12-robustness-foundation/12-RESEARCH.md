# Phase 12: Robustness Foundation - Research

**Researched:** 2026-05-30
**Domain:** C#/.NET 10 — Serilog structured logging, xUnit + NSubstitute, Polly v8 circuit breaker
**Confidence:** HIGH

## Summary

Phase 12 adds three orthogonal robustness layers to the existing `gsd-orchestrator` Worker service:
structured JSON logging via Serilog, a unit test project with >= 20% coverage via xUnit + NSubstitute,
and a Polly v8 circuit breaker wrapping the existing retry pipeline in `McpToolDispatcher`.

All three features integrate cleanly with the current codebase. The DI container (`Program.cs`) already
owns the logging and Polly pipeline registrations — Serilog replaces the existing `AddSimpleConsole`
call and the circuit breaker extends the existing `AddResiliencePipeline("mcp-tools", ...)` block.
`McpStdioClient` and `GsdStateMachine` are the primary unit test targets; both are deterministic and
inject their dependencies cleanly via constructor injection, making them straightforward to test with
NSubstitute mocks of `IMcpClient` and `ICheckpointStore`.

**Important Polly v8 constraint:** Polly v8 does NOT have a consecutive-failure circuit breaker.
The only circuit breaker type is ratio-based (`FailureRatio` over a `SamplingDuration` window with a
`MinimumThroughput` floor). Decision D-08 specifies "5 consecutive failures within 60 seconds" —
this must be re-expressed as a ratio-based config: MinimumThroughput=5, SamplingDuration=60s,
FailureRatio=1.0 (100% failures triggers break). This is documented as a known v7→v8 migration change.

**Primary recommendation:** Add Serilog in Program.cs using `UseSerilog` + `CompactJsonFormatter`,
extend the existing `AddResiliencePipeline` with `AddCircuitBreaker` catching `BrokenCircuitException`
and rethrowing as `McpException`, and place the test project at `src/GsdOrchestrator.Tests/`.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Logging (ROB-01)**
- D-01: Serilog with Console sink only, JSON formatter. No rolling file sink — container-friendly output.
- D-02: Log state entry/exit at `Information`. Claude API calls (model, token counts) at `Debug`. MCP tool calls (tool name, args, result status) at `Debug`. Errors at `Error` with full exception. Structured fields: `WorkflowId`, `IssueNumber`, `StateName`, `DurationMs`.
- D-03: Inject `ILogger<T>` via existing DI container. No global logger anti-pattern.

**Unit Tests (ROB-02)**
- D-04: New `GsdOrchestrator.Tests` xUnit project. Target >= 20% coverage on `GsdStateMachine` and `McpStdioClient`.
- D-05: Mock `IMcpClient` with NSubstitute. Unit tests only — no external MCP process, no real GitHub API calls.
- D-06: Individual state implementations NOT in scope — covered incrementally in Phases 13-16.

**Circuit Breaker (ROB-03)**
- D-07: Single Polly `ResiliencePipeline` — circuit breaker wraps existing retry (retry fires inside breaker).
- D-08: Thresholds: open after 5 consecutive failures within 60 seconds, half-open with 1 probe, reset after 30s in half-open. Planner has discretion to adjust for Polly v8 API (see research note above).
- D-09: When circuit open, throw `McpException` with message "MCP circuit breaker open — too many consecutive failures".

**Scope Boundary**
- D-10: GSD_REPOS multi-repo config NOT in this phase.
- D-11: No changes to `--issue`, `--resume`, `--watch` modes beyond adding log statements.

### Claude's Discretion
- Exact Serilog package versions and NuGet sources — use latest stable.
- NSubstitute vs Moq — prefer NSubstitute (cleaner API).
- xUnit test naming convention — `MethodName_Scenario_ExpectedResult`.
- Polly v8 ResiliencePipeline builder syntax — follow Polly v8 docs (breaking changes from v7).

### Deferred Ideas (OUT OF SCOPE)
- File/rolling log sink
- Integration tests with real MCP binary
- Coverage gate in CI
- Individual state unit tests
- GSD_REPOS multi-repo config
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ROB-01 | Serilog structured logging integrated — all state transitions, errors, and Claude calls emit structured log events | Serilog.Extensions.Hosting + Serilog.Sinks.Console + CompactJsonFormatter; replaces `AddSimpleConsole` in Program.cs; `GsdStateMachine.ExecuteLoopAsync` is the single instrumentation point for state transitions |
| ROB-02 | xUnit test project added with >= 20% coverage on GsdStateMachine and McpStdioClient | New project at `src/GsdOrchestrator.Tests/`; 6 GsdStateMachine methods + 3 McpStdioClient methods identified as testable without process spawning; NSubstitute mocks IMcpClient and ICheckpointStore |
| ROB-03 | Polly circuit breaker added for MCP tool calls (complements existing retry policy) | AddCircuitBreaker added to existing `AddResiliencePipeline("mcp-tools", ...)` in Program.cs; catch BrokenCircuitException in McpToolDispatcher.CallAsync, rethrow as McpException |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Structured logging (ROB-01) | Application / Worker | — | ILogger<T> flows through DI; Serilog registered at host level in Program.cs |
| Unit test scaffold (ROB-02) | Test project | Main project (under test) | Separate csproj references main; no external services needed |
| Circuit breaker (ROB-03) | McpToolDispatcher (MCP client layer) | Program.cs (pipeline registration) | Dispatcher already owns the ResiliencePipeline; open-circuit detection belongs in CallAsync |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Serilog.Extensions.Hosting | 10.0.0 | Integrates Serilog with IHostBuilder via `UseSerilog()` | Official Serilog hosting integration for Worker services [VERIFIED: nuget.org] |
| Serilog.Sinks.Console | 6.1.1 | Writes log events to console | Official console sink; supports custom formatters [VERIFIED: nuget.org] |
| Serilog.Formatting.Compact | 3.0.0 | `CompactJsonFormatter` — machine-parseable JSON per line | Purpose-built for structured JSON log output; compact byte count [VERIFIED: nuget.org] |
| xunit | 2.9.3 | Unit test framework | Industry standard for .NET; works with dotnet test [VERIFIED: nuget.org] |
| xunit.runner.visualstudio | 3.1.5 | VS test runner adapter | Required for `dotnet test` discovery [VERIFIED: nuget.org] |
| Microsoft.NET.Test.Sdk | 18.6.0 | MSBuild test targets | Required scaffold for any dotnet test project [VERIFIED: nuget.org] |
| NSubstitute | 5.3.0 | Mock generation for IMcpClient, ICheckpointStore | Cleaner API than Moq; no Setup() boilerplate; preferred per D-05 [VERIFIED: nuget.org] |
| coverlet.collector | 10.0.1 | Code coverage collection for `dotnet test` | Standard collector for XPlat Code Coverage [VERIFIED: nuget.org] |

**Note on NSubstitute 6.0.0-rc.1:** A release candidate exists but the latest stable is 5.3.0. Use 5.3.0. [VERIFIED: nuget.org]

### Supporting (already in project — no addition needed)

| Library | Version | Purpose |
|---------|---------|---------|
| Polly.Extensions | 8.6.6 | AddResiliencePipeline, ResiliencePipelineProvider — already registered [VERIFIED: GsdOrchestrator.csproj] |
| Microsoft.Extensions.Hosting | 10.0.7 | IHostBuilder, Worker service scaffold [VERIFIED: GsdOrchestrator.csproj] |

**Installation (new packages only):**
```bash
# Main project — Serilog
dotnet add src/GsdOrchestrator/GsdOrchestrator.csproj package Serilog.Extensions.Hosting --version 10.0.0
dotnet add src/GsdOrchestrator/GsdOrchestrator.csproj package Serilog.Sinks.Console --version 6.1.1
dotnet add src/GsdOrchestrator/GsdOrchestrator.csproj package Serilog.Formatting.Compact --version 3.0.0

# Test project — create first, then add packages
dotnet new xunit -n GsdOrchestrator.Tests -o src/GsdOrchestrator.Tests --framework net10.0
dotnet sln GithubMCP.slnx add src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj
dotnet add src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj reference src/GsdOrchestrator/GsdOrchestrator.csproj
dotnet add src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj package NSubstitute --version 5.3.0
dotnet add src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj package coverlet.collector --version 10.0.1
# xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk come with dotnet new xunit template
```

---

## Architecture Patterns

### System Architecture Diagram

```
Program.cs (IHostBuilder)
    │
    ├── UseSerilog(CompactJsonFormatter) ──► stdout (JSON lines)
    │       flows through ILogger<T> DI to all services
    │
    └── AddResiliencePipeline("mcp-tools")
            │
            ├── AddCircuitBreaker(options)   ← NEW (outer — trips first)
            │       │  open? → BrokenCircuitException
            │       │          caught in McpToolDispatcher.CallAsync
            │       │          rethrown as McpException("circuit open")
            │       │
            └── AddRetry(options)            ← EXISTING (inner — fires first)
                        │
                        └── McpToolDispatcher.CallAsync
                                    │
                                    └── IMcpClient.CallToolAsync
                                                │
                                            MCP stdio process
```

### Recommended Project Structure

```
src/
├── GsdOrchestrator/
│   ├── GsdOrchestrator.csproj         # +Serilog packages
│   ├── Program.cs                     # UseSerilog + extend AddResiliencePipeline
│   ├── Mcp/
│   │   ├── McpToolDispatcher.cs       # catch BrokenCircuitException → McpException
│   │   └── ...
│   └── Workflows/
│       └── GsdStateMachine.cs         # add entry/exit log with DurationMs
└── GsdOrchestrator.Tests/
    ├── GsdOrchestrator.Tests.csproj
    ├── GsdStateMachineTests.cs
    └── McpStdioClientTests.cs
```

### Pattern 1: Serilog Registration in Worker Service

Replace the existing `builder.Logging.AddSimpleConsole(...)` block in `Program.cs`:

```csharp
// Source: https://github.com/serilog/serilog-extensions-hosting
using Serilog;
using Serilog.Formatting.Compact;

// BEFORE (remove this):
// builder.Logging.AddSimpleConsole(o => o.IncludeScopes = false);
// builder.Services.AddLogging(lb => lb.AddFilter("Microsoft", LogLevel.Warning));

// AFTER (replace with this, before builder.Build()):
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter()));
```

All existing `ILogger<T>` injection points continue to work unchanged — Serilog hooks into
`Microsoft.Extensions.Logging` as the provider. [VERIFIED: serilog/serilog-extensions-hosting docs]

### Pattern 2: Structured Log in GsdStateMachine.ExecuteLoopAsync

The existing loop already has `_logger.LogInformation`. Enhance with timing and structured fields:

```csharp
// Source: pattern derived from existing GsdStateMachine.cs + Serilog enrichment
var sw = System.Diagnostics.Stopwatch.StartNew();
try
{
    await _checkpoints.SaveAsync(ctx, ct);
    ctx = await stateHandler.ExecuteAsync(ctx, ct);
    sw.Stop();
    _logger.LogInformation(
        "State {StateName} completed in {DurationMs}ms. WorkflowId={WorkflowId} IssueNumber={IssueNumber} NextState={NextState}",
        previousState, sw.ElapsedMilliseconds, ctx.WorkflowId, ctx.Issue?.Number, ctx.CurrentState);
}
catch (Exception ex)
{
    sw.Stop();
    _logger.LogError(ex,
        "Workflow {WorkflowId} failed at state {StateName} after {DurationMs}ms. IssueNumber={IssueNumber}",
        ctx.WorkflowId, ctx.CurrentState, sw.ElapsedMilliseconds, ctx.Issue?.Number);
    // existing transition to Failed state
}
```

### Pattern 3: Polly v8 Circuit Breaker — Ratio-Based (CRITICAL v7→v8 Migration Note)

**Polly v8 has no consecutive-failure circuit breaker.** The old `AdvancedCircuitBreakerPolicy` with
consecutive counts is gone. The v8 equivalent expresses D-08's "5 failures within 60s" as:
- `MinimumThroughput = 5` (at least 5 calls must be made in the window before tripping)
- `SamplingDuration = TimeSpan.FromSeconds(60)`
- `FailureRatio = 1.0` (100% failure rate within the window trips the breaker)
- `BreakDuration = TimeSpan.FromSeconds(30)` (half-open probe period)

```csharp
// Source: https://www.pollydocs.org/strategies/circuit-breaker.html [VERIFIED]
// In Program.cs — extend existing AddResiliencePipeline:
builder.Services.AddResiliencePipeline("mcp-tools", pipelineBuilder => pipelineBuilder
    .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
    {
        FailureRatio = 1.0,
        SamplingDuration = TimeSpan.FromSeconds(60),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder()
            .Handle<McpException>(ex => ex.IsTransient && !ex.IsSecondaryRateLimit)
    })
    .AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(5),
        ShouldHandle = args =>
            args.Outcome.Exception is McpException { IsTransient: true, IsSecondaryRateLimit: false }
                ? ValueTask.FromResult(true)
                : ValueTask.FromResult(false)
    }));
```

**Order matters:** `AddCircuitBreaker` must come BEFORE `AddRetry` in the builder. Polly v8 executes
strategies in registration order — outer first. So the circuit breaker fires before the retry engine
attempts any retries when the circuit is open. [VERIFIED: pollydocs.org]

### Pattern 4: Catch BrokenCircuitException in McpToolDispatcher

```csharp
// Source: https://www.pollydocs.org/strategies/circuit-breaker.html
public async Task<McpToolResult> CallAsync(string tool, JsonObject args, CancellationToken ct = default)
{
    try
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            // ... existing implementation unchanged ...
        }, ct);
    }
    catch (Polly.CircuitBreaker.BrokenCircuitException ex)
    {
        throw new McpException(
            "MCP circuit breaker open — too many consecutive failures",
            isTransient: false);
    }
}
```

### Pattern 5: xUnit Test with NSubstitute

```csharp
// Source: NSubstitute 5.x + xUnit 2.9 conventions
using NSubstitute;
using Xunit;

public class GsdStateMachineTests
{
    private readonly ICheckpointStore _checkpoints = Substitute.For<ICheckpointStore>();
    private readonly McpToolDispatcher _mcp; // needs full mock chain — see pitfall below
    private readonly ILogger<GsdStateMachine> _logger = Substitute.For<ILogger<GsdStateMachine>>();
    private readonly IWorkflowState _idleState = Substitute.For<IWorkflowState>();

    [Fact]
    public async Task RunAsync_WhenIdleStateTransitionsToDone_ReturnsCompletedContext()
    {
        // Arrange
        _idleState.State.Returns(WorkflowState.Idle);
        _idleState.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => (ci.Arg<GsdWorkflowContext>() with { CurrentState = WorkflowState.Done }));
        _checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _checkpoints.ArchiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var ctx = await _sut.RunAsync("owner", "repo", 1, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowState.Done, ctx.CurrentState);
        await _checkpoints.Received().ArchiveAsync(ctx.WorkflowId, Arg.Any<CancellationToken>());
    }
}
```

### Anti-Patterns to Avoid

- **Serilog global logger (`Log.Logger = ...`):** Bypasses DI and breaks testability. Use `UseSerilog()` on `IHostBuilder` only.
- **AddCircuitBreaker after AddRetry:** Inverts the strategy order — the retry would fire first when the circuit is open, wasting 3 retry attempts before the breaker stops it.
- **Mocking `McpToolDispatcher` directly in state machine tests:** `McpToolDispatcher` is a concrete class with a `ResiliencePipeline` dependency chain. Mock `IMcpClient` instead and either construct a real (no-op) dispatcher or create a thin interface. See pitfall section.
- **Using `dotnet new xunit` without `--framework net10.0`:** Default template may target `net8.0` — must match main project's `net10.0`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON structured log output | Custom JsonTextWriter | `Serilog.Formatting.Compact.CompactJsonFormatter` | Handles escaping, nested objects, timestamps, log levels correctly |
| Circuit breaker state machine | Custom failure counter + timer | `Polly.CircuitBreaker.CircuitBreakerStrategyOptions` | Half-open probing, thread safety, and retry interaction are non-trivial |
| Test mock of IMcpClient | Fake class implementing IMcpClient | `NSubstitute.Substitute.For<IMcpClient>()` | Saves 50+ lines of manual fake; returns and verifies calls inline |
| Code coverage measurement | Manual line counting | `coverlet.collector` + `dotnet test --collect:"XPlat Code Coverage"` | Produces Cobertura XML; feeds into ReportGenerator or CI |

---

## Specific Methods in GsdStateMachine to Unit Test

From reading the actual `GsdStateMachine.cs` [VERIFIED: GitHub repo]:

| Method | Test Scenario | Type |
|--------|---------------|------|
| `RunAsync` | Happy path: single state transitions to Done | Unit |
| `RunAsync` | State throws exception → ctx transitions to Failed | Unit |
| `RunAsync` | No handler registered for state → InvalidOperationException | Unit |
| `ExecuteLoopAsync` (via RunAsync) | OperationCanceledException → checkpoint saved, rethrows | Unit |
| `ResumeAsync` | Checkpoint exists → resumes from saved state | Unit |
| `ResumeAsync` | No checkpoint → throws InvalidOperationException | Unit |
| `PostFailureCommentAsync` (via Failed path) | Failed state → MCP add_issue_comment called | Unit |

**Note on McpToolDispatcher testability:** `McpToolDispatcher` takes `ResiliencePipelineProvider<string>` in its constructor — cannot be easily mocked with NSubstitute without a real Polly registry. For state machine tests, mock `IMcpClient` at the `McpStdioClient` level and wire up a real `McpToolDispatcher` with a no-op pipeline, OR introduce an `IMcpToolDispatcher` interface. The CONTEXT.md notes that the test project references `GsdOrchestrator` directly — "no new abstractions needed for testability." The simplest approach: create a test-only `ResiliencePipelineRegistry` with a pass-through `"mcp-tools"` pipeline.

**McpStdioClient testable methods (without spawning a process):**

| Method | What to test | How |
|--------|-------------|-----|
| `CallToolAsync` response parsing | Given a JSON response, parses `McpToolResult` correctly | Not directly testable without process — test the parsing logic in isolation or via internal helpers |
| `InitializeAsync` timeout | Not testable without a process | Skip |

**Coverage note:** Given the process-spawning nature of `McpStdioClient`, reaching 20% coverage on it without integration tests is challenging. The planner should focus the majority of test effort on `GsdStateMachine` (fully mockable) and treat `McpStdioClient` coverage as a stretch goal unless private method extraction is done.

---

## Common Pitfalls

### Pitfall 1: McpToolDispatcher Cannot Be Easily NSubstituted

**What goes wrong:** `McpToolDispatcher` is a sealed concrete class with no interface. Tests for `GsdStateMachine` that try to mock it via NSubstitute will fail at construction time because `ResiliencePipelineProvider<string>` requires a real Polly registry.

**Why it happens:** The CONTEXT.md states "no new abstractions needed" but `GsdStateMachine` takes `McpToolDispatcher` directly (not via interface). Only `IMcpClient` has an interface.

**How to avoid:** In the test project, construct a real `McpToolDispatcher` with a minimal pass-through Polly pipeline:
```csharp
var registry = new ResiliencePipelineRegistry<string>();
registry.TryAddBuilder("mcp-tools", (b, _) => { /* no-op pipeline */ });
var dispatcher = new McpToolDispatcher(mockMcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
```
Use `Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance` — already available via `Microsoft.Extensions.Logging.Abstractions` (transitive dep).

### Pitfall 2: Polly v8 BrokenCircuitException Namespace

**What goes wrong:** Code catches `BrokenCircuitException` but it fails to compile because v7 was `Polly.CircuitBreaker.BrokenCircuitException` and some samples show just `BrokenCircuitException`.

**How to avoid:** In Polly v8 the type is `Polly.CircuitBreaker.BrokenCircuitException`. Add `using Polly.CircuitBreaker;` at the top of `McpToolDispatcher.cs`. [VERIFIED: pollydocs.org]

### Pitfall 3: Circuit Breaker Strategy Order in v8 Pipeline Builder

**What goes wrong:** Registering `AddRetry` before `AddCircuitBreaker` means the retry fires 3 times before the breaker sees the aggregated failure. The breaker only trips after 3x the expected failures.

**How to avoid:** Always register `AddCircuitBreaker` first (outermost) then `AddRetry` in the pipeline builder. [VERIFIED: pollydocs.org]

### Pitfall 4: sln vs slnx for `dotnet sln add`

**What goes wrong:** The solution file is `GithubMCP.slnx` (new XML Solution format) but `dotnet sln add` may not support `.slnx` in all SDK versions.

**How to avoid:** Check with `dotnet --version` first. If .NET SDK 10 is installed, `.slnx` is supported by `dotnet sln`. If not, add the project reference manually in the `.slnx` XML. [ASSUMED — verify with actual dotnet SDK on build machine]

### Pitfall 5: `dotnet new xunit` Default Target Framework

**What goes wrong:** `dotnet new xunit` defaults to `net8.0` — type resolution fails when the test project targets a different framework than the main project (`net10.0`).

**How to avoid:** Always pass `--framework net10.0` to `dotnet new xunit`.

---

## Code Examples

### Complete Program.cs Serilog Registration (drop-in replacement)

```csharp
// Source: serilog/serilog-extensions-hosting README + verified pattern
using Serilog;
using Serilog.Formatting.Compact;

// Replace existing logging block (lines ~37-38 in current Program.cs):
// builder.Logging.AddSimpleConsole(o => o.IncludeScopes = false);
// builder.Services.AddLogging(lb => lb.AddFilter("Microsoft", LogLevel.Warning));

// With:
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter()));
```

### Complete AddResiliencePipeline with Circuit Breaker

```csharp
// Source: https://www.pollydocs.org/strategies/circuit-breaker.html [VERIFIED]
using Polly.CircuitBreaker;

builder.Services.AddResiliencePipeline("mcp-tools", pipelineBuilder => pipelineBuilder
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        // "5 consecutive failures within 60s" expressed as ratio-based (Polly v8 only has ratio-based)
        FailureRatio = 1.0,               // 100% failures in window trips the breaker
        SamplingDuration = TimeSpan.FromSeconds(60),
        MinimumThroughput = 5,            // need at least 5 calls before tripping
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder()
            .Handle<McpException>(ex => ex.IsTransient && !ex.IsSecondaryRateLimit)
    })
    .AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(5),
        ShouldHandle = args =>
            args.Outcome.Exception is McpException { IsTransient: true, IsSecondaryRateLimit: false }
                ? ValueTask.FromResult(true)
                : ValueTask.FromResult(false)
    }));
```

### Test Project .csproj (verified package versions)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\GsdOrchestrator\GsdOrchestrator.csproj" />
  </ItemGroup>
</Project>
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `CircuitBreakerPolicy` (consecutive failures) | `CircuitBreakerStrategyOptions` (ratio-based only) | Polly v8.0 (2023) | D-08 thresholds must be re-expressed as ratio; no direct consecutive-count equivalent |
| `AddSimpleConsole` for logging | `Serilog.Extensions.Hosting` + `CompactJsonFormatter` | — | Enables machine-parseable JSON; structured fields queryable in log aggregators |
| No interface on McpToolDispatcher | Concrete class only | Current state | Unit tests need real Polly registry or thin wrapper; see pitfall |

**Deprecated/outdated:**
- `Polly.CircuitBreaker.CircuitBreakerPolicy.Handle<T>().CircuitBreaker(exceptionsBeforeBreaking, ...)`: v7 API, removed in v8.
- `new ResiliencePipeline...` as standalone (outside DI): use `AddResiliencePipeline` on IServiceCollection for DI-managed pipelines.
- `builder.Logging.AddSimpleConsole(...)`: Will be removed once Serilog is wired; do not keep both.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `dotnet sln add` supports `.slnx` in .NET SDK 10 on Windows | Standard Stack (install commands) | Commands fail; planner needs fallback to manual XML edit of `.slnx` |
| A2 | `GsdStateMachine` tests can use `NullLogger<T>.Instance` without adding a package | Pitfall 1 | Extra package needed; minor — `Microsoft.Extensions.Logging.Abstractions` is already a transitive dep |
| A3 | `McpStdioClient` internals are accessible from the test project (no `InternalsVisibleTo` needed) | Don't Hand-Roll | If `SendRequestAsync` is private (it is), direct unit tests of it are blocked; only public surface is testable without reflection |

---

## Open Questions

1. **McpToolDispatcher testability without an interface**
   - What we know: `GsdStateMachine` takes `McpToolDispatcher` as a concrete constructor parameter, not via interface.
   - What's unclear: Whether the planner should introduce `IMcpToolDispatcher` as a prerequisite, or use the pass-through Polly registry pattern.
   - Recommendation: Use the pass-through registry pattern (no new interface) per D-06/D-11 scope constraints. Document in plan that `IMcpToolDispatcher` is deferred to Phase 13.

2. **Coverage target achievability on McpStdioClient**
   - What we know: All public methods in `McpStdioClient` spawn an actual process (`InitializeAsync`) or depend on one being running.
   - What's unclear: Whether 20% combined coverage on `GsdStateMachine + McpStdioClient` is achievable by testing `GsdStateMachine` alone.
   - Recommendation: Calculate coverage on `GsdStateMachine` only first. With 7 test scenarios covering the main dispatch loop (~60 LOC), combined coverage should exceed 20% on the two-file target surface. The planner should note this in the plan.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10 | `dotnet new xunit --framework net10.0` | [ASSUMED: yes, CI already passes on net10.0] | 10.x | — |
| NuGet.org (https) | Package restore | [ASSUMED: yes] | — | — |
| `dotnet test` | Coverage collection | [ASSUMED: yes, ships with SDK] | — | — |

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none — Wave 0 creates `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` |
| Quick run command | `dotnet test src/GsdOrchestrator.Tests/ --no-build` |
| Full suite command | `dotnet test src/GsdOrchestrator.Tests/ --collect:"XPlat Code Coverage"` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ROB-01 | Serilog emits JSON lines to stdout | Smoke (dotnet build + run --issue) | `dotnet build src/GsdOrchestrator/` | ❌ Wave 0 |
| ROB-02 | >= 20% coverage on GsdStateMachine + McpStdioClient | Unit | `dotnet test src/GsdOrchestrator.Tests/ --collect:"XPlat Code Coverage"` | ❌ Wave 0 |
| ROB-03 | Circuit breaker registered; BrokenCircuitException → McpException | Unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "CircuitBreaker"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet build src/GsdOrchestrator/ --no-incremental`
- **Per wave merge:** `dotnet test src/GsdOrchestrator.Tests/ --collect:"XPlat Code Coverage"`
- **Phase gate:** `dotnet build` green + all xUnit tests pass + coverage report shows >= 20% on target classes before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` — test project does not yet exist
- [ ] `src/GsdOrchestrator.Tests/GsdStateMachineTests.cs` — covers ROB-02, ROB-03
- [ ] `src/GsdOrchestrator.Tests/McpStdioClientTests.cs` — covers ROB-02 (stretch)
- [ ] Framework install: `dotnet new xunit -n GsdOrchestrator.Tests -o src/GsdOrchestrator.Tests --framework net10.0`

---

## Security Domain

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | no | Log fields are internal structured values, not user input |
| V6 Cryptography | no | — |

**Known threat patterns for this stack:**

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Log injection (structured field injection) | Tampering | Serilog's `CompactJsonFormatter` serialises each property value as a JSON value — injected newlines or braces in string values are escaped. No additional action needed. [ASSUMED] |
| Sensitive data in logs | Information Disclosure | D-02 logs tool name and result status only — no PAT tokens, no issue body content at Information level. Ensure `ANTHROPIC_API_KEY` and `GITHUB_PERSONAL_ACCESS_TOKEN` are never passed to logger methods. |

---

## Sources

### Primary (HIGH confidence)
- NuGet registry (api.nuget.org) — all package versions verified directly
- `https://www.pollydocs.org/strategies/circuit-breaker.html` — AddCircuitBreaker API, BrokenCircuitException, strategy order
- GitHub repo `Coding-Autopilot-System/gsd-orchestrator` — GsdStateMachine.cs, McpToolDispatcher.cs, McpStdioClient.cs, IMcpClient.cs, McpException.cs, GsdOrchestrator.csproj, Program.cs — all read verbatim via `gh api`

### Secondary (MEDIUM confidence)
- `https://github.com/serilog/serilog-extensions-hosting` — UseSerilog() pattern for Worker services
- `https://github.com/serilog/serilog-formatting-compact` — CompactJsonFormatter
- Context7 CLI — Polly and Serilog library IDs resolved (CLI path issue prevented doc fetch; fallback to WebFetch/NuGet registry)

### Tertiary (LOW confidence)
- WebSearch results for Polly v8 migration and Serilog Worker service patterns — cross-verified with official docs above

---

## Metadata

**Confidence breakdown:**
- Standard stack (NuGet versions): HIGH — verified directly against nuget.org API
- Architecture (Serilog registration, circuit breaker order): HIGH — verified against official docs
- Test coverage achievability: MEDIUM — depends on final LOC count; calculated estimate only
- McpStdioClient testability: MEDIUM — constrained by process-spawning design

**Research date:** 2026-05-30
**Valid until:** 2026-06-30 (NuGet versions stable; Polly v8 API stable)
