---
phase: 14-autonomous-test-generation
plan: "01"
subsystem: test-generation
tags: [tdd, red-phase, workflow-models, test-stubs, xunit]
dependency_graph:
  requires: []
  provides:
    - WorkflowState.TestGenerating enum value
    - GeneratedTest record
    - TestGenerationContext record
    - GsdWorkflowContext.TestGeneration property
    - TestGeneratingState stub class
    - 7 RED test stubs in TestGeneratingStateTests.cs
  affects:
    - src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs
    - src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs
    - src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs
tech_stack:
  added: []
  patterns:
    - TDD RED phase — test stubs compile, fail with NotImplementedException
    - NSubstitute mock pattern reused from TriagingStateTests
    - FunctionCallContent 3-arg constructor (MEL 10.6.0 verified)
key_files:
  created:
    - src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs
    - src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs
  modified:
    - src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs
decisions:
  - "BuildMcpClient testFileExists=true uses Arg.Is path-based matching (not sequential returns) — more explicit and maintainable for the test-file vs source-file distinction"
metrics:
  duration: "4m"
  completed_date: "2026-06-04"
  tasks_completed: 2
  files_modified: 3
---

# Phase 14 Plan 01: TestGenerating RED Phase Summary

TDD RED phase establishing the test contract for TestGeneratingState — WorkflowModels extended with TestGenerating enum value, GeneratedTest/TestGenerationContext records, and 7 failing test stubs defining the exact behavior Wave 2 must implement.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Extend WorkflowModels.cs with TestGenerating enum value and data records | cb73bf6 | WorkflowModels.cs |
| 2 | Write 7 RED test stubs in TestGeneratingStateTests.cs (with minimal stub class) | 3a21a31 | TestGeneratingState.cs, TestGeneratingStateTests.cs |

## Verification Results

- `dotnet build src/GsdOrchestrator/GsdOrchestrator.csproj` — Build succeeded
- `dotnet build src/GsdOrchestrator.Tests/` — Build succeeded
- `dotnet test --filter "FullyQualifiedName~TestGenerating"` — Failed: 7, Passed: 0 (RED confirmed)
- `dotnet test --filter "FullyQualifiedName~Triaging"` — Passed: 7, Failed: 0 (existing tests GREEN)

## Success Criteria Check

- [x] WorkflowState.TestGenerating exists in the enum between Editing and Validating
- [x] GeneratedTest and TestGenerationContext records exist and compile
- [x] GsdWorkflowContext.TestGeneration property exists (nullable)
- [x] TestGeneratingState.cs stub exists with correct constructor signature
- [x] TestGeneratingStateTests.cs has 7 [Fact] methods that compile but fail at runtime
- [x] Existing TriagingStateTests (7 tests) remain GREEN

## TDD Gate Compliance

- RED gate: `test(14-01)` commit 3a21a31 exists — 7 failing tests
- GREEN gate: pending (Wave 2 / Plan 14-02)

## Deviations from Plan

### Minor Discrepancies

**1. [Rule 1 - Deviation] BuildMcpClient testFileExists path-based matching**
- **Found during:** Task 2 implementation
- **Issue:** Plan suggested using sequential Returns for get_file_contents; however, sequential returns on NSubstitute non-path-specific matchers would require exact call ordering which is fragile across per-file loop iterations.
- **Fix:** Used `Arg.Is<JsonObject>(j => j["path"]!.GetValue<string>().Contains(".Tests"))` to distinguish source from test file reads — more explicit and correct.
- **Files modified:** TestGeneratingStateTests.cs

**2. [No action needed] grep -c "TestGenerating" criterion**
- **Note:** Plan stated `grep -c "TestGenerating" >= 2` (enum value + comment). The comment on the enum line reads "generate xUnit tests" not "TestGenerating". The implementation is semantically correct — enum value exists at correct position and dotnet build succeeds. The criterion discrepancy is in the plan's grep pattern, not the implementation.

## Known Stubs

| File | Pattern | Reason |
|------|---------|--------|
| src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs | `throw new NotImplementedException("Wave 2 implementation pending")` | Intentional RED phase stub — Wave 2 (Plan 14-02) implements full behavior |

## Threat Surface Scan

No new security-relevant surface introduced. WorkflowModels.cs additions are pure data records with no network endpoints, auth paths, or file access patterns.

## Self-Check: PASSED

- [x] src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs — modified and committed (cb73bf6)
- [x] src/GsdOrchestrator/Workflows/States/TestGeneratingState.cs — created and committed (3a21a31)
- [x] src/GsdOrchestrator.Tests/TestGeneratingStateTests.cs — created and committed (3a21a31)
- [x] Both commits exist in git log
