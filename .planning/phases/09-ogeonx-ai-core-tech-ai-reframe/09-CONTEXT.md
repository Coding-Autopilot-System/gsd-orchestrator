# Phase 9: OgeonX-Ai Core Tech AI Reframe + Level A - Context

**Gathered:** 2026-05-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Reposition `OgeonX-Ai/enterprise-ai-gateway` and `OgeonX-Ai/android` as AI engineering work with Level A documentation. Both repos live under the OgeonX-Ai personal GitHub account (not CAS). Each repo receives: (1) codebase scan to understand actual purpose, (2) README rewrite with hero line, badges, Mermaid architecture diagram, and cross-links, (3) GitHub wiki with 4 standard pages, (4) GitHub Actions CI badge. No modifications to existing source code — additive docs/CI only.

</domain>

<decisions>
## Implementation Decisions

### Codebase Scanning (Both Repos)
- **D-01:** Both repos require a codebase scan BEFORE writing any README or wiki content. Executor reads actual source files to understand what each repo does. Do NOT invent framing from repo names alone.

### enterprise-ai-gateway Framing (TECH-01)
- **D-02:** Framing is entirely codebase-driven. After scanning, derive the hero line, architecture description, and Mermaid diagram from what the code actually does. No pre-committed angle (not locked to "LLM proxy", "middleware", or "enterprise integration" — let the code speak).
- **D-03:** Architecture diagram: `flowchart LR` Mermaid — same pattern as all other repos. Core pipeline from scan drives the node/edge design.

### android Framing (TECH-02)
- **D-04:** Frame as an **AI-powered Android app** — leads with AI capabilities, then explains the app. Even if AI integration is partial or lightweight, position the AI features as the headline.
- **D-05:** Wiki page names: standard set — Home, Setup-Guide, Architecture, Configuration-Reference. Same as all prior repos (consistent portfolio pattern).
- **D-06:** Architecture diagram: `flowchart LR` Mermaid — same pattern as all other repos.

### android CI Strategy (TECH-02)
- **D-07:** CI = Gradle build + JVM unit tests, no emulator. Steps: `actions/setup-java` (Java 17 or 21, temurin distribution) → `gradlew test` (JVM unit tests only). No instrumented tests, no emulator setup.
- **D-08:** Run on `push` to `main` AND `pull_request`. ubuntu-latest runner.
- **D-09:** Badge uses `ci.yml/badge.svg?branch=main` — executor confirms the workflow filename after scanning the repo's `.github/workflows/` directory.

### enterprise-ai-gateway CI
- **D-10:** CI language/stack determined by codebase scan. Follow the same lightweight pattern as prior repos: build step + tests (if any exist). push + PR, ubuntu-latest. Executor confirms workflow name.

### Cross-Links (Both Repos)
- **D-11:** CAS ecosystem badge + line in both repos: shields.io org badge linking to `Coding-Autopilot-System` + "Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator] | [Promptimprover] | [autogen]" with markdown links. Same pattern as Phases 4-5.
- **D-12:** Intra-OgeonX-Ai linking: each repo includes a "See also" line linking to the other. `enterprise-ai-gateway` links to `android`; `android` links to `enterprise-ai-gateway`. Simple markdown link, not a badge.

### Carried Forward from Prior Phases
- **D-CF-01:** Enterprise tone throughout — no emoji in README or wiki.
- **D-CF-02:** Mermaid diagram in `## Architecture` section of README (Phase 2 pattern).
- **D-CF-03:** Wiki Home: hero paragraph + badges, quick-start snippet, navigation table (Phase 3/4 pattern).
- **D-CF-04:** CI push+PR triggers, ubuntu-latest (Phases 2, 4, 5, 7, 8 pattern).
- **D-CF-05:** No modifications to existing source code — docs/CI additions only.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-Level Decisions
- `.planning/PROJECT.md` — Enterprise tone constraint, no emoji, hiring-manager audience, gsd-orchestrator as crown jewel. Core value: "this person builds production-grade AI systems."
- `.planning/REQUIREMENTS.md` — TECH-01 (enterprise-ai-gateway) and TECH-02 (android). Both must be fully closed by this phase.

### Prior Phase Patterns (representative — executor follows these patterns)
- `.planning/phases/04-promptimprover-polish/04-CONTEXT.md` — Established README structure (D-03 through D-10), wiki pattern (D-11 through D-14), badge placement, cross-link format.
- `.planning/phases/08-cas-secondary-repos-level-a/08-04-SUMMARY.md` — Most recent CI fix. Notes ci.yml gotchas: `rg` not available on ubuntu-latest (use `grep`/`find`); bash backtick in double-quote strings causes EOF.

### Remote Repos (executor scans these)
- `OgeonX-Ai/enterprise-ai-gateway` — Executor must read source files to understand purpose before writing README/wiki.
- `OgeonX-Ai/android` — Executor must read source files (AndroidManifest.xml, key Activities/Fragments, build.gradle) to understand AI integration before writing README/wiki.

### No external ADRs or specs
No external specification documents exist for these repos — decisions are captured fully in this CONTEXT.md.

</canonical_refs>

<code_context>
## Existing Code Insights

### Established Patterns
- **README structure:** hero line → CAS badge + ecosystem line → "See also" OgeonX-Ai sibling link → CI/License badges → `## Architecture` (flowchart LR Mermaid) → `## Quick Start` → cross-link ecosystem line
- **Wiki pattern:** 4 pages — Home (hero + quick-start snippet + nav table), Setup-Guide (standalone, includes "what success looks like" section), Architecture (Mermaid diagram + prose), Configuration-Reference (table: Name | Type | Required | Default | Description)
- **CI pattern:** `name: CI`, `on: push main + pull_request`, ubuntu-latest, steps tailored to language

### Known CI Gotchas (from Phase 8 experience)
- `rg` (ripgrep) is NOT available on ubuntu-latest runners — use `grep -rl` or `find` instead
- Bash backticks inside double-quoted strings cause EOF parsing errors — use single-quoted strings for patterns containing backticks
- GitHub workflow scope requires GITHUB_MCP_PAT (not just repo-scoped token) for `.github/workflows/` file writes

### Integration Points
- Both repos link OUT to CAS org (not the reverse — CAS repos do not need updating)
- OgeonX-Ai personal profile (updated in Phase 6) already links to CAS — these repos reinforce that story

</code_context>

<specifics>
## Specific Ideas

- android CI: `gradlew test` (JVM unit tests) is the target step. If no unit tests exist in the scanned codebase, fall back to `gradlew assembleDebug` (build only) rather than creating empty test stubs.
- enterprise-ai-gateway: if the codebase contains an existing README, read it before rewriting to preserve accurate factual claims — same discipline as Phase 4 (D-03 in 04-CONTEXT.md).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 9-OgeonX-Ai Core Tech AI Reframe + Level A*
*Context gathered: 2026-05-27*
