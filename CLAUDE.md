# gsd-orchestrator

Autonomous GitHub workflow engine. Reads issues, plans code changes via Claude (MCP), branches, edits, commits, opens PRs, and generates tests — driven by a state machine. Production-grade .NET 10 C# with enterprise patterns.

## Project Context

See `.planning/PROJECT.md` for goals, milestone status, and requirements.

**Current milestone**: v3.0 — multi-repo support, test generation, PR review loop. All 5 phases complete, 35 tests green.

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 10 |
| State machine | Custom `GsdStateMachine` with 11 `IWorkflowState` implementations |
| Claude integration | MCP stdio client (`McpStdioClient` / `IMcpClient`) — JSON-RPC over subprocess |
| Resilience | Polly (`Microsoft.Extensions.Resilience`) |
| Logging | Serilog with structured JSON output |
| DI | `Microsoft.Extensions.DependencyInjection` + `IHost` |
| Checkpointing | File-based (`FileCheckpointStore`) — namespaced per `{owner}_{repo}_{workflowId}.json` |
| Auth | GitHub PAT (`GitHubPatProvider`) via env var |
| Tests | xUnit + NSubstitute |

## State Machine Flow

```
Idle → Triaging → Analyzing → Branching → Editing → Committing
     → TestGenerating → Reviewing → Validating → Documenting → PrCreating
```

Each state implements `IWorkflowState` in `src/GsdOrchestrator/Workflows/States/`.

## Key Files

| File | Purpose |
|---|---|
| `src/GsdOrchestrator/Program.cs` | DI wiring, host setup, CLI entrypoint |
| `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` | State machine orchestrator |
| `src/GsdOrchestrator/Mcp/McpStdioClient.cs` | JSON-RPC stdio client to Claude |
| `src/GsdOrchestrator/Mcp/McpToolDispatcher.cs` | Tool dispatch over MCP |
| `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs` | Workflow persistence |
| `src/GsdOrchestrator/Workflows/Models/RepoConfigLoader.cs` | `GSD_REPOS` JSON config |

## Configuration

All configuration is environment-variable driven (`.env` or shell):
- `GSD_REPOS` — JSON array of `{owner, repo}` objects for multi-repo support
- `GITHUB_TOKEN` — PAT for GitHub API
- `ANTHROPIC_API_KEY` — or via MCP config
- `GSD_CHECKPOINT_DIR` — defaults to `state/checkpoints/`

## Build & Test

```powershell
dotnet build src/GsdOrchestrator.sln
dotnet test src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj
```

## Conventions

- `PascalCase` for types, `camelCase` for locals, `_camelCase` for private fields
- State classes are self-contained — inject only what they need, return `IWorkflowState` for transitions
- Never access `HttpClient` or GitHub API directly from state classes — go through injected services
- Tests use NSubstitute mocks, never live API calls

## GSD Workflow

Use `/gsd:plan-phase` before any multi-file change. Use `/gsd:quick` for single-file fixes.
