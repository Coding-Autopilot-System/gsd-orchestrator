# Proof Points

This document maps the public claims in the repository README to code or tests in the repo. It is intentionally narrow: portfolio credibility is better served by traceable proof than by broad AI claims.

## Workflow capability

| Claim | Evidence |
| --- | --- |
| The workflow is state-machine driven | `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` |
| The implemented states include triage and test generation | `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` |
| The host registers Idle, Triaging, Analyzing, Branching, Editing, TestGenerating, Validating, Committing, PrCreating, Reviewing, and Documenting | `src/GsdOrchestrator/Program.cs` |
| Workflows can resume from checkpoints | `src/GsdOrchestrator/Workflows/GsdStateMachine.cs`, `src/GsdOrchestrator.Tests/GsdStateMachineTests.cs` |

## Operating modes

| Mode | Evidence |
| --- | --- |
| Issue workflow mode via `--issue <number>` | `src/GsdOrchestrator/Program.cs` |
| Triage-only mode via `--issue <number> --triage` | `src/GsdOrchestrator/Program.cs`, `src/GsdOrchestrator.Tests/TriagingStateTests.cs` |
| PR review mode via `--pr <number>` | `src/GsdOrchestrator/Program.cs`, `src/GsdOrchestrator.Tests/ReviewingStateTests.cs` |
| Watch mode across configured repos via `--watch` | `src/GsdOrchestrator/Program.cs`, `src/GsdOrchestrator/Workflows/Models/RepoConfigLoader.cs` |

## Enterprise-oriented control points

| Control point | Evidence |
| --- | --- |
| Retry and circuit-breaker protection around MCP tool calls | `src/GsdOrchestrator/Program.cs` |
| Structured logging through Serilog | `src/GsdOrchestrator/Program.cs` |
| Checkpoint files persisted under repo-local state | `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs`, `src/GsdOrchestrator.Tests/MultiRepoConfigTests.cs` |
| Validation gates before commit and PR flow | `src/GsdOrchestrator/Workflows/States/ValidatingState.cs` |
| Review submission to GitHub as part of the workflow | `src/GsdOrchestrator/Workflows/States/ReviewingState.cs`, `src/GsdOrchestrator.Tests/ReviewingStateTests.cs` |

## Demo-ready paths

1. Run `dotnet run -- --issue 42` from `src/GsdOrchestrator` to execute the issue workflow.
2. Run `dotnet run -- --issue 42 --triage` to demonstrate issue classification without code changes.
3. Run `dotnet run -- --pr 7` to demonstrate PR review mode.
4. Run `dotnet run -- --watch` to demonstrate polling across configured repositories.

These commands still require valid `.env` configuration and external service credentials. The point here is that the modes are implemented in the codebase, not just described in slides.
