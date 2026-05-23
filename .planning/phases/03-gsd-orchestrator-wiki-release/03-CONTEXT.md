# Phase 3: gsd-orchestrator Wiki & Release - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Create 4 GitHub Wiki pages for `Coding-Autopilot-System/gsd-orchestrator` (Home, Setup Guide, Architecture, Configuration Reference) and publish a GitHub Release v1.0.0 with feature-narrative release notes. All changes are additive — no modifications to existing README or source code.

</domain>

<decisions>
## Implementation Decisions

### Wiki Architecture Page (GSD-06)
- **D-01:** Embed the same `stateDiagram-v2` and `flowchart LR` diagrams from the README — do NOT duplicate them as new/different diagrams. Add expanded concise-bullet prose below each: 1-3 bullets per state (what it does, which API it calls, what triggers the transition). Reuse Phase 2 diagram content rather than creating net-new diagrams.
- **D-02:** Add a "Data Flow" section covering the Issue-to-PR transformation story: what goes in (GitHub issue body, labels) and what comes out (feature branch, commits, pull request with bot review). Frame it as a transformation narrative, not an API call list.

### Wiki Setup Guide (GSD-05)
- **D-03:** Standalone, self-contained Wiki page — NOT a reference to the README. The executor MUST read `.env.example` and verify each step against the actual source before writing. The guide must be copy-pasteable and accurate.
- **D-04:** Include a "What a successful run looks like" section at the end — show the expected terminal output (state machine transitions logging, final PR URL). Confirms setup worked and showcases the system's behavior.

### Wiki Home Page (GSD-04)
- **D-05:** Serve both audiences (hiring manager + developer) in 2 scrolls: (1) short hero paragraph pitching what gsd-orchestrator does, key badges (CI, .NET 10, MIT), (2) quick-start code snippet showing env vars + `dotnet run` command (5 lines max), (3) navigation table linking to the 3 other Wiki pages with one-liners.
- **D-06:** Quick-start snippet on Home shows ONLY the run command with the 3-4 required env vars set — NOT the full clone-through-run sequence (that belongs in the Setup Guide).

### GitHub Release v1.0.0 (GSD-08)
- **D-07:** GitHub Release notes ONLY — no CHANGELOG.md committed to main.
- **D-08:** Release notes use a feature-narrative format: lead with what the system does autonomously (issue-to-PR), call out key technical decisions (state machine, MCP stdio, Polly resilience, file checkpointing), and name the stack. Optimized for hiring manager impression, NOT a commit-based changelog.

### Wiki Configuration Reference (GSD-07)
- **D-09 (Claude's discretion):** Table format — Name | Type | Required | Default | Description. Source of truth is `.env.example` and any env var reads in the source code. Group by concern (GitHub vars, Anthropic vars, Behavior vars).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### gsd-orchestrator Repository (remote: Coding-Autopilot-System/gsd-orchestrator)
- `.env.example` — All required env vars with descriptions. MUST be read before writing Setup Guide or Config Reference.
- `README.md` — Existing Setup section (D-03: verify and expand, do not replace). Architecture section, Prerequisites section.
- `src/GsdOrchestrator/GsdOrchestrator.csproj` — Target framework, dependencies.

### State Machine (for Architecture wiki accuracy)
- `src/GsdOrchestrator/Workflows/States/` — 9 state files. Read to verify per-state descriptions in Architecture wiki.
- `src/GsdOrchestrator/Workflows/GsdStateMachine.cs` — State transition triggers.

### Phase 2 Outputs (reuse in Architecture wiki)
- `C:/GithubMCP/.planning/phases/02-gsd-orchestrator-ci-diagrams/02-02-SUMMARY.md` — Contains the exact Mermaid syntax used in the README. Architecture wiki page MUST use the same diagram content (D-01).

### Project Decisions
- `.planning/PROJECT.md` — Enterprise tone (constraint), crown jewel framing, GitHub Wiki decided.
- `.planning/REQUIREMENTS.md` — GSD-04 (Wiki Home), GSD-05 (Setup Guide), GSD-06 (Architecture), GSD-07 (Config Reference), GSD-08 (Release v1.0.0). Phase must close all five.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 2 Mermaid diagrams (stateDiagram-v2 + flowchart LR) — already in README. Architecture wiki embeds same diagrams with expanded prose (D-01). Do NOT reinvent.
- `.env.example` in remote repo — authoritative source for all env var names, types, and descriptions. Config Reference page is essentially a formatted version of this file.

### Established Patterns
- Enterprise tone throughout (PROJECT.md constraint) — no toy/demo language in any Wiki page.
- GitHub Wiki pages use Markdown; GitHub renders Mermaid in Wiki pages the same way as in README.
- All changes target the REMOTE repo (Coding-Autopilot-System/gsd-orchestrator) via GitHub MCP tools or git clone+push — NOT the local C:/GithubMCP repo.
- GitHub Wiki is a separate git repo at `https://github.com/Coding-Autopilot-System/gsd-orchestrator.wiki.git`. Use `gh api` or git clone to create/update pages.

### Integration Points
- Wiki pages link to each other (Home navigation table → Setup, Architecture, Config Reference).
- Wiki Architecture page embeds same diagrams that live in README — consistent cross-surface story.
- Release v1.0.0 tag points to current HEAD of main (after all Phase 3 commits land).

</code_context>

<specifics>
## Specific Ideas

- Wiki Home quick-start: 5-line bash snippet — export the 3-4 required env vars, then `dotnet run --project src/GsdOrchestrator/GsdOrchestrator.csproj`. Show the GH_ISSUE_NUMBER var last so the user knows what to change per run.
- Architecture wiki data flow: "Takes a GitHub Issue → produces a feature branch, one or more commits, a pull request with a bot review comment, and optional auto-merge. If GSD_AUTO_MERGE=true, squash-merges after review."
- Release notes tone: lead sentence should be something like "gsd-orchestrator v1.0.0 — the first production release of an autonomous GitHub workflow system that turns a GitHub issue into a reviewed pull request without human intervention."

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 3-gsd-orchestrator-wiki-release*
*Context gathered: 2026-05-23*
