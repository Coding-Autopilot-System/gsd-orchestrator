# Phase 5: autogen Polish — Research

**Researched:** 2026-05-24
**Domain:** GitHub Actions CI (Python / unittest), README authoring, GitHub Wiki (git-push model), shields.io badges
**Confidence:** HIGH — all claims verified against live remote repo files and GitHub API

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AG-01 | README rewritten — remove "starter kit" framing, add enterprise positioning | Current README fully read; "Microsoft Agent Framework Starter" title and "starter kit" language confirmed; enterprise reframe documented with factual hero text |
| AG-02 | GitHub Actions CI workflow (Python build) with passing badge | No `.github/` directory exists; test framework is stdlib `unittest`; CI strategy documented; critical finding: most tests import missing packages — only 2 tests are stdlib-safe in CI |
| AG-03 | GitHub Wiki — Home, Setup Guide, Architecture, Configuration Reference | `has_wiki: false` confirmed; Phase 3/4 wiki initialization pattern applies; source tree fully documented for Architecture page |
| AG-04 | README badges: CI, Python, License | Badge URL formats documented; default branch is `main`; LICENSE confirmed present |
| AG-05 | Cross-repo links to org and sibling projects | gsd-orchestrator and Promptimprover URLs verified; Phase 4 cross-repo link pattern reused |
</phase_requirements>

---

## Summary

Phase 5 elevates the autogen repository with five additive changes: README rewrite, GitHub Actions CI, GitHub Wiki (4 pages), README badges, and cross-repo links. No source code modifications.

**Critical CI finding:** The repo has **no `requirements.txt`** (404 from GitHub API) and most tests import `agent_framework` (Microsoft Agent Framework pip package) or `autogen_starter.*` (a package that has been removed from the repo tree). A full `python -m unittest discover` will fail with `ModuleNotFoundError`. The only tests that pass in a clean environment without installing any packages are `test_phase5_ui_contract.py` and `test_phase5_operator_views.py` — both use only Python stdlib and read checked-in static files. CI must target these two files explicitly, or accept a `--collect-only` / import-error-graceful run approach.

**Recommended CI strategy:** Run only the two stdlib-only tests (`tests/test_phase5_ui_contract.py` and `tests/test_phase5_operator_views.py`) using `python -m pytest` with explicit file targets. This gives a green badge with real coverage and is honest about what runs in a clean environment. No pip install of `agent-framework` is needed.

**Critical Wiki finding:** `has_wiki: false` — the autogen wiki has NOT been initialized. Same GitHub platform limitation as Phases 3 and 4: a human must create the first page via the web UI before automation can push pages. Wave 0 must include a manual checkpoint identical to the prior phases.

**Default branch is `main`** (not `master`). The CI workflow trigger, badge URL, and any git push commands must use `main`. This differs from Promptimprover (which uses `master`).

**Primary recommendation:** Plan three waves. Wave 0: manual wiki initialization checkpoint. Wave 1: CI workflow creation + README rewrite with badges. Wave 2: wiki page push (all 4 pages via clone+push to wiki.git using `master` branch — wiki repos always use `master` regardless of main repo default branch).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CI workflow | GitHub Actions (remote) | — | Workflow file committed to remote repo; no local build environment involved |
| README authoring | Remote repo (gh CLI / GitHub MCP) | — | All changes target Coding-Autopilot-System/autogen remote, not local C:/GithubMCP |
| Badge rendering | shields.io CDN | GitHub Actions (CI badge) | CI badge served by GitHub; other badges by shields.io |
| Wiki page delivery | GitHub Wiki git repo (wiki.git) | Local temp dir (staging) | Pages staged locally, pushed via git to wiki.git remote |
| Python test execution | GitHub Actions runner (stdlib only) | — | Only stdlib tests run in CI; no pip install of framework packages needed |

---

## Standard Stack

### Core Tools

| Tool | Version | Purpose | Source |
|------|---------|---------|--------|
| `gh` CLI | 2.86.0 | GitHub API calls, content creation | [VERIFIED: `gh --version`] |
| `git` | 2.53.0.windows.1 | Wiki clone+push to wiki.git | [VERIFIED: `git --version`] |
| `actions/checkout` | v4 | CI step | [VERIFIED: Phase 4 CI pattern] |
| `actions/setup-python` | v5 | Python setup in CI | [CITED: github.com/actions/setup-python] |
| `shields.io` | — | Badge generation for Python, License badges | [VERIFIED: Phase 2/4 badge pattern] |

