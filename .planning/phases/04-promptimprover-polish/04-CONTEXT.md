# Phase 4: Promptimprover Polish - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Elevate the Promptimprover repository with: (1) enterprise README rewrite — MCP middleware framing, no emoji, Mermaid flowchart, minimal quickstart, cross-repo links; (2) GitHub Actions CI workflow targeting `universal-refiner/` with build + vitest tests; (3) GitHub Wiki with 4 pages (Home, Setup Guide, Architecture, Configuration Reference); (4) README badges (CI, Node 22, License). All changes are additive — no modifications to existing source code or application logic.

</domain>

<decisions>
## Implementation Decisions

### CI Workflow (PI-02)
- **D-01:** CI targets `universal-refiner/` only. Steps: `npm ci` → `tsc build` → `vitest run`. The `mcp-server/` package is older, has a broken test script (`exit 1`), and is not the active package.
- **D-02:** Run on `push` to `main` AND `pull_request`. Node 22 runner (matches `@types/node: ^22.x` in package.json).

### README Rewrite (PI-01)
- **D-03:** Hero framing: "Promptimprover is an MCP server middleware that intercepts and refines every AI prompt before code generation — applying project context, coding standards, and compounding memory." No emoji in the rewritten README.
- **D-04:** Feature list leads with technical capabilities: RAG neural snippets, compounding memory (SQLite brain), auto-heal middleware, context-aware scouting. Ordered for tech lead credibility.
- **D-05:** Include a minimal quickstart (3-4 lines): clone, run `build_and_install.ps1`, add `prompt-refiner` as MCP server. Full setup details belong in the Wiki Setup Guide, not the README.
- **D-06:** `mcp-server/` directory is NOT mentioned in the README. README describes Promptimprover as the `universal-refiner` MCP server only.

### Architecture Diagram (README)
- **D-07:** One `flowchart LR` Mermaid diagram showing the core middleware pipeline: AI CLI → MCP stdio → Promptimprover server → internal subgraph [RAG snippets | SQLite memory | Auto-heal] → augmented prompt output. Core pipeline only — no storage internals (.refiner/, SQLite file paths).
- **D-08:** Place the diagram in a `## Architecture` section in the README, consistent with gsd-orchestrator's placement pattern.

### Badges (PI-04)
- **D-09:** Three badges below the headline: GitHub Actions CI badge, Node.js 22 shields.io badge, MIT License shields.io badge. No version badge (package is not npm-published). Match `Coding-Autopilot-System/Promptimprover` repo path exactly.

### Cross-Repo Links (PI-05)
- **D-10:** Add in Phase 4 (not deferred to Phase 6). Form: shields.io org badge linking to `Coding-Autopilot-System` + a one-liner "Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator] | [autogen]" with markdown links. Place after the badge line.

