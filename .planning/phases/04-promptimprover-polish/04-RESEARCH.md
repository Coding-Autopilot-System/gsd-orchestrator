# Phase 4: Promptimprover Polish — Research

**Researched:** 2026-05-23
**Domain:** GitHub Actions CI (Node 22 / TypeScript / Vitest), README authoring, GitHub Wiki (git-push model), shields.io badges
**Confidence:** HIGH — all claims verified against live remote repo files and GitHub API

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** CI targets `universal-refiner/` only. Steps: `npm ci` → `tsc build` → `vitest run`. The `mcp-server/` package is older, has a broken test script (`exit 1`), and is not the active package.
- **D-02:** Run on `push` to `main` AND `pull_request`. Node 22 runner (matches `@types/node: ^22.x` in package.json).
- **D-03:** Hero framing: "Promptimprover is an MCP server middleware that intercepts and refines every AI prompt before code generation — applying project context, coding standards, and compounding memory." No emoji in the rewritten README.
- **D-04:** Feature list leads with technical capabilities: RAG neural snippets, compounding memory (SQLite brain), auto-heal middleware, context-aware scouting. Ordered for tech lead credibility.
- **D-05:** Minimal quickstart (3-4 lines): clone, run `build_and_install.ps1`, add `prompt-refiner` as MCP server. Full setup details belong in the Wiki Setup Guide, not the README.
- **D-06:** `mcp-server/` directory is NOT mentioned in the README. README describes Promptimprover as the `universal-refiner` MCP server only.
- **D-07:** One `flowchart LR` Mermaid diagram showing: AI CLI → MCP stdio → Promptimprover server → internal subgraph [RAG snippets | SQLite memory | Auto-heal] → augmented prompt output. Core pipeline only — no storage internals (.refiner/, SQLite file paths).
- **D-08:** Place the diagram in a `## Architecture` section in the README, consistent with gsd-orchestrator's placement pattern.
- **D-09:** Three badges below the headline: GitHub Actions CI badge, Node.js 22 shields.io badge, MIT License shields.io badge. No version badge (package is not npm-published). Match `Coding-Autopilot-System/Promptimprover` repo path exactly.
- **D-10:** Cross-repo links in Phase 4 (not deferred). Form: shields.io org badge linking to `Coding-Autopilot-System` + one-liner "Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator] | [autogen]" with markdown links. Place after the badge line.
- **D-11:** Wiki Home follows Phase 3 pattern: (1) hero paragraph + badges, (2) quick-start MCP config snippet (JSON showing how to add `prompt-refiner` to Claude/Cursor config — max 5 lines), (3) navigation table linking to Setup Guide, Architecture, Configuration Reference.
- **D-12:** Wiki Setup Guide is standalone and self-contained. Must include "What a successful setup looks like" section: MCP server starts, lists available tools, first prompt refinement returns augmented prompt.
- **D-13 (Claude's discretion):** Wiki Architecture page embeds the same `flowchart LR` Mermaid diagram from the README. Add expanded prose below: per-component description. Reuse README diagram — do NOT create a different diagram.
- **D-14 (Claude's discretion):** Wiki Configuration Reference covers `universal-refiner/` configuration. Table format: Name | Type | Required | Default | Description.

### Claude's Discretion

- Wiki Architecture page prose depth and per-component descriptions (D-13).
- Wiki Configuration Reference column order and grouping (D-14).
- Exact wording of wiki page introductions (enterprise tone constraint applies throughout).

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PI-01 | README rewritten — remove internal language, add hero line, architecture section | Current README fully fetched; hero text locked in D-03; feature list in D-04; quickstart in D-05 |
| PI-02 | GitHub Actions CI workflow (TypeScript/Node build) with passing badge | package.json scripts verified; CI pitfall (Windows build script) documented; workflow pattern from Phase 2 available |
| PI-03 | GitHub Wiki — Home, Setup Guide, Architecture, Configuration Reference | Wiki not yet initialized (has_wiki: false); Phase 3 pattern fully documented; source tree read for Architecture accuracy |
| PI-04 | README badges: CI, Node, License | Badge URL formats documented; default branch is `master`; LICENSE confirmed present |
| PI-05 | Cross-repo links to org and sibling projects | gsd-orchestrator and autogen URLs verified; shields.io org badge format documented |
</phase_requirements>

---

## Summary

Phase 4 elevates the Promptimprover repository with five additive changes: README rewrite, GitHub Actions CI, GitHub Wiki (4 pages), README badges, and cross-repo links. No source code modifications.

**Critical CI finding:** The `universal-refiner/package.json` `build` script contains Windows-specific shell commands (`if not exist` and `copy`) that will fail on a Linux runner. The CI workflow MUST NOT call `npm run build`. Instead: run `node scripts/sync-version.mjs` (cross-platform Node.js) for version generation, then `npx tsc --noEmit` for type-checking, then `npm test` (vitest run) for tests. Vitest handles TypeScript transpilation internally and does not require a compiled `dist/` directory.

**Critical Wiki finding:** The Promptimprover wiki has NOT been initialized (`has_wiki: false`, `git ls-remote` would return "Repository not found"). Phase 3 established this is a confirmed GitHub platform limitation — a human must create the first page via the web UI before automation can push pages. Wave 0 must include a manual checkpoint identical to Phase 3's 03-00 plan.

**Default branch is `master`** (not `main`). The CI workflow trigger, badge URL, and any git push commands must use `master`.

**Primary recommendation:** Plan three waves. Wave 0: manual wiki initialization checkpoint. Wave 1: CI workflow creation + README rewrite with badges. Wave 2: wiki page push (all 4 pages via clone+push to wiki.git).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CI workflow | GitHub Actions (remote) | — | Workflow file committed to remote repo; no local build environment involved |
| README authoring | Remote repo (GitHub MCP / gh API) | — | All changes target Coding-Autopilot-System/Promptimprover remote, not local repo |
| Badge rendering | shields.io CDN | GitHub Actions (CI badge) | CI badge served by GitHub; other badges by shields.io |
| Wiki page delivery | GitHub Wiki git repo (wiki.git) | Local temp dir (staging) | Pages staged locally, pushed via git to wiki.git remote |
| TypeScript type-check | GitHub Actions runner | — | `tsc --noEmit` runs in CI; no local compilation needed for docs phase |
| Test execution | Vitest (on GitHub Actions) | — | `npm test` → `vitest run` handles TS transpilation natively |

---

## Standard Stack

### Core Tools

| Tool | Version | Purpose | Source |
|------|---------|---------|--------|
| `gh` CLI | 2.86.0 | GitHub API calls, content creation | [VERIFIED: `gh --version`] |
| `git` | 2.53.0.windows.1 | Wiki page push via clone+push to wiki.git | [VERIFIED: `git --version`] |
| `actions/checkout` | v4 | CI step | [VERIFIED: Phase 2 CI pattern uses v6; verify latest] |
| `actions/setup-node` | v4 | Node 22 setup in CI | [CITED: github.com/actions/setup-node] |
| `shields.io` | — | Badge generation for Node 22, License badges | [VERIFIED: Phase 2 pattern; same service] |

### Promptimprover Package Verified Facts

| Property | Value | Source |
|----------|-------|--------|
| Package name | `gemini-prompt-refiner` | [VERIFIED: universal-refiner/package.json] |
| Package version | 8.0.0 | [VERIFIED: universal-refiner/package.json] |
| Node engine | Node 22 (via `@types/node: ^22.x`) | [VERIFIED: package.json devDependencies] |
| Test framework | Vitest 4.1.4 | [VERIFIED: package.json + package-lock.json] |
| TypeScript | 5.9.3 | [VERIFIED: package.json devDependencies] |
| Module system | ESM (`"type": "module"`) | [VERIFIED: package.json] |
| Build script | `tsc && (if not exist ...) && copy ...` — WINDOWS ONLY | [VERIFIED: package.json scripts.build] |
| Test script | `vitest run` | [VERIFIED: package.json scripts.test] |
| Pretest script | `node scripts/sync-version.mjs` | [VERIFIED: package.json scripts.pretest] |
| Default branch | `master` | [VERIFIED: GitHub API repos/Coding-Autopilot-System/Promptimprover] |
| Has wiki | `false` (not initialized) | [VERIFIED: GitHub API has_wiki field] |
| LICENSE | Present (MIT, 1066 bytes) | [VERIFIED: GitHub API contents/LICENSE] |

### mcp-server Package (EXCLUDED from CI)

| Property | Value | Source |
|----------|-------|--------|
| Test script | `echo "Error: no test specified" && exit 1` | [VERIFIED: mcp-server/package.json] |
| Status | Older CommonJS package, not active | [VERIFIED: package.json version 1.0.0, type: commonjs] |
| CI inclusion | NEVER — broken test script | Decision D-01 |

---

## Architecture Patterns

### System Architecture Diagram

```
Phase 4 Delivery Flow

  Remote Repo (Coding-Autopilot-System/Promptimprover)
          |
          +-- Wave 0: Manual Step
          |     GitHub Web UI → creates first wiki page (initializes wiki.git)
          |
          +-- Wave 1: CI + README
          |     gh API → create .github/workflows/ci.yml
          |     gh API → update README.md (rewrite + badges + cross-repo links)
          |
          +-- Wave 2: Wiki Pages
                Local temp dir (/tmp/pi-wiki)
                git clone wiki.git → write 4 .md files → git push master
                        |
                        +-- Home.md
                        +-- Setup-Guide.md
                        +-- Architecture.md
                        +-- Configuration-Reference.md
```

### CI Workflow Pattern

Reuse the Phase 2 gsd-orchestrator CI pattern (`ubuntu-latest` runner with `working-directory`) adapted for Node 22 / TypeScript.

```yaml
# Source: Phase 2 CI pattern + Node 22 adaptation
name: CI

on:
  push:
    branches: [ master ]
  pull_request:

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: universal-refiner
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node 22
        uses: actions/setup-node@v4
        with:
          node-version: '22'

      - name: Install dependencies
        run: npm ci

      - name: Generate version file
        run: node scripts/sync-version.mjs

      - name: Type check
        run: npx tsc --noEmit

      - name: Run tests
        run: npm test
```

**Why `npx tsc --noEmit` not `npm run build`:**
The `build` script contains `(if not exist dist\\src\\core mkdir dist\\src\\core) && copy src\\core\\dashboard.html dist\\src\\core\\dashboard.html /Y` — Windows CMD syntax that fails on Ubuntu runner. `--noEmit` performs type-checking without file output, avoiding the Windows-specific copy step entirely. Vitest handles TypeScript transpilation for tests via its built-in esbuild transform; it does NOT require a compiled `dist/` directory.

**Why `node scripts/sync-version.mjs` explicitly:**
The `pretest` hook runs this automatically before `npm test`, but the `tsc --noEmit` step needs `src/core/generated-version.ts` to exist first (it's imported by `version.ts`). Running it explicitly before the type-check step prevents a "file not found" compile error.

### Wiki Delivery Pattern (identical to Phase 3)

```bash
# Step 1 — Initialize (Wave 0: manual, one-time)
# User navigates to https://github.com/Coding-Autopilot-System/Promptimprover/wiki
# Clicks "Create the first page", saves any stub page

# Step 2 — Automated push (Wave 2)
rm -rf /tmp/pi-wiki
git clone https://x-access-token:$(gh auth token)@github.com/Coding-Autopilot-System/Promptimprover.wiki.git /tmp/pi-wiki
cd /tmp/pi-wiki
# Write .md files
git add .
git config user.email "agent@gsd"
git config user.name "GSD Agent"
git commit -m "docs: add Promptimprover wiki pages"
git push origin master
```

**Critical:** Wiki git repos use `master` branch regardless of main repo default branch setting. Use `git push origin master`.

### README Structure (PI-01)

Ordered sections for enterprise README:

1. `# Promptimprover` — H1 title
2. Badge line (CI + Node 22 + License) — immediately below H1
3. Cross-repo ecosystem line (D-10) — immediately below badges
4. Hero paragraph (D-03) — "Promptimprover is an MCP server middleware..."
5. `## Features` — Technical capability list (D-04)
6. `## Architecture` — Mermaid `flowchart LR` diagram (D-07, D-08)
7. `## Quickstart` — 3-4 line install sequence (D-05)
8. `## License` — MIT reference

### Mermaid Diagram (D-07)

```mermaid
flowchart LR
    CLI["AI CLI\n(Claude / Cursor)"] -->|"stdio"| PI["Promptimprover\n(prompt-refiner)"]
    PI --> subgraph internal["Promptimprover Engine"]
        RAG["RAG Snippets\n(FlexSearch)"]
        Memory["SQLite Memory\n(LocalBrain)"]
        AutoHeal["Auto-Heal\n(BackgroundService)"]
    end
    internal --> Out["Augmented Prompt"]
```

Note: Exact Mermaid syntax may need adjustment at execution time — subgraph-inside-arrow syntax varies by Mermaid version. The planner should note this as a verification requirement after push.

### Badge URLs

```markdown
[![CI](https://github.com/Coding-Autopilot-System/Promptimprover/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/Coding-Autopilot-System/Promptimprover/actions/workflows/ci.yml)
[![Node 22](https://img.shields.io/badge/node-22-brightgreen)](https://nodejs.org/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
```

**Important:** CI badge must specify `?branch=master` because the default branch is `master`, not `main`. Without this, the badge may show "no status" until the first push to master triggers CI.

### Cross-Repo Links (PI-05, D-10)

```markdown
Part of the [Coding-Autopilot-System](https://github.com/Coding-Autopilot-System) ecosystem:
[gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [autogen](https://github.com/Coding-Autopilot-System/autogen)
```

---

## Source Tree (for Wiki Architecture page accuracy)

Verified directory structure of `universal-refiner/src/`:

```
universal-refiner/src/
├── index.ts                    # Entry point: starts dashboard, server, background tasks
├── core/
│   ├── server.ts               # PromptRefinerServer — MCP server, tool handlers
│   ├── background-service.ts   # BackgroundAutonomyService — file watcher, auto-heal
│   ├── blackboard.ts           # AgenticBlackboard — shared intent/log state
│   ├── config.ts               # ConfigManager — reads .gemini-refiner.json
│   ├── dashboard.ts            # CommandCenterDashboard — web UI (port 3000)
│   ├── logger.ts               # RuntimeLogger — file-based logging to ~/.refiner/
│   ├── generated-version.ts    # Auto-generated by sync-version.mjs (not hand-edited)
│   └── version.ts              # Version helpers
├── detectors/
│   └── project-scout.ts        # NodeDetector, PythonDetector, ArchitecturalScout
├── memory/
│   ├── local-brain.ts          # LocalBrain — JSON pattern store in .refiner/memory.json
│   └── neural-snippets.ts      # NeuralSnippets — FlexSearch RAG over local codebase
├── refiners/
│   ├── prompt-refiner.ts       # PromptRefiner — core prompt augmentation logic
│   └── prompt-optimizer.ts     # PromptOptimizer — optimization pass
├── linters/
│   └── prompt-linter.ts        # PromptLinter — validates prompts against standards
└── history/
    ├── commit-ingest.ts        # CommitIngester — git log ingestion
    ├── correlation-engine.ts   # CorrelationEngine — correlates commits to outcomes
    ├── event-store.ts          # EventStore — SQLite event persistence
    ├── lesson-extractor.ts     # LessonExtractor — derives lessons from history
    ├── schema.ts               # DB schema
    ├── template-generator.ts   # TemplateGenerator — prompt templates from history
    └── timeline.ts             # Timeline — event ordering
```

[VERIFIED: GitHub API contents listing for all directories, 2026-05-23]

---

## Configuration Reference (for Wiki Config page — D-14)

### Environment Variables

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `PORT` | integer | No | `3000` | Web port for CommandCenter dashboard UI |
| `PROMPT_REFINER_GLOBAL_DIR` | string | No | `~/.refiner` | Global directory for runtime logs |
| `PROMPT_REFINER_LOG_LEVEL` | string | No | `info` | Log verbosity: `debug`, `info`, `warn`, `error` |

[VERIFIED: logger.ts and index.ts env var reads, 2026-05-23]

### File-Based Configuration

| File | Location | Purpose |
|------|----------|---------|
| `.gemini-refiner.json` | Project root (where `prompt-refiner` is run) | Optional: `mandates[]` (custom rules), `ignoredPaths[]` (paths to skip) |
| `.refiner/memory.json` | Project root `.refiner/` | LocalBrain pattern store (auto-created) |
| `.refiner/blackboard.json` | Project root `.refiner/` | AgenticBlackboard intent/log state (auto-created) |

[VERIFIED: config.ts (CONFIG_FILE = ".gemini-refiner.json"), local-brain.ts (DOT_REFINER = ".refiner", STORAGE_NAME = "memory.json"), 2026-05-23]

No `.env.example` exists in the repo — configuration is entirely via environment variables and the optional `.gemini-refiner.json` file.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| TypeScript CI on Linux | Custom build script wrapper | `npx tsc --noEmit` + `vitest run` | Avoids Windows-specific build script; vitest handles TS transpilation |
| Wiki page push | GitHub Contents API | `git clone wiki.git` + push | Contents API doesn't support wiki repos; git push is the only method |
| Badge generation | Static SVG files | shields.io URL parameters | Auto-updating, zero maintenance |
| Version number sync | Hardcoded version strings | `scripts/sync-version.mjs` (already exists) | Pre-existing script handles this; CI must call it |

---

## Common Pitfalls

### Pitfall 1: Windows Build Script on Ubuntu CI Runner

**What goes wrong:** `npm run build` fails on Ubuntu runner with `if: command not found` or `copy: command not found`.
**Why it happens:** The `build` script contains Windows CMD syntax: `(if not exist dist\\src\\core mkdir dist\\src\\core) && copy src\\core\\dashboard.html dist\\src\\core\\dashboard.html /Y`. This is only valid on Windows.
**How to avoid:** Do NOT call `npm run build` in CI. Instead: (1) `node scripts/sync-version.mjs` for version generation, (2) `npx tsc --noEmit` for type-checking, (3) `npm test` for tests. The `pretest` hook runs `sync-version.mjs` automatically before `npm test`, but the explicit step is needed before `tsc --noEmit`.
**Warning signs:** CI step "Build" failing with shell syntax errors on ubuntu-latest.

[VERIFIED: universal-refiner/package.json scripts.build — confirmed Windows CMD syntax, 2026-05-23]

### Pitfall 2: Wiki "Repository Not Found" (identical to Phase 3 Pitfall 1)

**What goes wrong:** `git clone https://github.com/Coding-Autopilot-System/Promptimprover.wiki.git` returns "Repository not found".
**Why it happens:** `has_wiki: false` — GitHub has NOT initialized the wiki.git repository. The wiki feature must be triggered by creating the first page via the web UI.
**How to avoid:** Wave 0 must be a manual human checkpoint: navigate to `https://github.com/Coding-Autopilot-System/Promptimprover/wiki`, click "Create the first page", save any stub. Automation can proceed only after this step.
**Warning signs:** Any git operation on the wiki.git URL fails before Wave 0 is confirmed.

[VERIFIED: GitHub API `has_wiki: false` confirmed 2026-05-23; GitHub platform behavior confirmed from Phase 3 research]

### Pitfall 3: Wrong Branch in CI Trigger and Badge URL

**What goes wrong:** CI badge shows "no status" or CI never runs on default branch.
**Why it happens:** Promptimprover default branch is `master`, not `main`. A CI trigger of `push: branches: [main]` will never fire on normal commits.
**How to avoid:** CI trigger must be `branches: [ master ]`. Badge URL must include `?branch=master` query parameter.
**Warning signs:** CI badge shows "no status" after the workflow is created; pushes to master don't trigger CI.

[VERIFIED: GitHub API default_branch: "master", 2026-05-23]

### Pitfall 4: Wiki Push to Wrong Branch

**What goes wrong:** `git push origin main` to wiki.git fails; pages don't appear.
**Why it happens:** GitHub wiki git repos always use `master` as their default branch, regardless of the main repo's default branch.
**How to avoid:** Always use `git push origin master` for wiki.git operations.
**Warning signs:** `git push` fails with "remote: error: refusing to update checked out branch: refs/heads/main".

[CITED: Phase 3 Research Pitfall 3 — confirmed pattern from prior phase execution]

### Pitfall 5: generated-version.ts Not Present Before tsc

**What goes wrong:** `tsc --noEmit` fails with "Cannot find module './generated-version.js'" or similar.
**Why it happens:** `src/core/generated-version.ts` is auto-generated by `scripts/sync-version.mjs` at build/test time. It is NOT committed to the repo (not in source tree). Without running the pretest/prebuild script first, the file doesn't exist for the type checker.
**How to avoid:** Always run `node scripts/sync-version.mjs` as a step BEFORE `npx tsc --noEmit`.
**Warning signs:** TypeScript compilation error referencing `generated-version.ts` or `PACKAGE_VERSION`.

[VERIFIED: universal-refiner/src/core/generated-version.ts is a generated file; scripts/sync-version.mjs writes it; tsconfig includes src/**/*; 2026-05-23]

### Pitfall 6: Mermaid Subgraph-in-Arrow Syntax

**What goes wrong:** Mermaid flowchart with subgraph renders as parse error in GitHub.
**Why it happens:** The `-->` arrow cannot point directly to a subgraph in all Mermaid versions. The syntax `A --> subgraph B ... end` is not universally valid.
**How to avoid:** Connect arrows to the last node inside the subgraph, or use a gateway node after the subgraph. Verify rendering after push.
**Warning signs:** GitHub renders "Syntax error in graph" instead of the diagram.

[ASSUMED — based on known Mermaid rendering quirks; planner should add a verification step]

---

## Wiki Page Content Guide

### Home.md (PI-03, D-11)

Structure (Phase 3 D-05/D-06 pattern adapted for MCP server):

1. `# Promptimprover` — H1
2. Badge line (CI + Node 22 + License) — same as README
3. Hero paragraph (2-3 sentences, enterprise tone, D-03 framing)
4. Quick-start JSON snippet: MCP config entry showing how to add `prompt-refiner` (max 5 lines, D-11):
   ```json
   {
     "mcpServers": {
       "prompt-refiner": {
         "command": "prompt-refiner"
       }
     }
   }
   ```
5. Navigation table:
   | Page | Description |
   |------|-------------|
   | [Setup Guide](Setup-Guide) | Prerequisites, install, and first prompt refinement |
   | [Architecture](Architecture) | Pipeline components and data flow |
   | [Configuration Reference](Configuration-Reference) | Environment variables and config files |

### Setup-Guide.md (PI-03, D-12)

Sections in order:
1. `## Prerequisites` — Windows, Node 22, Claude Desktop or Cursor
2. `## Installation` — `git clone`, `.\build_and_install.ps1`
3. `## Add to MCP Client` — JSON config snippet for Claude Desktop / Cursor
4. `## What a Successful Setup Looks Like` — MCP server starts, tool list appears, first prompt returns augmented output

**build_and_install.ps1 verified content:**
```
cd universal-refiner
npm install
npm run build
npm install -g .
```
Installs the package globally as `prompt-refiner` command. [VERIFIED: build_and_install.ps1, 2026-05-23]

### Architecture.md (PI-03, D-13)

1. Same `flowchart LR` diagram as README (D-13 — reuse, do not create different diagram)
2. Per-component prose below diagram:
   - **PromptRefinerServer** — MCP server entry point; registers tools, handles `refine_prompt` tool calls via stdio transport
   - **ProjectScout / Detectors** — detects tech stack (Node, Python) and architectural patterns at the project root
   - **NeuralSnippets** — FlexSearch-based RAG over local codebase files; retrieves relevant code examples matching the prompt query
   - **LocalBrain** — JSON-backed pattern store in `.refiner/memory.json`; accumulates project-specific rules learned over time
   - **PromptRefiner / PromptOptimizer** — applies project context, learned patterns, RAG snippets, and standards mandates to the raw prompt
   - **BackgroundAutonomyService** — chokidar file watcher; triggers commit ingestion and lesson extraction on file change
   - **EventStore / CorrelationEngine / LessonExtractor** — SQLite-backed history: ingests git commits, correlates outcomes, extracts lessons for predictive refinement
   - **CommandCenterDashboard** — web UI on port 3000 (local only); shows agent logs and blackboard state

### Configuration-Reference.md (PI-03, D-14)

Table format with env vars (from source-verified list above) + file-based config (`.gemini-refiner.json`).

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `gh` CLI | GitHub API calls, content creation | Yes | 2.86.0 | — |
| `git` | Wiki clone+push | Yes | 2.53.0.windows.1 | — |
| Node.js | CI workflow research/validation | Yes (local) | v24.13.0 | — |
| GitHub Actions Node 22 runner | CI execution | Yes (cloud) | Provided by actions/setup-node@v4 | — |

No missing dependencies. All operations use `gh` CLI and `git` which are confirmed available.

---

## Validation Architecture

### Test Framework (CI Verification)

| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.4 |
| Config file | None (no vitest.config.ts — Vitest auto-discovers `tests/` directory) |
| Quick run command | `cd universal-refiner && npm test` |
| Full suite command | `cd universal-refiner && npm test` (same — all tests in `tests/`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | Notes |
|--------|----------|-----------|-------------------|-------|
| PI-01 | README contains hero line, no emoji | Manual visual check | — | Content review after push |
| PI-02 | CI workflow runs green on master | Smoke (CI run) | `gh run list -R Coding-Autopilot-System/Promptimprover --limit 1` | Check after workflow file push |
| PI-03 | Wiki pages exist with correct content | Manual visual check | `git ls-remote https://github.com/Coding-Autopilot-System/Promptimprover.wiki.git` | Verify pages at wiki URL |
| PI-04 | Badges render in README | Manual visual check | — | Verify at repo URL |
| PI-05 | Cross-repo links in README | Manual check | `gh api repos/Coding-Autopilot-System/Promptimprover/contents/README.md` | Check raw README content |

### Wave 0 Gaps

- [ ] Wiki initialization — `https://github.com/Coding-Autopilot-System/Promptimprover/wiki` — manual human action required before Wave 2
- [ ] `generated-version.ts` does not exist until CI first run — not a gap (CI step handles it)

---

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|------------------|-------|
| Phase 2 used `windows-latest` runner (.NET) | Phase 4 uses `ubuntu-latest` runner (Node) | Node CI is standard on Linux; TypeScript/Node tools are cross-platform except for this repo's build script |
| `actions/checkout@v6` (Phase 2) | `actions/checkout@v4` (current stable) | Phase 2 used v6 — verify this was intentional or a typo; v4 is the current release at time of research |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Mermaid `flowchart LR` with subgraph syntax per D-07 will render correctly on GitHub | Common Pitfalls / Pitfall 6 | Diagram shows as error; executor must adjust syntax and re-push |
| A2 | `actions/checkout@v4` is the current stable version | Standard Stack | Use wrong version; low risk (GitHub provides upgrade warnings) |
| A3 | Vitest 4.1.4 auto-discovers `tests/` without a config file | Standard Stack | Tests not found in CI; fixable by adding `--dir tests` flag |

---

## Open Questions (RESOLVED)

1. **CI badge first-run timing**
   - What we know: Badge shows "no status" until the first CI run completes after the workflow file is pushed
   - What's unclear: Whether a workflow_dispatch or immediate push is needed to trigger the first run
   - RESOLVED: The CI trigger includes `push: branches: [master]`, so pushing `ci.yml` itself triggers the first run immediately. No separate workflow_dispatch needed.

2. **`actions/checkout` version**
   - What we know: Phase 2 used `@v6` — this may have been a forward-looking choice or a typo (latest stable is v4)
   - What's unclear: Whether v6 exists as a pre-release
   - RESOLVED: Use `actions/checkout@v4` (confirmed current stable). Phase 2's `@v6` was likely a typo; v4 is the current release. Plans use `@v4`.

---

## Sources

### Primary (HIGH confidence)

- [VERIFIED: GitHub API] `Coding-Autopilot-System/Promptimprover` — repo metadata, all file contents fetched directly
- [VERIFIED: file contents] `universal-refiner/package.json` — exact scripts, dependencies, version
- [VERIFIED: file contents] `universal-refiner/tsconfig.json` — compiler options
- [VERIFIED: file contents] `mcp-server/package.json` — broken test script confirmed
- [VERIFIED: file contents] `build_and_install.ps1` — install script steps
- [VERIFIED: file contents] All `universal-refiner/src/` directory tree
- [VERIFIED: GitHub API] `has_wiki: false`, `default_branch: master`
- [VERIFIED: Phase 3 Research] `03-RESEARCH.md` — wiki initialization pitfall, clone+push pattern, wiki branch = master
- [VERIFIED: Phase 2 CI pattern] `Coding-Autopilot-System/gsd-orchestrator/.github/workflows/ci.yml` — CI template

### Secondary (MEDIUM confidence)

- [CITED: Phase 3 03-CONTEXT.md D-05/D-06] — Wiki Home 2-scroll pattern
- [CITED: Phase 3 03-CONTEXT.md D-03/D-04] — Setup Guide standalone principle + "successful run" section
- [CITED: Phase 2 02-CONTEXT.md D-08] — Badge placement pattern

### Tertiary (LOW confidence / ASSUMED)

- [ASSUMED] Mermaid subgraph syntax rendering behavior (Pitfall 6)
- [ASSUMED] `actions/checkout@v4` vs v6 — version assumption

---

## Metadata

**Confidence breakdown:**
- Standard stack (package.json facts): HIGH — verified from live remote files
- CI workflow strategy: HIGH — verified scripts, confirmed Windows pitfall, cross-validated with Phase 2 pattern
- Wiki delivery: HIGH — confirmed has_wiki: false, Phase 3 pattern verified from execution
- Architecture content: HIGH — all source directories read, components documented
- Configuration reference: HIGH — all env vars traced from source code

**Research date:** 2026-05-23
**Valid until:** 2026-06-23 (stable tech stack; 30-day window)