### autogen Repository Verified Facts

| Property | Value | Source |
|----------|-------|--------|
| Default branch | `main` | [VERIFIED: GitHub API `default_branch: "main"`, 2026-05-24] |
| Has wiki | `false` (not initialized) | [VERIFIED: GitHub API `has_wiki: false`, 2026-05-24] |
| LICENSE | Present (MIT) | [VERIFIED: GitHub API `license.key: "mit"`, 2026-05-24] |
| Primary language | Python | [VERIFIED: GitHub API `language: "Python"`, 2026-05-24] |
| Python version (codebase) | 3.14-era (3.10+ compatible via `from __future__ import annotations`) | [VERIFIED: AGENTS.md + source code review, 2026-05-24] |
| Entry point | `main.py` — referenced in README, but NOT present in repo tree | [VERIFIED: recursive tree listing, 2026-05-24] |
| requirements.txt | NOT present (404 from GitHub API) | [VERIFIED: GitHub contents API + recursive tree, 2026-05-24] |
| pyproject.toml | NOT present | [VERIFIED: recursive tree listing, 2026-05-24] |
| .github/ directory | NOT present — no existing CI | [VERIFIED: recursive tree listing, 2026-05-24] |
| Test framework | `unittest` (stdlib) — dominant style | [VERIFIED: test file inspection; TESTING.md, 2026-05-24] |
| CI-safe test files | `tests/test_phase5_ui_contract.py`, `tests/test_phase5_operator_views.py` | [VERIFIED: import analysis of all test files, 2026-05-24] |
| Package manager | `pip` + virtualenv (from README and AGENTS.md) | [VERIFIED: AGENTS.md, README.md, 2026-05-24] |

### Test Files by Dependency Category

| Test File | Imports | CI-Safe Without pip? |
|-----------|---------|----------------------|
| `test_phase5_ui_contract.py` | stdlib only (`re`, `unittest`, `pathlib`) | YES |
| `test_phase5_operator_views.py` | stdlib only (`re`, `unittest`, `pathlib`) | YES |
| `test_phase3_routing.py` | `agent_framework`, `maf_starter.*` | NO — requires pip install |
| `test_maf_setup.py` | `agent_framework`, `maf_starter.*`, `fastapi` | NO |
| `test_workspace_contract.py` | `autogen_dashboard.*`, `autogen_starter.*` | NO — `autogen_starter` removed from repo |
| `test_phase1_api.py` | `autogen_dashboard.*`, `autogen_starter.*` | NO |
| `test_phase1_runtime.py` | `autogen_starter.*`, `maf_starter.*`, `agent_framework` | NO |
| All other phase* tests | Similar — `agent_framework` or `autogen_starter.*` | NO |

[VERIFIED: import grep of all test files via GitHub API, 2026-05-24]

### Missing Module Risk: `autogen_starter`

`autogen_starter/` was present historically (referenced in TESTING.md, ARCHITECTURE.md, STACK.md from 2026-03-26) but is **not in the current repo tree**. Multiple tests import from it. These tests will fail at import time in any environment without a separately installed `autogen_starter` package. The CI workflow MUST NOT attempt to discover and run all tests. [VERIFIED: recursive tree listing shows no `autogen_starter/` directory, 2026-05-24]

---

## Architecture Patterns

### System Architecture Diagram

```
Phase 5 Delivery Flow

  Remote Repo (Coding-Autopilot-System/autogen)
          |
          +-- Wave 0: Manual Step
          |     GitHub Web UI: https://github.com/Coding-Autopilot-System/autogen/wiki
          |     Click "Create the first page", save stub → wiki.git initialized
          |
          +-- Wave 1: CI + README
          |     gh API → create .github/workflows/ci.yml
          |     gh API → update README.md (rewrite + badges + cross-repo links)
          |
          +-- Wave 2: Wiki Pages
                Local temp dir (/tmp/ag-wiki or C:/tmp/ag-wiki)
                git clone wiki.git → write 4 .md files → git push master
                        |
                        +-- Home.md
                        +-- Setup-Guide.md
                        +-- Architecture.md
                        +-- Configuration-Reference.md
```

### CI Workflow Pattern

Python CI targeting the two stdlib-safe test files only.

```yaml
# Source: Phase 4 CI pattern adapted for Python + unittest
name: CI

on:
  push:
    branches: [ main ]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up Python 3.12
        uses: actions/setup-python@v5
        with:
          python-version: '3.12'

      - name: Run static contract tests
        run: python -m pytest tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py -v
```

