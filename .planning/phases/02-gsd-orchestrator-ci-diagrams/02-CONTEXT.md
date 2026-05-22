# Phase 2: gsd-orchestrator CI & Diagrams - Context

**Gathered:** 2026-05-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a passing GitHub Actions CI workflow, two Mermaid architecture diagrams, and a badges line to the gsd-orchestrator repository. No new application code — all changes are additive (`.github/`, README updates).

</domain>

<decisions>
## Implementation Decisions

### State Machine Diagram
- **D-01:** Use `stateDiagram-v2` with `direction LR`. Show all 9 states (Idle, Analyzing, Branching, Editing, Validating, Committing, PrCreating, Reviewing, Documenting → Done) with **no transition labels**. Labels cause Mermaid rendering bugs (#2902, #5827) and add clutter for a 60-second hiring manager scan.
- **D-02:** Below the diagram, add brief per-state descriptions — 1-2 lines per state: what the state does and what triggers the transition to the next state. This gives tech leads the trigger detail without cluttering the diagram.

### Component Diagram
- **D-03:** Show the orchestrator's three integration points: `McpStdioClient → github-mcp-server.exe → GitHub API`, `Anthropic.SDK → Claude API`, and `FileCheckpointStore → .checkpoints/`. Use `graph LR` or `flowchart LR` for the component diagram.

### CI Workflow
- **D-04 (Claude's discretion):** Trigger on `push` to `main` AND `pull_request`. This ensures the badge reflects the default branch while also validating PRs.
- **D-05 (Claude's discretion):** Build directly via `src/GsdOrchestrator/GsdOrchestrator.csproj` (not `GithubMCP.slnx`). The `.slnx` solution format is newer and less battle-tested in CI environments; the project file is portable and unambiguous.
- **D-06 (Claude's discretion):** Use `windows-latest` runner (matches dev environment and Prerequisites section in README). Steps: `dotnet restore` → `dotnet build --no-restore --configuration Release`.

### Diagram Placement
- **D-07 (Claude's discretion):** Add a new `## Diagrams` section to the README, placed between the existing `## How it works` line and the `## Prerequisites` section. Keep the existing ASCII `## Architecture` block intact — the Mermaid diagrams show behavior/component topology while the ASCII block shows code structure. They complement, not duplicate.

### Badges
- **D-08 (Claude's discretion):** Add a badge line immediately below the headline `# GSD Orchestrator` and subtitle. Three badges: GitHub Actions CI (standard Actions badge format), `.NET 10` (shields.io), `MIT License` (shields.io). Match the `Coding-Autopilot-System/gsd-orchestrator` repo path exactly.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### gsd-orchestrator Repository
- `src/GsdOrchestrator/GsdOrchestrator.csproj` — Target framework (`net10.0`), SDK (`Microsoft.NET.Sdk.Worker`), all NuGet dependencies. Use this for the CI build command.
- `README.md` — Current README structure. New content must integrate with existing sections (How it works, Prerequisites, Setup, Run, Architecture, Project structure). Do NOT restructure existing sections.

### State Machine Implementation
- `src/GsdOrchestrator/Workflows/States/` — 9 state files: `IdleState.cs`, `AnalyzingState.cs`, `BranchingState.cs`, `EditingState.cs`, `ValidatingState.cs`, `CommittingState.cs`, `PrCreatingState.cs`, `ReviewingState.cs`, `DocumentingState.cs`. Read these to write accurate per-state descriptions in D-02.
- `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` — State machine orchestration; read to understand transition triggers for the prose supplement.

### MCP / Component Architecture
- `src/GsdOrchestrator/Mcp/McpStdioClient.cs` — Spawns `github-mcp-server.exe` as stdio child process; the component diagram must accurately reflect this (not HTTP for the orchestrator-to-MCP connection).
- `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs` — Checkpoint store; include in component diagram.

### Project Decisions
- `.planning/PROJECT.md` § Key Decisions — Mermaid over image files (decided), enterprise tone (decided), gsd-orchestrator as crown jewel (decided).
- `.planning/REQUIREMENTS.md` — GSD-01 (CI badge), GSD-02 (state machine diagram), GSD-03 (component diagram), GSD-09 (badges). Phase must close all four.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `McpStdioClient.cs` — Spawns MCP server as a **stdio child process** (not HTTP). The component diagram must show this correctly: `Orchestrator → McpStdioClient (stdio) → github-mcp-server.exe → GitHub API`.
- `.env.example` — Documents all required env vars. CI does not need these at build time (no `.env` required for `dotnet build`).

### Established Patterns
- README already uses enterprise tone and code fences — maintain consistent formatting.
- README has a text flow line: `Issue → Analyzing → Branching → ...`. The Mermaid state diagram replaces/supersedes this visual, but the text line can stay as a quick reference above the full diagram.
- `GithubMCP.slnx` is a Visual Studio Solution XML file (not `.sln`). `dotnet build` supports it from .NET 9+, but avoid it in CI — use the `.csproj` directly.

### Integration Points
- New `.github/workflows/ci.yml` — new file, no conflicts with existing code.
- README badge line — insert above the first horizontal rule (`---`) that separates the headline from the How it works section.
- Mermaid Diagrams section — insert between `## How it works` block and `## Prerequisites`.

</code_context>

<specifics>
## Specific Ideas

- State machine Mermaid: `stateDiagram-v2` / `direction LR`. State sequence: `[*] --> Idle --> Analyzing --> Branching --> Editing --> Validating --> Committing --> PrCreating --> Reviewing --> Documenting --> [*]`. State node names must match the class names exactly (PrCreating not PrCreation, Documenting not Documentation).
- Component diagram: use `flowchart LR` with subgraphs for GitHub side and Anthropic side to visually separate external dependencies from internal components.
- Per-state prose: 9 short bullets (1-2 lines each). Tone: precise, technical, no fluff. Example style: "**Analyzing** — reads the issue body via GitHub MCP, asks Claude to produce a change plan and target file list."

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 2-gsd-orchestrator-ci-diagrams*
*Context gathered: 2026-05-22*
