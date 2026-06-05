---
phase: 14-autonomous-test-generation
plan: "02"
subsystem: test-generation
tags: [tdd, green-phase, test-generating-state, validating-state, editing-state, xunit]
dependency_graph:
  requires:
    - "14-01"
  provides:
    - Full TestGeneratingState implementation (write_file LLM loop)
    - EditingState → TestGenerating transition
    - ValidatingState Gate 5 (TestCompilation)
    - ValidatingState Gate 4 updated (hasGeneratedTests OR condition)
    - Program.cs DI registration of TestGeneratingState
    - All 7 TestGeneratingStateTests GREEN
  affects:
    - src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs
    - src/GsdOrchestrator/Workflows/States/EditingState.cs
    - src/GsdOrchestrator/Workflows/States/ValidatingState.cs
    - src/GsdOrchestrator/Program.cs
    - src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs
tech_stack:
  added: []
  patterns:
    - TDD GREEN phase — 7 RED stubs now GREEN
    - LLM ReAct write_file loop pattern (mirrors EditingState exactly)
    - IsTestableSourceFile / DeriveTestPath static helpers
    - AIFunctionFactory.Create synthetic tool for write_file
    - Gate 5 structural test validation (file existence + [Fact]/[Theory] attribute check)
key_files:
  created: []
  modified:
    - src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs
    - src/GsdOrchestrator/Workflows/States/EditingState.cs
    - src/GsdOrchestrator/Workflows/States/ValidatingState.cs
    - src/GsdOrchestrator/Program.cs
    - src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs
decisions:
  - "DeriveTestPath kept static; warning logged at call site in ExecuteAsync (not inside static method)"
  - "BuildLlmWithToolCall mock fixed to return toolCallResponse on all calls (not just first) — correct for multi-file scenarios since the while loop exits after finalContent is set"
  - "Gate 5 uses Warn (not Block) severity — structural test validation is advisory; LLM-generated content may vary"
metrics:
  duration: "15m"
  completed_date: "2026-06-04"
  tasks_completed: 2
  files_modified: 5
---

# Phase 14 Plan 02: TestGenerating GREEN Phase Summary

Full TestGeneratingState implementation replacing the Wave 1 stub — LLM ReAct write_file loop generates xUnit tests for each edited source file, commits via create_or_update_file, and transitions to Validating; Gate 5 (TestCompilation) added to ValidatingState; all 21 tests GREEN.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Implement TestGeneratingState.cs (replace stub with full write_file loop) | b917b4d | TestGeneratingState.cs |
| 2 | Wire EditingState + ValidatingState + Program.cs; all 7 tests GREEN | a4109cc | EditingState.cs, ValidatingState.cs, Program.cs, TestGeneratingStateTests.cs |

## Verification Results

- `dotnet build src/GsdOrchestrator/GsdOrchestrator.csproj` — Build succeeded
- `dotnet build src/GsdOrchestrator.Tests/` — Build succeeded
- `dotnet test --filter "FullyQualifiedName~TestGenerating"` — Passed: 7, Failed: 0 (GREEN confirmed)
- `dotnet test src/GsdOrchestrator.Tests/` — Passed: 21, Failed: 0 (all GREEN)
- `grep "WorkflowState.TestGenerating" EditingState.cs` — match found
- `grep "TestCompilation" ValidatingState.cs` — match found
- `grep "TestGeneratingState" Program.cs` — match found
- `grep "using System.Text;" ValidatingState.cs` — match found

## Success Criteria Check

- [x] TestGeneratingState.cs fully implements the write_file LLM loop (not a stub)
- [x] EditingState transitions to TestGenerating instead of Validating
- [x] ValidatingState has Gate 5 (TestCompilation) with Warn severity
- [x] Gate 4 (TestIntent) checks ctx.TestGeneration as a satisfying condition
- [x] Program.cs has AddSingleton for TestGeneratingState between EditingState and ValidatingState
- [x] All 21 tests green: 7 GsdStateMachineTests + 7 TriagingStateTests + 7 TestGeneratingStateTests
- [x] TESTGEN-01: TestGeneratingState implemented and tested
- [x] TESTGEN-02: create_or_update_file called with derived test path on each source file
- [x] TESTGEN-03: Gate 5 in ValidatingState checks test file existence + [Fact]/[Theory] attribute

## TDD Gate Compliance

- RED gate: `test(14-01)` commit 3a21a31 (Wave 1) — 7 failing test stubs
- GREEN gate: `feat(14-02)` commit b917b4d (Wave 2) — full implementation makes 7 tests pass

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed BuildLlmWithToolCall mock for multi-file test scenario**
- **Found during:** Task 2 test run (Test 7: ExecuteAsync_WithMultipleEditableFiles_GeneratesTestForEach)
- **Issue:** `BuildLlmWithToolCall()` used `.Returns(toolCallResponse, stopResponse)` — NSubstitute returns stopResponse for all calls after the first. In a 2-file scenario, file 2's LLM call returns stopResponse (no write_file), producing WasSkipped=true. `create_or_update_file` was called only once, but Test 7 asserts twice.
- **Fix:** Changed mock to `.Returns(Task.FromResult(toolCallResponse))` (always). The while loop exits immediately after write_file content is captured (finalContent != null), so repeated toolCallResponse is correct and safe for any number of files.
- **Files modified:** src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs
- **Commit:** a4109cc

**2. [Rule 2 - Minor] DeriveTestPath static method — LogWarning at call site**
- **Found during:** Task 1 implementation review
- **Issue:** Plan initially included `_logger.LogWarning` inside the static DeriveTestPath method — not possible for a static method.
- **Fix:** Plan itself noted the preferred fix (log at call site in ExecuteAsync). Implemented exactly: DeriveTestPath kept static, warning logged in the foreach loop when `!sourcePath.StartsWith("src/")`.
- **Files modified:** src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs
- **Commit:** b917b4d

## Known Stubs

None — all production code is fully implemented. The Wave 1 `NotImplementedException` stub has been completely replaced.

## Threat Surface Scan

| Flag | File | Description |
|------|------|-------------|
| threat_flag: content-logging | TestGeneratingState.cs | T-14-05 confirmed mitigated: ReadFileAsync and TryReadFileWithShaAsync log path only (not content). File content passes to LLM prompt but never to structured logs. |

T-14-06 (DoS via runaway LLM) confirmed mitigated: MaxTurnsPerFile=20 constant, WasSkipped=true on no content within limit.

## Self-Check: PASSED

- [x] src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs — modified, committed b917b4d
- [x] src/GsdOrchestrator/Workflows/States/EditingState.cs — modified, committed a4109cc
- [x] src/GsdOrchestrator/Workflows/States/ValidatingState.cs — modified, committed a4109cc
- [x] src/GsdOrchestrator/Program.cs — modified, committed a4109cc
- [x] src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs — modified, committed a4109cc
- [x] b917b4d exists in git log
- [x] a4109cc exists in git log
- [x] All 21 tests GREEN (verified via dotnet test)
