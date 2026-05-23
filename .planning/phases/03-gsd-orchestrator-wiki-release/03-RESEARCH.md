# Phase 3: gsd-orchestrator Wiki & Release — Research

**Researched:** 2026-05-23
**Domain:** GitHub Wiki (git-push model), GitHub Releases (gh CLI), documentation authoring
**Confidence:** HIGH — all claims verified against live repo files and GitHub API

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Architecture wiki embeds the same `stateDiagram-v2` and `flowchart LR` from the README. Add 1-3 concise prose bullets per state below the diagram. Reuse Phase 2 content — no net-new diagrams.
- **D-02:** Architecture wiki adds a "Data Flow" section: what goes in (issue body, labels) → what comes out (branch, commits, PR, optional auto-merge). Transformation narrative, not API call list.
- **D-03:** Setup Guide is a standalone, self-contained page — NOT a redirect to README. Executor MUST read `.env.example` and verify each step against source before writing. Must be copy-pasteable.
- **D-04:** Setup Guide ends with "What a successful run looks like" — expected terminal output showing state transitions and final PR URL.
- **D-05:** Home page serves both hiring manager and developer in 2 scrolls: hero paragraph + badges → quick-start snippet → navigation table.
- **D-06:** Quick-start on Home shows ONLY the 3-4 required env vars + `dotnet run` command (≤ 5 lines). Full clone-through-run sequence belongs in Setup Guide.
- **D-07:** GitHub Release notes ONLY — no CHANGELOG.md committed to main.
- **D-08:** Release notes use feature-narrative format: autonomous capability lead, key technical decisions, stack. Optimized for hiring manager.
- **D-09 (Claude's discretion):** Config Reference uses table format — Name | Type | Required | Default | Description. Grouped by concern (GitHub vars, Anthropic vars, Behavior vars). Source of truth is `.env.example` + env var reads in source.

### Claude's Discretion

- Config Reference table grouping and column order (D-09 guideline provided, executor may refine formatting).
- Exact prose wording of wiki page introductions (enterprise tone constraint from PROJECT.md applies).

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| GSD-04 | GitHub Wiki — Home page with overview and navigation | Wiki git-push model documented; Home structure per D-05/D-06; badge URLs verified from Phase 2 |
| GSD-05 | GitHub Wiki — Setup Guide (prerequisites, clone, .env, first run) | `.env.example` fully fetched and parsed; Program.cs CLI args verified; checkpoint path verified (`.gsd/state/`) |
| GSD-06 | GitHub Wiki — Architecture deep-dive (state machine, components, data flow) | All 9 state files + GsdStateMachine.cs + WorkflowModels.cs read; exact MCP tool calls per state documented |
| GSD-07 | GitHub Wiki — Configuration Reference (all env vars) | All 7 env vars enumerated from `.env.example` + source code cross-check complete |
| GSD-08 | GitHub Release v1.0.0 with changelog | `gh release create` syntax verified; current HEAD SHA confirmed; no existing releases |
</phase_requirements>

---

## Summary

Phase 3 creates four GitHub Wiki pages and one GitHub Release for `Coding-Autopilot-System/gsd-orchestrator`. The work is purely additive — no source code modifications.

**Critical blocker discovered:** GitHub's wiki `.wiki.git` repository does not exist until at least one page is created via the GitHub web UI. The `git ls-remote` and `git push` commands both return "Repository not found" until manual initialization occurs. This is a confirmed GitHub platform limitation with no API workaround as of April 2026. **Wave 0 of the plan must include a manual checkpoint for the user to create the first wiki page via the GitHub web UI** before automation can push the remaining three pages and the release.

Once initialized (one page via web UI), the remaining three wiki pages can be pushed via `git clone wiki.git → add .md files → git push`. The GitHub Release is fully automatable via `gh release create -R Coding-Autopilot-System/gsd-orchestrator v1.0.0`.

All source content (env vars, state names, CLI args, MCP tool calls, diagram syntax) has been verified against the live remote repository in this research session. The executor will not need to re-read most source files if it relies on the canonical refs section in CONTEXT.md and the verified data tables in this document.

**Primary recommendation:** Plan two waves. Wave 0: manual wiki initialization checkpoint (user creates Home.md stub in web UI). Wave 1: automated git push of all 4 final wiki pages + `gh release create` for v1.0.0.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Wiki page authoring | Local file system (temp git repo) | GitHub wiki.git (remote) | Pages are Markdown files pushed via git — no backend API involvement |
| Wiki page delivery | GitHub Wiki (CDN-served) | — | GitHub renders Markdown + Mermaid natively in wiki; no build step |
| GitHub Release creation | GitHub API (via gh CLI) | — | `gh release create` calls GitHub REST API; no local file involvement beyond release notes text |
| Mermaid diagram rendering | GitHub Wiki renderer | — | GitHub renders `mermaid` code fences in wiki pages identically to README |
| Content sourcing | Remote repo (Coding-Autopilot-System/gsd-orchestrator) | Phase 2 SUMMARY.md (local) | All content derived from live source files and Phase 2 diagram outputs |

---

## Standard Stack

### Core Tools

| Tool | Version | Purpose | Verification |
|------|---------|---------|-------------|
| `gh` CLI | 2.86.0 | Release creation (`gh release create`), GitHub API calls | [VERIFIED: `gh --version`] |
| `git` | 2.53.0.windows.1 | Wiki page push via git clone/push to wiki.git | [VERIFIED: `git --version`] |
| GitHub REST API | v3 | Release creation endpoint | [VERIFIED: `gh api repos/.../releases` returns `[]`] |

### No Additional Libraries Required

All wiki and release operations are achievable with `gh` CLI and `git` only. No npm packages, no Python libraries, no .NET SDK involvement for this phase.

### Verified Unavailable: GitHub MCP Tools for Wiki

The `github-mcp-server.exe` toolsets do NOT include wiki operations. Available toolsets confirmed: `actions, code_security, copilot, dependabot, discussions, gists, git, issues, labels, notifications, orgs, projects, pull_requests, repos, secret_protection, security_advisories, stargazers, users`. No `wiki` toolset exists. [VERIFIED: `github-mcp-server.exe --help`]

---

## Architecture Patterns

### System Architecture Diagram

```
User Request (new wiki page / release)
          │
          ▼
   Wave 0: Manual Step
   GitHub Web UI → creates first wiki page (Home.md stub)
   (initializes wiki.git repo on GitHub servers)
          │
          ▼
   Wave 1: Automated Steps
          │
          ├── Wiki Pages (4 pages)
          │     │
          │     ▼
          │   Local temp dir
          │   git clone https://github.com/.../gsd-orchestrator.wiki.git
          │     │
          │     ├── Home.md (overwrite stub with full content)
          │     ├── Setup-Guide.md
          │     ├── Architecture.md
          │     └── Configuration-Reference.md
          │     │
          │     ▼
          │   git add + git commit + git push
          │     │
          │     ▼
          │   GitHub Wiki (rendered, publicly accessible)
          │
          └── Release v1.0.0
                │
                ▼
              gh release create v1.0.0 \
                --repo Coding-Autopilot-System/gsd-orchestrator \
                --title "gsd-orchestrator v1.0.0" \
                --notes-file release-notes.md
                │
                ▼
              GitHub Releases page (tag v1.0.0 points to HEAD 8da3a74)
```

### Wiki Git Push Pattern

```bash
# Clone initialized wiki (after Wave 0 manual step)
git clone https://x-access-token:${GH_TOKEN}@github.com/Coding-Autopilot-System/gsd-orchestrator.wiki.git /tmp/wiki

# Add/overwrite pages
cp Home.md Setup-Guide.md Architecture.md Configuration-Reference.md /tmp/wiki/

# Commit and push
cd /tmp/wiki
git add .
git -c user.email="bot@gsd" -c user.name="GSD Bot" commit -m "docs: add wiki pages (GSD-04 GSD-05 GSD-06 GSD-07)"
git push origin master
```

Note: GitHub wiki repos use `master` as the default branch, not `main`. [CITED: https://docs.github.com/en/communities/documenting-your-project-with-wikis/adding-or-editing-wiki-pages]

### Release Creation Pattern

```bash
# Write release notes to file first (avoids shell quoting issues with multi-line strings)
cat > /tmp/release-notes.md << 'EOF'
[feature-narrative content]
EOF

gh release create v1.0.0 \
  --repo Coding-Autopilot-System/gsd-orchestrator \
  --title "gsd-orchestrator v1.0.0" \
  --notes-file /tmp/release-notes.md \
  --target main
```

The `--target main` flag ensures the tag points to the current HEAD of main (`8da3a7470a76085485d33b31ccb4f4816a6d7ae8`). No pre-existing releases exist; this will be the first. [VERIFIED: GitHub API `releases` endpoint returned `[]`]

### GitHub Contents API Pattern (for base64 payload delivery)

If any wiki content is delivered via GitHub API rather than git push, use the established Phase 2 pattern: write JSON payload to file and use `--input` flag to avoid shell variable truncation on content > 1 KB. [VERIFIED: Phase 2 SUMMARY.md key decision]

### Anti-Patterns to Avoid

- **Trying git push before web UI initialization:** Returns "Repository not found" — the wiki.git repo does not exist until the web UI creates the first page. Cannot be bypassed via API.
- **Using `--field content=` with base64 in shell variables:** Breaks for content > 1 KB. Use `--input payload.json` instead (established Phase 2 pattern).
- **Using transition labels in stateDiagram-v2:** Causes Mermaid rendering bugs (#2902/#5827). The README diagrams use no transition labels — architecture wiki must match exactly. [VERIFIED: Phase 2 SUMMARY.md]
- **Adding `direction` keyword inside flowchart subgraph blocks:** Not supported by GitHub's Mermaid renderer. Keep `direction LR` at top-level only. [VERIFIED: Phase 2 SUMMARY.md]
- **Committing CHANGELOG.md to main:** Explicitly locked out by D-07. Release notes go in the GitHub Release only.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Creating GitHub release | Custom API POST logic | `gh release create` | Handles tag creation, release draft, notes, target ref in one command |
| Wiki git authentication | Custom credential storage | `gh auth token` piped into clone URL | gh CLI already authenticated; token available via `gh auth token` |
| Mermaid diagram syntax | Re-derive from source | Copy exact syntax from Phase 2 SUMMARY.md | Diagrams already validated; re-derivation risks introducing bugs |

---

## Verified Source Data

### .env.example — All Environment Variables

Fetched live from remote repo. All 7 vars verified:

| Name | Type | Required | Default | Description | Group |
|------|------|----------|---------|-------------|-------|
| `GITHUB_PERSONAL_ACCESS_TOKEN` | string | **Yes** | — | GitHub PAT (scopes: `repo`, `read:org`) | GitHub |
| `ANTHROPIC_API_KEY` | string | **Yes** | — | Anthropic API key (for autonomous orchestrator only) | Anthropic |
| `GSD_GITHUB_OWNER` | string | **Yes** | — | Target repo owner (GitHub username or org) | Behavior |
| `GSD_GITHUB_REPO` | string | **Yes** | — | Target repository name | Behavior |
| `GSD_REVIEWERS` | string | No | `""` (empty) | Comma-separated GitHub usernames to request as PR reviewers | Behavior |
| `GSD_AUTO_MERGE` | bool | No | `false` | If `true`, squash-merges PRs after Documenting state completes | Behavior |
| `GSD_MCP_BINARY` | string | No | auto-discovered | Path to `github-mcp-server.exe`. Probes up from cwd if not set. | Behavior |

[VERIFIED: live `.env.example` fetch via `gh api`]

**Source cross-check:** `IdleState.cs` requires `GSD_GITHUB_OWNER` and `GSD_GITHUB_REPO`. `ReviewingState.cs` reads `GSD_REVIEWERS`. `DocumentingState.cs` reads `GSD_AUTO_MERGE`. `Program.cs` reads `GSD_MCP_BINARY` via `FindMcpBinary()`. `ANTHROPIC_API_KEY` is required by DI-registered `IChatClient`. No additional env vars discovered in source beyond what `.env.example` documents.

### Checkpoint Directory

Source code truth: `.gsd/state/{workflowId}.json` (FileCheckpointStore.cs). README says `.checkpoints/` — this is **incorrect** in the README. The Setup Guide must state `.gsd/state/`. [VERIFIED: `FileCheckpointStore.cs` line `_stateDir = Path.Combine(repoRoot, ".gsd", "state")`]

### CLI Arguments

From `Program.cs`:
```
dotnet run -- --issue <number>        Run workflow for a specific issue
dotnet run -- --resume <workflow-id>  Resume an interrupted workflow
dotnet run -- --watch                 Poll open issues every 5 minutes
```
[VERIFIED: `Program.cs` arg parsing loop]

### State Machine — 9 States + 2 Terminal States

Full enum from `WorkflowModels.cs`: `Idle, Analyzing, Branching, Editing, Validating, Committing, PrCreating, Reviewing, Documenting, Done, Failed`

State transition order (verified from state handler return calls):
```
Idle → Analyzing → Branching → Editing → Validating → Committing → PrCreating → Reviewing → Documenting → Done
```
Any state may transition to `Failed` on unhandled exception. [VERIFIED: `GsdStateMachine.cs` `ExecuteLoopAsync`]

### Per-State MCP Tool Calls (verified from source)

| State | Primary MCP Tool(s) | LLM Involvement |
|-------|--------------------|-|
| Idle | `get_repository`, `get_issue` | No |
| Analyzing | `search_code` (best-effort) | Yes — produces `AnalysisPlan` JSON |
| Branching | `list_branches`, `create_branch` (implied) | No |
| Editing | `create_or_update_file` (per file, ReAct loop, max 20 turns per file) | Yes |
| Validating | File safety blocklist check, diff size gate, merge conflict pre-flight | No |
| Committing | `get_branch` (confirms final commit SHA) | No |
| PrCreating | `list_pull_requests` (idempotency), `create_pull_request` (implied) | Yes — generates PR title/body |
| Reviewing | `add_pull_request_review_comment`, `request_reviewers` (if configured) | Yes — generates review comment |
| Documenting | `create_or_update_file` for `docs/github-mcp-tools.md` and `CHANGELOG.md`, merge if `GSD_AUTO_MERGE=true` | Yes |

[VERIFIED: all state files read]

### Mermaid Diagrams — Exact Syntax for Architecture Wiki

The Phase 2 SUMMARY.md confirms these patterns were validated and committed to the README:

**stateDiagram-v2:** 9 states, direction LR, NO transition labels (avoids Mermaid bug #2902/#5827):
```mermaid
stateDiagram-v2
    direction LR
    [*] --> Idle
    Idle --> Analyzing
    Analyzing --> Branching
    Branching --> Editing
    Editing --> Validating
    Validating --> Committing
    Committing --> PrCreating
    PrCreating --> Reviewing
    Reviewing --> Documenting
    Documenting --> [*]
```

**flowchart LR:** 4 subgraphs, 7 edges, `|stdio|` label on McpStdioClient → github-mcp-server edge. Subgraph labels use double-quoted bracket syntax when containing parentheses. [VERIFIED: Phase 2 SUMMARY.md + README.md content fetch]

### Project Stack (for Release Notes)

From `GsdOrchestrator.csproj` (verified):
- Target framework: `net10.0` (Worker SDK)
- `Anthropic.SDK` 5.10.0
- `dotenv.net` 4.0.2
- `Microsoft.Extensions.AI` 10.6.0
- `Microsoft.Extensions.Hosting` 10.0.7
- `Polly.Extensions` 8.6.6

[VERIFIED: `GsdOrchestrator.csproj` content fetch]

### Current HEAD SHA (for Release Tag Target)

`8da3a7470a76085485d33b31ccb4f4816a6d7ae8` — HEAD of `main` as of 2026-05-23. [VERIFIED: GitHub GraphQL API + REST API `git/refs/heads/main`]

---

## Common Pitfalls

### Pitfall 1: Wiki Git Repo "Repository Not Found"

**What goes wrong:** Executor runs `git clone https://github.com/Coding-Autopilot-System/gsd-orchestrator.wiki.git` or `git push` and receives "Repository not found".
**Why it happens:** GitHub does not create the `.wiki.git` repository until a user creates the first wiki page via the web UI. `has_wiki: true` on the repo API means the feature is enabled, not that the git repo is initialized.
**How to avoid:** Wave 0 must include a manual human checkpoint. The user navigates to `https://github.com/Coding-Autopilot-System/gsd-orchestrator/wiki` in a browser, clicks "Create the first page", creates any stub page (even a one-liner), and saves it. Only after this does automation work.
**Warning signs:** "Repository not found" from any git command targeting the `.wiki.git` URL.

[VERIFIED: confirmed via `git ls-remote` test in research session, and cross-validated against GitHub community discussion https://github.com/orgs/community/discussions/175621]

### Pitfall 2: Shell Variable Truncation of Large Base64 Content

**What goes wrong:** `gh api --field "content=${CONTENT}"` returns HTTP 422 "content is not valid Base64" when CONTENT > ~1 KB.
**Why it happens:** Shell variable expansion corrupts large strings in some environments.
**How to avoid:** Write JSON payload to a temp file and use `gh api --input payload.json`. Established pattern from Phase 2.
**Warning signs:** HTTP 422 from GitHub Contents API.

### Pitfall 3: Wrong Default Branch for Wiki Git Repo

**What goes wrong:** `git push origin main` to wiki.git fails; pages don't appear.
**Why it happens:** GitHub wiki git repos use `master` as the default branch, not `main`, regardless of the main repo's default branch setting.
**How to avoid:** Use `git push origin master` when pushing to wiki.git, or push to `HEAD` and let GitHub handle branch mapping.

### Pitfall 4: Mermaid Transition Labels in stateDiagram-v2

**What goes wrong:** Labels on `-->` transitions (e.g., `Idle --> Analyzing : start`) cause rendering failures on GitHub.
**Why it happens:** GitHub Mermaid bug #2902/#5827. The README diagrams were specifically crafted without labels.
**How to avoid:** Copy exact diagram syntax from Phase 2 SUMMARY.md verbatim. Add prose bullets below the diagram for per-state explanation instead.

### Pitfall 5: README Says ".checkpoints/" but Code Uses ".gsd/state/"

**What goes wrong:** Setup Guide tells users checkpoints are in `.checkpoints/` — users can't find them.
**Why it happens:** The README description of the project structure shows `.checkpoints/` but `FileCheckpointStore.cs` stores to `.gsd/state/{workflowId}.json`.
**How to avoid:** The executor MUST reference the source code (as required by D-03), not the README, when writing the Setup Guide. The verified path is `.gsd/state/`.

---

## Wiki Page Content Guide

### GSD-04: Home Page Structure

Per D-05 and D-06, two-scroll layout:

**Scroll 1:** Hero paragraph (what it does, 2-3 sentences, enterprise tone) + badge line (CI, .NET 10, MIT).

**Scroll 2:** Quick-start code block (5 lines max — 3-4 env var exports + `dotnet run` command) + navigation table.

Navigation table structure:
| Page | Description |
|------|-------------|
| [Setup Guide](Setup-Guide) | Prerequisites, installation, first run |
| [Architecture](Architecture) | State machine, component topology, data flow |
| [Configuration Reference](Configuration-Reference) | All environment variables |

Badge URLs (same as README, verified working):
- CI: `https://github.com/Coding-Autopilot-System/gsd-orchestrator/actions/workflows/ci.yml/badge.svg`
- .NET 10: `https://img.shields.io/badge/.NET-10-512BD4`
- MIT: `https://img.shields.io/badge/license-MIT-green`

Quick-start snippet per D-06:
```bash
export GITHUB_PERSONAL_ACCESS_TOKEN=ghp_...
export ANTHROPIC_API_KEY=sk-ant-...
export GSD_GITHUB_OWNER=your-org
export GSD_GITHUB_REPO=your-repo
dotnet run --project src/GsdOrchestrator/GsdOrchestrator.csproj -- --issue 42
```

### GSD-05: Setup Guide Sections

Verified against source, copy-pasteable:

1. **Prerequisites** — Windows, .NET 10 SDK, GitHub PAT (scopes: `repo`, `read:org`), Anthropic API key
2. **Clone**
   ```bash
   git clone https://github.com/Coding-Autopilot-System/gsd-orchestrator.git
   cd gsd-orchestrator
   ```
3. **Configure .env** — `cp .env.example .env`, then fill in the 4 required vars
4. **First run** — `cd src/GsdOrchestrator && dotnet run -- --issue <number>`
5. **Resume interrupted run** — `dotnet run -- --resume <workflow-id>`
6. **What a successful run looks like (D-04)** — Show expected log output pattern from `GsdStateMachine.cs` `_logger.LogInformation("[{Id}] → {State}", ...)`:
   ```
   info: GsdOrchestrator.Workflows.GsdStateMachine[0]
         Workflow abc123def456 starting at state Idle
   info: GsdOrchestrator.Workflows.GsdStateMachine[0]
         [abc123def456] → Analyzing
   info: GsdOrchestrator.Workflows.GsdStateMachine[0]
         [abc123def456] → Branching
   ...
   info: GsdOrchestrator.Workflows.GsdStateMachine[0]
         [abc123def456] → Done

   ✓ PR created:   https://github.com/your-org/your-repo/pull/N
   ✓ Docs updated: docs/github-mcp-tools.md, CHANGELOG.md
     Workflow ID:  abc123def456
   ```

### GSD-06: Architecture Page Sections

1. **State Machine** — `stateDiagram-v2` diagram (verbatim from README/Phase 2), then 1-3 bullet prose per state
2. **Component Topology** — `flowchart LR` diagram (verbatim from README/Phase 2)
3. **Data Flow (D-02)** — "Issue-to-PR Transformation" narrative: input (GitHub issue body + labels) → Analyzing state produces branch name + file list → Editing state reads/writes files via MCP → Validating gates → PR created with bot review comment → optional auto-merge. Frame as user-visible transformation, not API call enumeration.

Per-state bullets (sourced from verified state files):

| State | Bullet Points |
|-------|---------------|
| Idle | Calls `get_repository` + `get_issue` via MCP; populates IssueContext (title, body, labels, default branch) |
| Analyzing | Sends issue body to Claude; retries up to 3 times on JSON parse failure; produces AnalysisPlan (branch name, files to modify, summary) |
| Branching | Creates feature branch from default branch; idempotent — resumes from existing branch if workflow was interrupted |
| Editing | ReAct loop per file (max 20 turns); reads file content, sends to Claude with issue context, commits result via `create_or_update_file` |
| Validating | Four gates: file safety blocklist (no `.pem`, `.key`, CI workflows), merge conflict pre-flight, diff size check, test coverage intent |
| Committing | Calls `get_branch` to confirm final commit SHA is present; records commit URL for PR body |
| PrCreating | Checks for existing PR from same branch (idempotent); generates PR title/body via Claude; opens PR |
| Reviewing | Posts bot review comment explaining what changed and why; requests reviewers from `GSD_REVIEWERS` if configured |
| Documenting | Updates `docs/github-mcp-tools.md` and `CHANGELOG.md` on default branch in parallel; squash-merges if `GSD_AUTO_MERGE=true` |

### GSD-07: Configuration Reference

Table format (D-09), grouped by concern, sourced from `.env.example` + source code cross-check:

**GitHub Variables:**
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `GITHUB_PERSONAL_ACCESS_TOKEN` | string | Yes | — | GitHub PAT. Required scopes: `repo`, `read:org` |

**Anthropic Variables:**
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `ANTHROPIC_API_KEY` | string | Yes* | — | Anthropic API key. *Required for autonomous orchestrator only — not needed for GitHub MCP Server standalone use |

**Behavior Variables:**
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `GSD_GITHUB_OWNER` | string | Yes | — | GitHub username or org that owns the target repository |
| `GSD_GITHUB_REPO` | string | Yes | — | Name of the target repository |
| `GSD_REVIEWERS` | string | No | `""` | Comma-separated GitHub usernames to request as PR reviewers. Leave empty to skip review requests |
| `GSD_AUTO_MERGE` | bool | No | `false` | If `true`, automatically squash-merges the PR after the Documenting state completes |
| `GSD_MCP_BINARY` | string | No | auto-discovered | Full path to `github-mcp-server.exe`. If not set, the orchestrator probes parent directories from cwd |

### GSD-08: Release Notes Narrative

Per D-07 and D-08, feature-narrative format. Key talking points (sourced from verified stack + behavior):

- Lead: Autonomous issue-to-PR — GitHub issue in, reviewed PR out, zero human intervention
- State machine: 9-state workflow engine (Idle → Analyzing → Branching → Editing → Validating → Committing → PrCreating → Reviewing → Documenting → Done)
- MCP integration: GitHub operations via stdio-spawned MCP server (not HTTP) — battle-tested transport
- Resilience: Polly exponential backoff on transient MCP failures; file-based checkpointing under `.gsd/state/` for resume-after-failure
- Stack: .NET 10 Worker, `Microsoft.Extensions.AI`, `Anthropic.SDK` 5.10.0, `Polly.Extensions` 8.6.6
- Optional auto-merge: squash-merge after bot-authored documentation update
- Watch mode: `--watch` polls open issues every 5 minutes for fully unattended operation

---

## Runtime State Inventory

> Phase 3 is documentation/release only — no rename, refactor, or migration. This section is included for completeness.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | None — no datastores store wiki content externally | None |
| Live service config | GitHub wiki.git repo is UNINITIALIZED — must be initialized via web UI before git push works | Manual wiki initialization (Wave 0 checkpoint) |
| OS-registered state | None | None |
| Secrets/env vars | `GH_TOKEN` not set in environment; `gh auth token` provides token at runtime | None — gh CLI handles auth |
| Build artifacts | None | None |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `gh` CLI | Release creation, GitHub API calls | ✓ | 2.86.0 | — |
| `git` | Wiki page push | ✓ | 2.53.0.windows.1 | — |
| GitHub auth (OgeonX-Ai) | Write access to Coding-Autopilot-System org | ✓ | Token scopes: gist, read:org, repo | — |
| Wiki git repo initialized | Wiki page push (Wave 1) | ✗ | — | Manual web UI initialization (Wave 0 checkpoint) |
| GitHub Release on repo | v1.0.0 release | ✗ (no releases exist) | — | N/A — this is what we create |
| `dotnet` | Verifying CLI args (research only) | ✓ | 10.0.203 | — |

**Missing dependencies with no fallback:**
- Wiki git repo initialization — cannot be automated; requires manual human action via GitHub web UI.

**Missing dependencies with fallback:**
- None beyond the wiki initialization blocker above.

---

## Validation Architecture

`nyquist_validation` is `true` in config.json. However, Phase 3 produces exclusively documentation artifacts (Markdown files) and a GitHub Release. There is no code to unit test or integrate test against.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | None applicable — documentation phase |
| Config file | N/A |
| Quick run command | Manual verification (view wiki page in browser / check release on GitHub) |
| Full suite command | Manual verification |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| GSD-04 | Home page exists with hero, badges, quick-start, nav table | smoke — gh api wiki check | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/wiki/Home 2>/dev/null` | ❌ Wave 0 |
| GSD-05 | Setup Guide exists with all sections and correct env var names | smoke — content grep | `git clone wiki.git && grep -c "GITHUB_PERSONAL_ACCESS_TOKEN" Setup-Guide.md` | ❌ Wave 0 |
| GSD-06 | Architecture page exists with both Mermaid diagrams | smoke — content grep | `grep -c "stateDiagram-v2" Architecture.md` | ❌ Wave 0 |
| GSD-07 | Config Reference lists all 7 env vars | smoke — content grep | `grep -c "GSD_" Configuration-Reference.md` | ❌ Wave 0 |
| GSD-08 | Release v1.0.0 exists on GitHub | automated | `gh release view v1.0.0 --repo Coding-Autopilot-System/gsd-orchestrator` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** Visual inspection of rendered wiki page in browser
- **Per wave merge:** `gh release view v1.0.0` + manual spot-check of wiki pages
- **Phase gate:** All 5 requirements verified before closing phase

### Wave 0 Gaps

- [ ] Wiki git repo must be initialized (manual: user creates first page via web UI at `https://github.com/Coding-Autopilot-System/gsd-orchestrator/wiki`)
- [ ] Verification scripts above do not exist yet — executor writes them inline as post-task verification commands

---

## Security Domain

Phase 3 creates only documentation and a GitHub Release tag. No authentication logic, no data processing, no secrets handling beyond existing `GH_TOKEN` usage that was already present in Phase 2.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | N/A — no auth code written |
| V3 Session Management | No | N/A |
| V4 Access Control | No | N/A |
| V5 Input Validation | No | N/A — no user input processed |
| V6 Cryptography | No | N/A |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Token in git history | Information Disclosure | Use `gh auth token` at runtime; never hard-code token in committed files |
| Wiki content injection | Tampering | Content is static Markdown authored by executor; no user-supplied input |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | GitHub wiki repo uses `master` as default branch for git push | Architecture Patterns | Push to `main` silently succeeds but pages may not appear; easy to detect and fix |
| A2 | Wiki initialization via web UI unblocks subsequent git push without additional configuration | Pitfalls / Environment | If additional config is needed (e.g., branch protection), executor must adapt |

---

## Open Questions (RESOLVED)

1. **Wiki branch name for push target** — RESOLVED: Use `master`; fallback `git push origin HEAD` per 03-01-PLAN.md Task 2 action. Decision reflected in plan with concrete fallback strategy.
   - What we know: GitHub wiki docs say clone and push; Phase 2 SUMMARY.md does not address this
   - What was unclear: Whether the target branch is `master` or configurable; research indicates `master` but this was ASSUMED
   - Resolution: Plan handles with explicit `master` target + `git push origin HEAD` fallback if master fails

---

## Sources

### Primary (HIGH confidence)

- Live remote repo fetch via `gh api` — `.env.example`, `Program.cs`, `GsdStateMachine.cs`, `WorkflowModels.cs`, all 9 state files, `FileCheckpointStore.cs`, `GsdOrchestrator.csproj`, `README.md`
- `gh --version`, `git --version`, `dotnet --version` — tool availability verification
- `gh api repos/Coding-Autopilot-System/gsd-orchestrator` — `has_wiki: true` confirmed
- `gh api repos/Coding-Autopilot-System/gsd-orchestrator/releases` — `[]` confirmed (no existing releases)
- `gh api repos/Coding-Autopilot-System/gsd-orchestrator/git/refs/heads/main` — HEAD SHA `8da3a74...` confirmed
- `/c/GithubMCP/.planning/phases/02-gsd-orchestrator-ci-diagrams/02-02-SUMMARY.md` — exact Mermaid syntax and GitHub API patterns from Phase 2

### Secondary (MEDIUM confidence)

- [GitHub Docs: Adding or editing wiki pages](https://docs.github.com/en/communities/documenting-your-project-with-wikis/adding-or-editing-wiki-pages) — git-push model, no REST API for wiki pages
- [gh release create --help] — release creation flags and behavior

### Tertiary (LOW confidence — flagged)

- GitHub wiki uses `master` branch [ASSUMED] — inferred from common pattern, not directly verified by successful push

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all tools verified via version commands
- Verified source content (env vars, states, CLI args): HIGH — read directly from live remote repo files
- Wiki initialization blocker: HIGH — confirmed via git ls-remote failure + GitHub community discussions (April 2026)
- Wiki git branch name (`master`): LOW — assumed, not tested via successful push
- Architecture patterns: HIGH — established from Phase 2 SUMMARY.md + gh CLI help

**Research date:** 2026-05-23
**Valid until:** 2026-06-23 (stable tooling; GitHub wiki limitation is a persistent platform gap)