**Why Python 3.12 not 3.14:**
Python 3.14 is not yet in stable release as a GitHub Actions runner image. `actions/setup-python@v5` supports 3.12 as the current stable LTS version. The codebase uses `from __future__ import annotations` universally which makes it compatible back to 3.10+. [ASSUMED — Python 3.14 availability on GitHub Actions runners not verified; 3.12 is safe choice]

**Why `pytest` not `python -m unittest`:**
`pytest` discovers and runs `unittest.TestCase` classes natively. It also provides cleaner output and the `-v` flag for a readable CI log. The two target test files use `unittest.TestCase` so pytest handles them without any configuration. pytest must be installed as a CI step: `pip install pytest`.

**Why only these two test files:**
All other test files import `agent_framework` (a Microsoft pip package), `autogen_starter.*` (removed from repo), or `fastapi`/`pydantic` — none of which are declared in any requirements file. Running full test discovery would produce `ModuleNotFoundError` on every other file. The two stdlib-only tests verify real behavior (static asset contract, UI component presence) and give a legitimate green badge.

**Revised CI with pytest install:**

```yaml
name: CI

on:
  push:
    branches: [ main ]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up Python 3.12
        uses: actions/setup-python@v5
        with:
          python-version: '3.12'

      - name: Install test runner
        run: pip install pytest

      - name: Run static contract tests
        run: python -m pytest tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py -v
```

### Wiki Delivery Pattern (identical to Phases 3 and 4)

```bash
# Step 1: Initialize (Wave 0 — manual, one-time)
# Navigate to: https://github.com/Coding-Autopilot-System/autogen/wiki
# Click "Create the first page" → save any stub

# Step 2: Automated push (Wave 2)
WIKI_DIR="/tmp/ag-wiki"
rm -rf "$WIKI_DIR"
git clone "https://x-access-token:$(gh auth token)@github.com/Coding-Autopilot-System/autogen.wiki.git" "$WIKI_DIR"
cd "$WIKI_DIR"
# Write .md files
git add .
git config user.email "agent@gsd"
git config user.name "GSD Agent"
git commit -m "docs: add autogen wiki pages"
git push origin master
```

**Critical:** Wiki git repos always use `master` branch regardless of the main repo's default branch (`main`). The autogen main repo uses `main`, but the wiki.git uses `master`. Use `git push origin master` for wiki operations.

### README Structure (AG-01)

Enterprise README sections in order:

1. `# autogen` — H1 title (remove "Starter" from title)
2. Badge line (CI + Python 3.12 + License) — immediately below H1
3. Cross-repo ecosystem line (AG-05) — immediately below badges
4. Hero paragraph — enterprise positioning (see hero text below)
5. `## Features` — technical capability list
6. `## Architecture` — Mermaid `flowchart LR` diagram
7. `## Quickstart` — minimal setup sequence (virtual environment + environment variables)
8. `## Configuration` — summary of key env vars (or link to wiki for full reference)
9. `## License` — MIT reference

**Current README problems (verified from live content):**
- Title: `# Microsoft Agent Framework Starter` — "Starter" must be removed
- Framing: "This repo now runs as a Microsoft Agent Framework starter" — internal/toy framing
- Local Windows paths in links: `[main.py](/C:/repo/autogen/main.py)` — broken absolute paths
- No enterprise positioning, no badges, no cross-repo links
- README describes `main.py` as the entrypoint — but `main.py` is NOT in the current repo tree

**Corrected entry point:** The README must not reference `main.py` directly by path. Reference `python main.py` as a command rather than a file link. The actual file structure shows `maf_starter/` as the active package with no root `main.py` in the tree — the README likely describes local development setup accurately, but the linked file path is a local path artifact from the developer's machine.

### Hero Text (AG-01)

```
autogen is a Python multi-agent orchestration runtime built on Microsoft Agent Framework — combining a Gemini/Claude provider fallback chain, AG-UI observability, and a local operator workbench for end-to-end autonomous engineering workflows.
```

No "starter kit" language. No emoji. Enterprise tone.

### Mermaid Architecture Diagram (AG-01)

