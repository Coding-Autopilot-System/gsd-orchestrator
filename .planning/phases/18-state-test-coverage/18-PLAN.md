# Phase 18 — State Test Coverage + Checkpoint Hardening

## Summary

Phase 18 adds xUnit test coverage for 6 previously-untested workflow states and hardens the
checkpoint store with schema version validation.

## What Was Done

### Plan 18-01: Wave 1 — AnalyzingStateTests, BranchingStateTests, EditingStateTests

Created `src/GsdOrchestrator.Tests/States/` directory with 3 new test classes:

| File | Tests |
|---|---|
| `States/AnalyzingStateTests.cs` | 5 tests |
| `States/BranchingStateTests.cs` | 5 tests |
| `States/EditingStateTests.cs` | 5 tests |

Each class follows the NSubstitute pattern from `TriagingStateTests.cs`:
- `McpToolDispatcher` built with a pass-through Polly registry
- `NullLogger<T>` for all logging dependencies
- `IMcpClient` mocked via `Substitute.For<IMcpClient>()`
- `IChatClient` mocked for states that use LLM

Key test patterns per state:

**AnalyzingState** — LLM returns valid `AnalysisPlan` JSON → transitions to `Branching`; all-bad-JSON → throws `InvalidOperationException`; `search_code` failure is swallowed (best-effort); cancellation via LLM mock.

**BranchingState** — new branch created → `WasResumed=false`; existing branch detected → `WasResumed=true`, `create_branch` not called; MCP failure on `get_branch` propagates; cancellation via MCP mock.

**EditingState** — LLM calls `write_file` → `create_or_update_file` called, `EditContext` populated, transitions to `TestGenerating`; LLM never calls `write_file` → no commit, still transitions; cancellation via MCP mock.

### Plan 18-02: Wave 2 — CommittingStateTests, DocumentingStateTests, PrCreatingStateTests

Created 3 more test classes in the same directory:

| File | Tests |
|---|---|
| `States/CommittingStateTests.cs` | 6 tests |
| `States/DocumentingStateTests.cs` | 6 tests |
| `States/PrCreatingStateTests.cs` | 6 tests |

Key test patterns per state:

**CommittingState** — `get_branch` SHA captured in `CommitContext`; commit URL formatted correctly; `get_branch` called with correct branch name; MCP failure propagates; cancellation via MCP mock.

**DocumentingState** — `DocumentingState` requires `IConfiguration` (mocked for `GSD_AUTO_MERGE`); both `docs/github-mcp-tools.md` and `CHANGELOG.md` are committed; `merge_pull_request` called only when auto-merge enabled; existing CHANGELOG reads existing SHA.

**PrCreatingState** — no existing PR → `create_pull_request` called, PR number/URL stored; existing PR found → `create_pull_request` NOT called, existing PR number used; MCP failure propagates; cancellation via MCP mock.

### Plan 18-03: Checkpoint Schema Versioning

**`WorkflowModels.cs`** — Added `SchemaVersion` string property to `GsdWorkflowContext` with default `"1.0"`. This serializes automatically to/from checkpoint JSON.

**`FileCheckpointStore.cs`** — Added `ValidateSchemaVersion` private method called after deserialization in `LoadAsync`. On mismatch, logs warning and returns `null` (triggering fresh start). Added `CurrentSchemaVersion = "1.0"` constant.

**`States/CheckpointStoreTests.cs`** — 4 tests:
- `LoadAsync_CompatibleSchemaVersion_ReturnsContext` — writes raw JSON with `schemaVersion: "1.0"`, verifies load succeeds
- `LoadAsync_IncompatibleSchemaVersion_ReturnsNull` — writes raw JSON with `schemaVersion: "2.0"`, verifies null returned
- `SaveAsync_ThenLoadAsync_RoundTripsContext` — save + load round trip
- `LoadAsync_MissingWorkflow_ReturnsNull` — missing file returns null

## Test Count

| Baseline (before Phase 18) | After Phase 18 |
|---|---|
| 36 tests | 73 tests |

All 73 tests pass in Release configuration.

## Verification

```
dotnet test src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj --configuration Release
```

Result: `Passed! - Failed: 0, Passed: 73, Skipped: 0, Total: 73`
