# Phase 2: gsd-orchestrator CI & Diagrams - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-22
**Phase:** 2-gsd-orchestrator-ci-diagrams
**Areas discussed:** State Machine Diagram Fidelity

---

## State Machine Diagram Fidelity

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid: 9 states, no labels | All 9 states visible, clean LR rendering, no Mermaid label-overlap bugs, supplemented by prose | ✓ |
| Full 9 states + transition labels | Maximum detail, but risks label overlap/rendering glitches in GitHub Mermaid | |
| Simplified 3-4 phases | Most scannable, but undersells architectural depth | |

**User's choice:** Hybrid — 9 states, no transition labels (recommended by advisor research)
**Notes:** Research surfaced Mermaid label-overlap issues (#2902, #5827) that make full-label diagrams unreliable on GitHub. Hybrid was unanimously recommended for hiring-manager audience.

---

## Supplementary Prose (follow-up to State Machine)

| Option | Description | Selected |
|--------|-------------|----------|
| Brief state descriptions | 1-2 lines per state: what it does + what triggers next | ✓ |
| Lifecycle overview sentence | Single paragraph, no per-state breakdown | |
| You decide | Claude picks detail level | |

**User's choice:** Brief state descriptions
**Notes:** Confirmed: 1-2 lines per state covering purpose and transition trigger.

---

## Claude's Discretion

The following areas were not selected for discussion — Claude applied standard defaults:

- **CI trigger scope:** push to `main` + `pull_request`
- **Build target:** `src/GsdOrchestrator/GsdOrchestrator.csproj` (not `GithubMCP.slnx`)
- **CI runner:** `windows-latest`
- **Diagram placement:** New `## Diagrams` section between `## How it works` and `## Prerequisites`; ASCII `## Architecture` block preserved
- **Badge content:** CI, .NET 10, MIT License

## Deferred Ideas

None — discussion stayed within phase scope.