### GitHub Wiki (PI-03)
- **D-11:** Wiki Home follows Phase 3 pattern (D-05/D-06 from 03-CONTEXT.md): (1) hero paragraph + badges, (2) quick-start MCP config snippet (JSON showing how to add `prompt-refiner` to Claude/Cursor config — max 5 lines), (3) navigation table linking to Setup Guide, Architecture, Configuration Reference.
- **D-12:** Wiki Setup Guide is standalone and self-contained (same principle as Phase 3 D-03). Must include a "What a successful setup looks like" section: MCP server starts, lists available tools, first prompt refinement returns augmented prompt.
- **D-13 (Claude's discretion):** Wiki Architecture page embeds the same `flowchart LR` Mermaid diagram from the README. Add expanded prose below: per-component description (what it does, what triggers it). Reuse README diagram — do NOT create a different diagram.
- **D-14 (Claude's discretion):** Wiki Configuration Reference covers `universal-refiner/` configuration: `.refiner/` knowledge store path, SQLite memory path, any env vars or config files the MCP server reads. Table format: Name | Type | Required | Default | Description.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Promptimprover Repository (remote: Coding-Autopilot-System/Promptimprover)
- `universal-refiner/package.json` — Active package: name `gemini-prompt-refiner` v8.0.0, scripts (build, test via vitest), Node 22 type definitions. CI workflow MUST target this directory.
- `universal-refiner/tsconfig.json` — TypeScript compiler config for the build step.
- `mcp-server/package.json` — Older package (CommonJS, v1.0.0). NOT targeted by CI. NOT mentioned in README.
- `README.md` — Current README (emoji-heavy, to be rewritten). Executor must read current content before rewriting to preserve any accurate factual claims.
- `build_and_install.ps1` — Install script referenced in quickstart. Read before writing the quickstart snippet.
- `universal-refiner/src/` — Source tree. Executor should read to understand components accurately before writing Wiki Architecture page.

### Project Decisions
- `.planning/PROJECT.md` — Enterprise tone constraint, gsd-orchestrator as crown jewel, GitHub Wiki decided.
- `.planning/REQUIREMENTS.md` — PI-01 (README), PI-02 (CI), PI-03 (Wiki), PI-04 (badges), PI-05 (cross-repo links). Phase must close all five.

### Prior Phase Patterns (reuse)
- `.planning/phases/03-gsd-orchestrator-wiki-release/03-CONTEXT.md` — D-05/D-06: Wiki Home 2-scroll pattern. D-03: standalone Setup Guide principle. D-04: "what a successful run looks like" section.
- `.planning/phases/02-gsd-orchestrator-ci-diagrams/02-CONTEXT.md` — D-04/D-05/D-06: CI workflow pattern (build on push + PR, use project file directly). Badge placement pattern (D-08).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `universal-refiner/package.json` scripts — `build` (tsc), `test` (vitest run) are the CI steps. Use these exact npm script names.
- Phase 3 Wiki delivery pattern — wiki as separate git repo (`Promptimprover.wiki.git`). Same clone+push approach applies.
- Phase 2 CI pattern — `.github/workflows/ci.yml` at repo root, `working-directory` to target a subdirectory package.

### Established Patterns
- Enterprise tone throughout (PROJECT.md constraint) — no toy/demo language, no emoji in any deliverable.
- GitHub Wiki is a separate git repo: `https://github.com/Coding-Autopilot-System/Promptimprover.wiki.git`.
- All changes target the REMOTE repo via GitHub MCP tools or git clone+push — NOT the local C:/GithubMCP repo.
- Mermaid renders in GitHub Wiki pages the same as in README.
- Badges use shields.io with exact repo path `Coding-Autopilot-System/Promptimprover`.

### Integration Points
- CI badge URL will reference `Coding-Autopilot-System/Promptimprover` GitHub Actions workflow.
- Cross-repo links reference: `https://github.com/Coding-Autopilot-System/gsd-orchestrator` and `https://github.com/Coding-Autopilot-System/autogen`.
- Wiki pages link to each other (Home navigation table → Setup, Architecture, Config Reference).

</code_context>

<specifics>
## Specific Ideas

- README hero: "Promptimprover is an MCP server middleware that intercepts and refines every AI prompt before code generation — applying project context, coding standards, and compounding memory."
- Mermaid diagram: `flowchart LR` with a subgraph for the internal pipeline: `AI CLI -->|stdio| Promptimprover --> subgraph internal["Promptimprover Engine"] RAG[RAG Snippets] Memory[SQLite Memory] AutoHeal[Auto-Heal] end --> AugmentedPrompt[Augmented Prompt]`
- Wiki Home quickstart: JSON snippet showing MCP server config entry for Claude Desktop (`"prompt-refiner": { "command": "prompt-refiner" }`)
- Badge line: CI badge (GitHub Actions) + `![Node 22](https://img.shields.io/badge/node-22-brightgreen)` + `![MIT](https://img.shields.io/badge/license-MIT-blue)`

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 4-promptimprover-polish*
*Context gathered: 2026-05-23*