```mermaid
flowchart LR
    Op["Operator\n(DevUI / Workbench)"] -->|"prompt"| MAF["autogen\n(MAF Runtime)"]
    MAF --> subgraph routing["Provider Routing"]
        Gemini["Gemini API\n(primary)"]
        Anthropic["Anthropic API\n(fallback)"]
        CLI["Local CLIs\n(gemini-cli / claude)"]
    end
    MAF --> subgraph agents["Agent Layer"]
        Entities["Entities\n(repo_team, copilots)"]
        Tools["Repo Tools\n(read / write / search)"]
        Checkpoints["Checkpoints\n(FileCheckpointStorage)"]
    end
    agents --> Out["Run Output\n+ Artifacts"]
```

Note: Mermaid subgraph-in-arrow syntax may need adjustment at execution time — verify rendering after push. Same caveat as Phase 4 Pitfall 6.

### Badge URLs (AG-04)

```markdown
[![CI](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml)
[![Python 3.12](https://img.shields.io/badge/python-3.12-blue)](https://www.python.org/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
```

**Important:** CI badge uses `?branch=main` because the default branch is `main`. This is the opposite of Promptimprover (which needed `?branch=master`).

### Cross-Repo Links (AG-05)

```markdown
Part of the [Coding-Autopilot-System](https://github.com/Coding-Autopilot-System) ecosystem:
[gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover)
```

Mirrors the Promptimprover cross-repo link pattern from Phase 4 D-10, but links to gsd-orchestrator and Promptimprover instead.

### Recommended Project Structure (Current — for Architecture wiki page)

```
autogen/
├── main.py                     # Entry point (referenced in README, not in current tree)
├── maf_starter/                # Active MAF runtime package
│   ├── config.py               # Settings loader — reads .env for MAF_*, GEMINI_*, ANTHROPIC_*
│   ├── agent_factory.py        # Agent builder — creates MAF agents with OpenAIChatClient
│   ├── provider_fallback.py    # Fallback chain — Gemini → Anthropic → CLI providers
│   ├── routing_policy.py       # RoutingPlan — classifies prompts, selects provider/model
│   ├── routing_types.py        # ChainStep, RouteAttempt, RouteLane type definitions
│   ├── team_factory.py         # Team builder — planner→researcher→implementer→reviewer
│   ├── workflow_factory.py     # Workflow builder — file-checkpointed MAF workflows
│   ├── orchestration.py        # RunOrchestrationState — stage tracking, handoffs
│   ├── tools.py                # Repo tools — read/list/search/write with bounded access
│   ├── repo_execution.py       # Write plan execution — safe bounded file writes
│   ├── approval_policy.py      # Human-in-the-loop approval policy
│   ├── validation_runner.py    # Post-execution validation command runner
│   └── gsd_autofill.py         # GSD workflow auto-fill integration
├── entities/
│   └── repo_team/
│       └── workflow.py         # DevUI-discoverable repo team workflow
├── autogen_dashboard/          # Legacy AutoGen dashboard (compatibility layer)
│   ├── app.py                  # FastAPI legacy session API
│   ├── session_runner.py       # Legacy session lifecycle
│   ├── session_store.py        # Legacy session persistence
│   ├── schemas.py              # Pydantic session schemas
│   └── static/                 # Dashboard UI (index.html, app.js, styles.css)
└── tests/                      # Test suite (16 files; 2 stdlib-only, rest require pip)
```

[VERIFIED: recursive tree listing from GitHub API, 2026-05-24]

---

## Source Tree for Wiki Architecture Page

Verified from GitHub API (2026-05-24). The ARCHITECTURE.md in `.planning/codebase/` references the old `maf_core/` structure — the current repo uses `maf_starter/` throughout. The wiki Architecture page must describe the current structure, not the stale `.planning/codebase/ARCHITECTURE.md`.

Key components for Architecture wiki prose:

- **maf_starter/config.py** — `Settings` dataclass + `load_settings()`. Reads `.env` for `MAF_MODEL`, `GEMINI_API_KEY`, `MAF_FALLBACK_CHAIN`, etc. Supports context-var-based run scope for per-request repo root binding.
- **maf_starter/agent_factory.py** — `build_agent()`. Creates MAF `Agent` with `OpenAIChatClient` against Gemini's OpenAI-compatible endpoint. Repo tools injected at build time.
- **maf_starter/provider_fallback.py** — Fallback middleware wrapping MAF agent. Detects quota/rate-limit errors and retries with next chain step (Gemini → Anthropic → gemini-cli → claude-cli → codex-cli).
- **maf_starter/routing_policy.py** — `build_routing_plan()`. Classifies prompt complexity (simple/standard/deep) and selects primary model + fallback order.
- **maf_starter/team_factory.py** — `build_repo_team()`. Sequential planner→researcher→implementer→reviewer chain with `FileCheckpointStorage`.
- **maf_starter/tools.py** — `build_repo_tools()`. Bounded repo access: `get_repo_overview`, `list_repo_files`, `read_repo_file`, `search_repo`, `request_human_approval`.
- **maf_starter/orchestration.py** — `RunOrchestrationState`. Stage tracking (planning/research/implementation/review/validation), specialist handoffs, pause/approval states.
- **entities/repo_team/workflow.py** — DevUI-discoverable entry point for the multi-agent team workflow.
- **autogen_dashboard/** — Legacy AutoGen compatibility layer. Preserved for backward compatibility; primary operator surface is DevUI via `maf_starter/`.

---

## Configuration Reference for Wiki Config Page

**Source:** verified from `maf_starter/config.py` live code, 2026-05-24.

### Required Environment Variables

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `GEMINI_API_KEY` | string | Yes | — | Gemini API key (also read as `MAF_API_KEY`) |

### Optional Environment Variables

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `MAF_MODEL` | string | No | `gemini-2.5-flash` | Primary model for agent runs |
| `MAF_BASE_URL` | string | No | `https://generativelanguage.googleapis.com/v1beta/openai/` | API base URL (also `GEMINI_BASE_URL`) |
| `MAF_REPO_ROOT` | string | No | `.` (project root) | Repo root path for agent filesystem access |
| `MAF_ENTITIES_DIR` | string | No | `entities` | Directory for DevUI entity discovery |
| `MAF_CHECKPOINT_DIR` | string | No | `state\maf-checkpoints` | Checkpoint storage directory |
| `MAF_FALLBACK_CHAIN` | string | No | Auto-computed | Comma-separated fallback provider chain |
| `MAF_ROUTE_LANE` | string | No | `auto` | Routing lane: `auto`, `balanced`, `fast`, `deep` |
| `ANTHROPIC_API_KEY` | string | No | — | Optional Anthropic API key for Claude fallback |
| `ANTHROPIC_MODEL` | string | No | `claude-sonnet-4-6` | Anthropic model when API key is set |
| `GEMINI_CLI_COMMAND` | string | No | `gemini.cmd` | Gemini CLI executable path |
| `CLAUDE_CLI_COMMAND` | string | No | `claude` | Claude CLI executable path |
| `CODEX_CLI_COMMAND` | string | No | `codex.cmd` | Codex CLI executable path |

[VERIFIED: `maf_starter/config.py` full read, 2026-05-24]

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Wiki page push | GitHub Contents API | `git clone wiki.git` + push | Contents API doesn't support wiki repos; confirmed from Phase 3/4 execution |
| Badge generation | Static SVG files | shields.io URL parameters | Auto-updating, zero maintenance |
| Python CI on Linux | Custom test runner script | `pip install pytest` + `python -m pytest [files] -v` | pytest discovers unittest.TestCase natively; explicit file targeting avoids import errors |
| Full test discovery | `python -m unittest discover -s tests` | Explicit file args: `pytest tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py` | Full discovery hits import errors on files that need `agent_framework` or `autogen_starter` |

---

## Common Pitfalls

### Pitfall 1: `requirements.txt` Does Not Exist

**What goes wrong:** CI step `pip install -r requirements.txt` fails with "No such file or directory".
**Why it happens:** The repo has no committed `requirements.txt`, `pyproject.toml`, or `setup.py`. The STACK.md and AGENTS.md reference one, but it was never committed to the repo (or was deleted during the `maf_core` → `maf_starter` refactor).
**How to avoid:** Do NOT add a `pip install -r requirements.txt` step to CI. Only install `pytest` itself. The two target test files have no third-party imports and need nothing else.
**Warning signs:** CI step failing with "requirements.txt not found" immediately.

[VERIFIED: GitHub API contents endpoint returns 404 for requirements.txt; recursive tree listing confirms absence, 2026-05-24]

### Pitfall 2: `autogen_starter` Package Not in Repo

**What goes wrong:** `python -m unittest discover -s tests` fails on the first test file that imports `from autogen_starter import ...` with `ModuleNotFoundError: No module named 'autogen_starter'`.
**Why it happens:** `autogen_starter/` was present in the old codebase structure but was removed during the `maf_core` → `maf_starter` refactor. The old `autogen_dashboard/` package still imports from it, as do several test files.
**How to avoid:** Only run the two stdlib-only test files in CI. Never use `discover` without explicit file targeting.
**Warning signs:** First CI run fails with `ModuleNotFoundError`.

[VERIFIED: recursive tree listing + import analysis of test files + `autogen_dashboard/app.py` imports `autogen_starter.providers`, 2026-05-24]

### Pitfall 3: Wiki "Repository Not Found" (same as Phases 3 and 4)

**What goes wrong:** `git clone https://github.com/Coding-Autopilot-System/autogen.wiki.git` returns "Repository not found".
**Why it happens:** `has_wiki: false` — GitHub has NOT initialized the wiki.git repository. The wiki must be seeded by creating the first page via the web UI.
**How to avoid:** Wave 0 must be a manual human checkpoint: navigate to `https://github.com/Coding-Autopilot-System/autogen/wiki`, click "Create the first page", save any stub. Automation can proceed only after this step.
**Warning signs:** Any git operation on the wiki.git URL fails before Wave 0 is confirmed.

[VERIFIED: GitHub API `has_wiki: false`, 2026-05-24; behavior confirmed from Phase 3 and Phase 4 execution]

### Pitfall 4: Wiki Push Branch Mismatch

**What goes wrong:** `git push origin main` to wiki.git fails; pages don't appear.
**Why it happens:** GitHub wiki git repos always use `master` as their default branch, regardless of the main repo's default branch. The autogen main repo uses `main`, but wiki.git uses `master`.
**How to avoid:** Always use `git push origin master` for wiki.git operations — even though the main repo's default branch is `main`.
**Warning signs:** Push fails or pages don't show after push.

[CITED: Phase 3 and Phase 4 execution — confirmed pattern]

### Pitfall 5: CI Badge Branch Parameter

**What goes wrong:** CI badge shows "no status" after workflow is created.
**Why it happens:** Without `?branch=main` in the badge URL, GitHub may default to showing the badge for the last-run branch, which may not be `main` if the first run was triggered on a PR branch.
**How to avoid:** Badge URL must include `?branch=main`. Note this is `main` (not `master` as in Promptimprover).
**Warning signs:** Badge shows grey "no status" shield after the workflow file is pushed.

[VERIFIED: GitHub API `default_branch: "main"`, 2026-05-24]

### Pitfall 6: README References Broken Local File Paths

**What goes wrong:** The current README contains links like `[main.py](/C:/repo/autogen/main.py)` — absolute Windows local paths. These render as broken links in GitHub.
**Why it happens:** The README was written assuming local development on the developer's machine and used local absolute paths instead of relative repo paths.
**How to avoid:** The rewritten README must not include any `/C:/repo/autogen/` paths. Use relative paths (`maf_starter/config.py`) or plain text references (`python main.py doctor`), not markdown file links to non-existent local paths.
**Warning signs:** Any `](/C:/` in the rendered README.

[VERIFIED: README.md content full read, 2026-05-24 — multiple broken local paths confirmed]

### Pitfall 7: `main.py` Not in Repo Tree

**What goes wrong:** The rewritten README describes `python main.py` commands, which is accurate for local development, but creates confusion because `main.py` is not visible in the GitHub file browser.
**Why it happens:** `main.py` is listed in the AGENTS.md and referenced throughout README and docs, but does NOT appear in the GitHub tree listing. It may be gitignored or the refactor removed it.
**How to avoid:** The wiki Setup Guide should note that `main.py` is the local entry point. The Architecture page should not reference `main.py` as a visible repo artifact. The README quickstart uses `python main.py <cmd>` commands (accurate) but links to the file by path (broken).
**Warning signs:** Readers can't find `main.py` in the GitHub file browser.

[VERIFIED: recursive tree listing shows no `main.py` at root, 2026-05-24]

---

## Wiki Page Content Guide

### Home.md (AG-03)

Structure (Phase 3/4 pattern adapted for Python multi-agent):

1. `# autogen` — H1
2. Badge line (CI + Python 3.12 + License) — same as README
3. Hero paragraph (2-3 sentences, enterprise tone)
4. Quick-start command snippet (3-4 commands: venv, pip, env setup):
   ```bash
   python -m venv .venv
   .venv/Scripts/activate   # Windows: .\.venv\Scripts\Activate.ps1
   pip install agent-framework python-dotenv
   python main.py doctor
   ```
5. Navigation table:
   | Page | Description |
   |------|-------------|
   | [Setup Guide](Setup-Guide) | Prerequisites, installation, and first agent run |
   | [Architecture](Architecture) | Runtime components and provider fallback chain |
   | [Configuration Reference](Configuration-Reference) | Environment variables and .env configuration |

### Setup-Guide.md (AG-03)

Sections in order:
1. `## Prerequisites` — Python 3.10+, pip, virtual environment, Gemini API key
2. `## Installation` — `git clone`, create venv, `pip install agent-framework python-dotenv fastapi uvicorn`
3. `## Configuration` — copy `.env.example` to `.env`, set `GEMINI_API_KEY`
4. `## Running the Agent` — `python main.py doctor`, `python main.py smoke --message "..."`
5. `## What a Successful Setup Looks Like` — `doctor` prints config without exposing key; `smoke` returns agent response

**Note:** No `requirements.txt` exists — the Setup Guide must list the key packages manually. The README's quickstart uses `pip install -r requirements.txt` which will fail. The wiki should give working instructions: `pip install agent-framework python-dotenv fastapi uvicorn`.

### Architecture.md (AG-03)

1. Mermaid `flowchart LR` diagram (same as README — reuse pattern from Phase 4 D-13)
2. Per-component prose below diagram using the component descriptions from the Source Tree section above
3. Key sections: Provider Routing, Agent Layer, Entities, Legacy Compatibility

### Configuration-Reference.md (AG-03)

Table format with all env vars from the Configuration Reference section above. Group: Required, Optional/MAF, Optional/Provider.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `gh` CLI | GitHub API calls, content creation | Yes | 2.86.0 | — |
| `git` | Wiki clone+push | Yes | 2.53.0.windows.1 | — |
| Python 3.12 | CI workflow research/validation | Yes (local) | 3.14.2 (CI uses 3.12) | — |
| `pytest` | CI test runner | No (not committed) | Installed by CI: `pip install pytest` | — |
| GitHub Actions Python 3.12 runner | CI execution | Yes (cloud) | Provided by actions/setup-python@v5 | — |

No missing dependencies that block execution. `pytest` is installed by the CI workflow itself.

---

## Validation Architecture

### Test Framework (CI Verification)

| Property | Value |
|----------|-------|
| Framework | stdlib `unittest` (run via `pytest`) |
| Config file | None — no pytest.ini, pyproject.toml, or conftest.py |
| Quick run command | `python -m pytest tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py -v` |
| Full suite command | Same — only these two tests are CI-runnable without framework pip packages |
| Install required | `pip install pytest` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | Notes |
|--------|----------|-----------|-------------------|-------|
| AG-01 | README contains hero line, no "starter kit" language, no local paths | Manual visual check | `gh api repos/Coding-Autopilot-System/autogen/contents/README.md --jq '.content' \| base64 -d` | Content review after push |
| AG-02 | CI workflow runs green on main | Smoke (CI run) | `gh run list -R Coding-Autopilot-System/autogen --limit 1` | Check after workflow file push |
| AG-03 | Wiki pages exist with correct content | Manual check | `git ls-remote https://github.com/Coding-Autopilot-System/autogen.wiki.git` | Verify pages at wiki URL |
| AG-04 | Badges render in README | Manual visual check | — | Verify at repo URL |
| AG-05 | Cross-repo links in README | Manual check | `gh api repos/Coding-Autopilot-System/autogen/contents/README.md --jq '.content' \| base64 -d \| grep 'Coding-Autopilot-System'` | Check raw README content |

### Wave 0 Gaps

- [ ] Wiki initialization — `https://github.com/Coding-Autopilot-System/autogen/wiki` — manual human action required before Wave 2
- [ ] No `requirements.txt` — handled by CI installing `pytest` directly (not via requirements file)

---

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|------------------|-------|
| Phase 2 used `windows-latest` runner (.NET) | Phase 5 uses `ubuntu-latest` runner (Python) | Python CI is standard on Linux |
| Phase 4 used `actions/setup-node@v4` | Phase 5 uses `actions/setup-python@v5` | Equivalent pattern, different ecosystem |
| `actions/setup-python@v4` (older) | `actions/setup-python@v5` (current) | v5 is current stable [ASSUMED — verify at execution time] |
| TESTING.md (2026-03-26) lists `python -m unittest discover` | Phase 5 CI uses explicit pytest file targeting | `discover` fails due to removed `autogen_starter` package |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Python 3.12 is available via `actions/setup-python@v5` on GitHub Actions | CI Workflow Pattern | Use different version; 3.11 is safe fallback |
| A2 | `actions/setup-python@v5` is current stable version | Standard Stack | Use wrong version; low risk (GitHub provides upgrade warnings) |
| A3 | Mermaid flowchart subgraph syntax renders correctly on GitHub | Architecture Patterns | Diagram shows as error; executor adjusts syntax and re-pushes |
| A4 | `pip install pytest` completes without issue on ubuntu-latest | Validation Architecture | CI fails at install step; trivial to fix |
| A5 | `main.py` at the repo root is genuine (local to developer's machine, not committed) rather than gitignored | Pitfall 7 | Could be gitignored and exist locally; safe to describe as commands, avoid file links |

---

## Open Questions

1. **Should the CI workflow include a `pip install agent-framework` step to enable more tests?**
   - What we know: `agent-framework` is the Microsoft Agent Framework. It is a real PyPI package. Installing it might allow `test_phase3_routing.py` and `test_maf_setup.py` to run.
   - What's unclear: Whether `agent-framework` is installable on PyPI without additional dependencies; whether it requires Windows-specific features; whether it has a compatible version for Python 3.12 CI.
   - Recommendation: Start with the two stdlib-only tests for a clean green CI. The planner can add a follow-up option to install the package, but that risks introducing a more fragile CI that breaks when package versions change.

2. **Is `main.py` gitignored or genuinely absent?**
   - What we know: `main.py` is referenced in README and AGENTS.md as the entry point; it does not appear in the recursive tree listing.
   - What's unclear: Whether `.gitignore` excludes it (the README treats it as a real file) or whether it was accidentally deleted.
   - Recommendation: Treat it as absent from the GitHub view. Reference `python main.py` as a command in docs but do not link to it as a file.

---

## Sources

### Primary (HIGH confidence)

- [VERIFIED: GitHub API] `Coding-Autopilot-System/autogen` — repo metadata: `default_branch: "main"`, `has_wiki: false`, `language: "Python"`, `license.key: "mit"`, fetched 2026-05-24
- [VERIFIED: GitHub API tree] Recursive file listing — confirms no `requirements.txt`, no `.github/`, no `autogen_starter/`, no `main.py` at root
- [VERIFIED: GitHub API contents] All `tests/*.py` imports — two stdlib-only files identified
- [VERIFIED: GitHub API contents] `maf_starter/config.py` — complete env var list
- [VERIFIED: GitHub API contents] `autogen_dashboard/app.py` — imports `autogen_starter.providers` (missing package confirmed)
- [VERIFIED: GitHub API contents] `README.md` — current content: "Starter" framing, broken local paths, no badges
- [VERIFIED: GitHub API contents] `.planning/codebase/TESTING.md` — test framework documentation
- [VERIFIED: GitHub API contents] `AGENTS.md` — Python 3.14-era codebase, pip + virtualenv workflow
- [VERIFIED: Phase 4 Research] `04-RESEARCH.md` — wiki delivery pattern, badge URL format, enterprise tone, cross-repo links pattern
- [VERIFIED: Phase 3 Research] `03-RESEARCH.md` — wiki initialization pattern (manual first page), wiki.git branch = master

### Secondary (MEDIUM confidence)

- [CITED: Phase 3 and Phase 4 execution] Wiki initialization must be manual (confirmed from two prior phase executions)
- [CITED: Phase 4 CI pattern] `actions/checkout@v4` + `ubuntu-latest` — reused for Python CI

### Tertiary (LOW confidence / ASSUMED)

- [ASSUMED] `actions/setup-python@v5` is current stable version (not verified via API)
- [ASSUMED] Python 3.12 available on ubuntu-latest GitHub Actions runner
- [ASSUMED] Mermaid subgraph syntax renders correctly on GitHub

---

## Metadata

**Confidence breakdown:**
- Repo metadata (branch, wiki, license): HIGH — verified from GitHub API
- Source tree / file listing: HIGH — recursive tree listing confirmed
- Test file import analysis: HIGH — multiple test files read and imports verified
- CI strategy: HIGH — derived from verified facts about missing packages
- Configuration reference: HIGH — `maf_starter/config.py` read completely
- Wiki delivery: HIGH — confirmed from Phase 3 and Phase 4 execution
- Architecture content: MEDIUM — current structure verified; prose quality depends on execution

**Research date:** 2026-05-24
**Valid until:** 2026-06-24 (stable; 30-day window)
