---
phase: 12-robustness-foundation
phase_number: 12
generated: "2026-05-29"
mode: auto
---

# Phase 12: Robustness Foundation - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Add Serilog structured logging to all state transitions, create a GsdOrchestrator.Tests xUnit project with >= 20% coverage on GsdStateMachine and McpStdioClient, and extend the Polly resilience pipeline with a circuit breaker for MCP tool calls. No new workflow states. No behavioral changes to existing --issue / --resume / --watch modes.

</domain>

<decisions>
## Implementation Decisions

### Logging (ROB-01)

- **D-01:** Serilog with Console sink only, JSON formatter. No rolling file sink — container-friendly output, no file management overhead. File sink deferred to later phase if needed.
- **D-02:** Log all state entry/exit transitions at `Information` level. Claude API calls (model, token counts) at `Debug`. MCP tool calls (tool name, args, result status) at `Debug`. Errors at `Error` with full exception. Structured fields: `WorkflowId`, `IssueNumber`, `StateName`, `DurationMs`.
- **D-03:** Inject `ILogger<T>` via existing DI container (Microsoft.Extensions.DI already wired). No global logger anti-pattern.

### Unit Tests (ROB-02)

- **D-04:** New `GsdOrchestrator.Tests` xUnit project in the solution. Target >= 20% coverage on `GsdStateMachine` (state dispatch, checkpoint load/save, error paths) and `McpStdioClient` (request serialization, response parsing, timeout handling).
- **D-05:** Mock `IMcpClient` with NSubstitute. Unit tests only — no external MCP process, no real GitHub API calls. Fast and deterministic.
- **D-06:** Individual state implementations (IdleState, AnalyzingState, etc.) are NOT in scope for this phase — they will be covered incrementally as new states are added in Phases 13-16.

### Circuit Breaker (ROB-03)

- **D-07:** Single Polly `ResiliencePipeline` — circuit breaker wraps the existing retry policy (retry fires inside the breaker). Consistent with existing Polly usage in `McpToolDispatcher`.
- **D-08:** Circuit breaker thresholds: open after 5 consecutive failures within 60 seconds, half-open with 1 probe attempt, reset after 30 seconds in half-open. These are reasonable defaults — planner has discretion to adjust based on Polly v8 API.
- **D-09:** When circuit is open, throw a `McpException` with a clear message ("MCP circuit breaker open — too many consecutive failures"). State machine catches this and fails the workflow with a comment on the issue.

### Scope Boundary

- **D-10:** GSD_REPOS multi-repo config is NOT introduced in this phase. Phase 12 is observability and resilience only. Config refactor deferred to Phase 16.
- **D-11:** No changes to existing `--issue`, `--resume`, `--watch` operating modes beyond adding log statements.

### Claude's Discretion

- Exact Serilog package versions and NuGet sources — use latest stable.
- NSubstitute vs Moq for mocking — either is fine; prefer NSubstitute for cleaner API.
- xUnit test naming convention — use standard `MethodName_Scenario_ExpectedResult` pattern.
- Polly v8 ResiliencePipeline builder syntax — planner should follow Polly v8 docs (breaking changes from v7).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Requirements
- `.planning/REQUIREMENTS.md` §"Robustness Foundation (ROB)" — ROB-01, ROB-02, ROB-03 definitions
- `.planning/ROADMAP.md` §"Phase 12 — Robustness Foundation" — success criteria

### Existing Codebase (remote — read via gh api or gh repo clone)
- `GsdOrchestrator/Program.cs` — DI container setup; Serilog must be registered here
- `GsdOrchestrator/Mcp/McpToolDispatcher.cs` — existing Polly retry policy; circuit breaker wraps this
- `GsdOrchestrator/Mcp/IMcpClient.cs` — interface to mock in unit tests
- `GsdOrchestrator/Workflows/GsdStateMachine.cs` — primary unit test target
- `GsdOrchestrator/GsdOrchestrator.csproj` — current NuGet packages; add Serilog + NSubstitute + xUnit here

### No external ADRs
No external specs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IMcpClient` interface: Clean abstraction — NSubstitute can mock it directly without any changes. Tests call `Received()` to verify tool call args.
- `McpException`: Already exists — circuit breaker open state should throw this type for consistent error handling in the state machine.
- `GsdWorkflowContext` record: Immutable, serializable — checkpoint tests can round-trip it via `FileCheckpointStore` with a temp directory.

### Established Patterns
- **DI via `Microsoft.Extensions.DependencyInjection`**: All services registered in `Program.cs`. Serilog registers as `ILogger<T>` via `AddSerilog()` — no changes to state constructors needed beyond adding logger parameter.
- **Polly in `McpToolDispatcher`**: Existing `ResiliencePipeline` with retry. Polly v8 pipeline builder — circuit breaker added as an outer strategy wrapping the retry.
- **`IWorkflowState` interface**: Each state has `ExecuteAsync(ctx, ct)`. Log entry/exit by wrapping state execution in `GsdStateMachine.RunAsync` — single instrumentation point, no changes to individual states.

### Integration Points
- Serilog hooks into `Program.cs` → flows through `ILogger<T>` to all services via DI
- Circuit breaker added in `McpToolDispatcher` constructor (already owns the Polly pipeline)
- xUnit test project references `GsdOrchestrator` project directly — no new abstractions needed for testability

</code_context>

<specifics>
## Specific Ideas

No specific UI or behavior references. Standard enterprise observability patterns apply.

- Log format should be machine-parseable JSON for future log aggregation (Seq, ELK, Azure Monitor)
- Test project: `GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` in solution root sibling to `GsdOrchestrator/`

</specifics>

<deferred>
## Deferred Ideas

- File/rolling log sink — can be added when deployment target is known (Phase 16 or later)
- Integration tests with real MCP binary — deferred; requires MCP binary in test environment
- Coverage gate in CI (e.g., fail if coverage drops below 20%) — deferred to Phase 12 follow-up or Phase 14
- Individual state unit tests — deferred; states get coverage as they are added in Phases 13-16
- GSD_REPOS multi-repo config — Phase 16

</deferred>

---

*Phase: 12-robustness-foundation*
*Context gathered: 2026-05-29*
