# Phase 4: Promptimprover Polish - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-23
**Phase:** 04-promptimprover-polish
**Areas discussed:** CI build scope, README positioning, Architecture diagram, Cross-repo links timing, Badge set, mcp-server visibility, Wiki page structure

---

## CI Build Scope

| Option | Description | Selected |
|--------|-------------|----------|
| universal-refiner only | npm ci + tsc build + vitest run in universal-refiner/. Active package with real tests. | ✓ |
| Both packages | Run npm ci + tsc in both universal-refiner/ and mcp-server/. | |
| You decide | Claude picks the cleanest CI configuration. | |

**User's choice:** universal-refiner only

| Option | Description | Selected |
|--------|-------------|----------|
| Build + test | npm ci, tsc build, then vitest run. | ✓ |
| Build only | npm ci + tsc build, skip vitest. | |

**User's choice:** Build + test
**Notes:** mcp-server/ excluded because its test script returns exit 1 and it's the legacy package.

---

## README Positioning

| Option | Description | Selected |
|--------|-------------|----------|
| MCP middleware | "Promptimprover is an MCP server middleware that intercepts and refines every AI prompt before code generation." | ✓ |
| Prompt governance platform | "Enterprise prompt governance layer for AI-assisted development — RAG context injection, compounding memory, ISO 27001-aligned audit trail." | |
| Universal AI refinement engine | Keep current 'Universal AI Governance' direction but clean it up. | |

**User's choice:** MCP middleware framing

| Option | Description | Selected |
|--------|-------------|----------|
| Technical capabilities first | Lead with: RAG neural snippets, compounding memory (SQLite brain), auto-heal middleware, context-aware scouting. | ✓ |
| Developer workflow benefits first | Lead with: 'every prompt automatically gets project context', 'errors self-heal', etc. | |
| You decide | Claude picks the order. | |

**User's choice:** Technical capabilities first

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — minimal quickstart | 3-4 line snippet: clone, build_and_install.ps1, add as MCP server. | ✓ |
| No — README is positioning only | Skip installation entirely, link to Wiki. | |
| You decide | Claude picks. | |

**User's choice:** Yes — minimal quickstart
**Notes:** Full details in Wiki Setup Guide; README shows just enough to orient a developer.

---

## Architecture Diagram

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — one flowchart | Single flowchart LR showing middleware pipeline. | ✓ |
| No — text architecture section only | Prose + component list, no Mermaid. | |

**User's choice:** Yes — one Mermaid flowchart

| Option | Description | Selected |
|--------|-------------|----------|
| Core pipeline only | AI CLI → MCP stdio → Promptimprover (RAG + Memory + Auto-Heal) → augmented prompt. | ✓ |
| Full system including storage | Add SQLite Brain db, .refiner/ store, hooks/ directory. | |
| You decide | Claude picks components. | |

**User's choice:** Core pipeline only

---

## Cross-Repo Links Timing

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 4 now | Add in Phase 4 per PI-05. Phase 6 can update all three as needed. | ✓ |
| Defer to Phase 6 | Phase 6 handles all inter-repo linking as a coherent batch. | |

**User's choice:** Phase 4 now

| Option | Description | Selected |
|--------|-------------|----------|
| Org badge + text links | shields.io org badge + "Part of Coding-Autopilot-System ecosystem: gsd-orchestrator \| autogen" line. | ✓ |
| Full Ecosystem section | ## Ecosystem section with sibling repo descriptions. | |

**User's choice:** Org badge + text links

---

## Badge Set (PI-04)

| Option | Description | Selected |
|--------|-------------|----------|
| CI + Node + License | Three badges. Matches gsd-orchestrator pattern. No version badge (not npm-published). | ✓ |
| CI + Node + License + Version | Add a v8.0.0 static badge. | |
| You decide | Claude picks. | |

**User's choice:** CI + Node + License

---

## mcp-server/ Visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Ignore it | README focuses on universal-refiner only. mcp-server/ not mentioned. | ✓ |
| Brief note in Project Structure | List mcp-server/ as 'legacy HTTP SSE server (archived)'. | |

**User's choice:** Ignore it — README focuses on universal-refiner only

---

## Wiki Page Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Same 2-scroll pattern (Phase 3) | Hero + badges, quick-start snippet, navigation table. | ✓ |
| Adapted pattern | Replace code snippet with MCP config JSON snippet. | |

**User's choice:** Same 2-scroll pattern — consistent portfolio presentation

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — show expected behavior | Include "what a successful setup looks like" section in Setup Guide. | ✓ |
| No — ends at 'server is running' | Setup guide ends at installation steps. | |

**User's choice:** Yes — show expected behavior

---

## Claude's Discretion

- D-13: Wiki Architecture page embeds same flowchart LR diagram from README with expanded prose below. No separate/different diagram.
- D-14: Wiki Configuration Reference — table format covering universal-refiner/ config: .refiner/ path, SQLite memory path, env vars.

## Deferred Ideas

None — discussion stayed within phase scope.
