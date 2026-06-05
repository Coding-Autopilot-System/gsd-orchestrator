# Phase 5: autogen Polish — Pattern Map

**Mapped:** 2026-05-24
**Files analyzed:** 7 (1 CI workflow + 1 README + 4 wiki pages + 1 Wave 0 checkpoint)
**Analogs found:** 7 / 7

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Coding-Autopilot-System/autogen/.github/workflows/ci.yml` | config (CI workflow) | request-response (push trigger → runner → test result) | `04-01-PLAN.md` — Promptimprover ci.yml (Node 22 / Vitest) | role-match (same CI pattern, different ecosystem: Python/pytest vs Node/vitest) |
| `Coding-Autopilot-System/autogen/README.md` | documentation | transform (rewrite existing file) | `04-02-PLAN.md` — Promptimprover README rewrite | exact (same structure, same badge pattern, same cross-repo links pattern) |
| `autogen.wiki.git/Home.md` | documentation (wiki) | file-I/O (git clone + push) | `04-03-PLAN.md` — Promptimprover Home.md | exact (same wiki delivery, same page structure) |
| `autogen.wiki.git/Setup-Guide.md` | documentation (wiki) | file-I/O | `04-03-PLAN.md` — Promptimprover Setup-Guide.md | exact (same structure; Python venv + pip vs Node build_and_install.ps1) |
| `autogen.wiki.git/Architecture.md` | documentation (wiki) | file-I/O | `04-03-PLAN.md` — Promptimprover Architecture.md | exact (same Mermaid flowchart LR + per-component prose structure) |
| `autogen.wiki.git/Configuration-Reference.md` | documentation (wiki) | file-I/O | `04-03-PLAN.md` — Promptimprover Configuration-Reference.md | exact (same Name/Type/Required/Default/Description table format) |
| Wave 0 manual checkpoint | checkpoint (human gate) | event-driven (human action unblocks automation) | `04-00-PLAN.md` — Promptimprover wiki initialization | exact (identical GitHub platform limitation; identical git ls-remote verification) |

---

## Pattern Assignments

### `Coding-Autopilot-System/autogen/.github/workflows/ci.yml` (config, CI workflow)

**Analog:** `04-01-PLAN.md` — Promptimprover CI workflow (Node 22)

**Key diff from analog:** Python/pytest instead of Node/vitest. No working-directory default (autogen has no subdirectory package — tests are at repo root `tests/`). Uses `actions/setup-python@v5` instead of `actions/setup-node@v4`. Branch trigger is `main` (not `master` as in Promptimprover — autogen default branch is `main`). No equivalent of `sync-version.mjs` pre-step. Only `pip install pytest` needed — no `requirements.txt` exists.

**Exact file content to create** (from RESEARCH.md CI Workflow Pattern, lines 166-191):

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

**Critical rules (derived from analog pitfalls):**
- Branch trigger MUST be `main` (not `master`) — autogen default branch is `main` [VERIFIED: GitHub API]
- Do NOT use `python -m unittest discover -s tests` — full discovery hits ModuleNotFoundError on `autogen_starter` and `agent_framework` imports
- Do NOT add `pip install -r requirements.txt` — `requirements.txt` does not exist in the repo (404 confirmed)
- Only target the two stdlib-safe test files explicitly: `tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py`
- `pip install pytest` must precede the test run step — pytest is not pre-installed on ubuntu-latest

**Analog pattern for gh CLI file creation** (from 04-01-PLAN.md Task 1 action):
```
Use GitHub MCP `create_or_update_file` tool:
  owner: "Coding-Autopilot-System"
  repo: "autogen"
  path: ".github/workflows/ci.yml"
  branch: "main"
  message: "ci: add Python 3.12 GitHub Actions build workflow (AG-02)"
  [no sha parameter — new file]
```

---

### `Coding-Autopilot-System/autogen/README.md` (documentation, transform)

**Analog:** `04-02-PLAN.md` — Promptimprover README rewrite

**Key diffs from analog:**
- H1 title: `# autogen` (remove "Microsoft Agent Framework Starter" — "Starter" must go)
- Branch in CI badge: `?branch=main` (Promptimprover used `?branch=master`)
- Technology badge: `[![Python 3.12](https://img.shields.io/badge/python-3.12-blue)](https://www.python.org/)` (not Node 22)
- Cross-repo links point TO gsd-orchestrator and Promptimprover (Promptimprover pointed TO gsd-orchestrator and autogen)
- No Windows-specific quickstart (Python venv pattern, not `.ps1` script)
- Architecture Mermaid diagram describes MAF provider routing, not MCP middleware pipeline
- Must remove broken local paths (`/C:/repo/autogen/`) from current README — same problem class as Promptimprover's internal references

**Section order** (identical to 04-02 analog, from RESEARCH.md README Structure, line 218-227):

```
1. # autogen
2. Badge line (CI + Python 3.12 + License)
3. Cross-repo ecosystem line
4. Hero paragraph
5. ## Features
6. ## Architecture (Mermaid flowchart LR)
7. ## Quickstart
8. ## Configuration
9. ## License
```

**Badge URLs** (from RESEARCH.md Badge URLs section, lines 268-272):

```markdown
[![CI](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml)
[![Python 3.12](https://img.shields.io/badge/python-3.12-blue)](https://www.python.org/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
```

**Cross-repo ecosystem line** (from RESEARCH.md Cross-Repo Links section, lines 279-281):

```markdown
Part of the [Coding-Autopilot-System](https://github.com/Coding-Autopilot-System) ecosystem:
[gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover)
```

**Hero paragraph** (from RESEARCH.md Hero Text section, lines 241-244):

```
autogen is a Python multi-agent orchestration runtime built on Microsoft Agent Framework — combining a Gemini/Claude provider fallback chain, AG-UI observability, and a local operator workbench for end-to-end autonomous engineering workflows.
```

**Mermaid Architecture Diagram** (from RESEARCH.md, lines 249-263):

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

Note: Verify Mermaid subgraph-inside-arrow syntax renders on GitHub after push. If broken, use the node-based connection pattern from 04-02 analog (`MAF --> Gemini`, `MAF --> Anthropic`, etc.).

**gh CLI update pattern** (from 04-02-PLAN.md Task 1 action, lines 126-199):
```
Step 1: Read current README.md via GitHub MCP `get_file_contents` — capture the `sha` field (mandatory for update).
Step 2: Compose full new README content.
Step 3: Call `create_or_update_file` with the captured sha, branch: "main".
Commit message: "docs: rewrite README with hero framing, badges, architecture diagram, and cross-repo links (AG-01 AG-04 AG-05)"
```

**Must NOT include:**
- "Starter" anywhere in title or body
- "starter kit" language
- Local Windows paths (`/C:/repo/autogen/` — broken links confirmed in current README)
- File links to `main.py` (not in repo tree — reference as command only: `python main.py`)
- Emoji

---

### `autogen.wiki.git/Home.md` (documentation/wiki, file-I/O)

**Analog:** `04-03-PLAN.md` — Promptimprover Home.md (lines 158-188)

**Key diffs from analog:** Python quickstart commands instead of MCP JSON config snippet. No MCP config JSON block. Replace Promptimprover hero text with autogen hero text. Navigation table links to same four pages.

**Structure** (from RESEARCH.md Wiki Page Content Guide, lines 449-468):

```markdown
# autogen

[![CI](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml)
[![Python 3.12](https://img.shields.io/badge/python-3.12-blue)](https://www.python.org/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[hero paragraph — same as README]

## Quickstart

```bash
python -m venv .venv
.venv/Scripts/activate   # Windows: .\.venv\Scripts\Activate.ps1
pip install agent-framework python-dotenv
python main.py doctor
```

## Documentation

| Page | Description |
|------|-------------|
| [Setup Guide](Setup-Guide) | Prerequisites, installation, and first agent run |
| [Architecture](Architecture) | Runtime components and provider fallback chain |
| [Configuration Reference](Configuration-Reference) | Environment variables and .env configuration |
```

**Wiki navigation link convention** (from 04-03-PLAN.md line 94-95):
Use bare page names WITHOUT `.md` extension: `[Setup Guide](Setup-Guide)` NOT `[Setup Guide](Setup-Guide.md)`

---

### `autogen.wiki.git/Setup-Guide.md` (documentation/wiki, file-I/O)

**Analog:** `04-03-PLAN.md` — Promptimprover Setup-Guide.md (lines 192-256)

**Key diffs from analog:** Python venv + pip instead of PowerShell build script. `pip install agent-framework python-dotenv fastapi uvicorn` (no `requirements.txt` exists — must list packages manually). Gemini API key env var instead of MCP client config. `python main.py doctor` as health check instead of MCP tool list.

**Section structure** (from RESEARCH.md Setup-Guide section, lines 471-478):
```
## Prerequisites
## Installation
## Configuration
## Running the Agent
## What a Successful Setup Looks Like
```

**Critical:** Must include "What a Successful Setup Looks Like" section — this is a required structural element from the analog (04-03 acceptance criteria line 376: `grep -c "What a Successful Setup Looks Like"`).

**Install commands** (no requirements.txt — list packages manually):
```bash
python -m venv .venv
.venv/Scripts/activate
pip install agent-framework python-dotenv fastapi uvicorn
```

**Health check command:**
```bash
python main.py doctor
```

---

### `autogen.wiki.git/Architecture.md` (documentation/wiki, file-I/O)

**Analog:** `04-03-PLAN.md` — Promptimprover Architecture.md (lines 258-314)

**Key diffs from analog:** MAF runtime component descriptions instead of MCP server components. Use verified component list from RESEARCH.md Source Tree (lines 286-334). Same Mermaid flowchart LR diagram as README. Note: apply same subgraph rendering caveat.

**Structure:**
```
# Architecture
[intro sentence]
## Pipeline Diagram
[Mermaid flowchart LR — same as README]
## Components
[per-component prose — one sub-section per major component]
```

**Components to document** (from RESEARCH.md lines 326-334):
- `maf_starter/config.py` — Settings dataclass + load_settings()
- `maf_starter/agent_factory.py` — build_agent() with OpenAIChatClient
- `maf_starter/provider_fallback.py` — Fallback middleware (Gemini → Anthropic → CLI chain)
- `maf_starter/routing_policy.py` — build_routing_plan(), prompt complexity classification
- `maf_starter/team_factory.py` — build_repo_team(), sequential agent chain with FileCheckpointStorage
- `maf_starter/tools.py` — build_repo_tools(), bounded repo access
- `maf_starter/orchestration.py` — RunOrchestrationState, stage tracking
- `entities/repo_team/workflow.py` — DevUI-discoverable entry point
- `autogen_dashboard/` — Legacy AutoGen compatibility layer

---

### `autogen.wiki.git/Configuration-Reference.md` (documentation/wiki, file-I/O)

**Analog:** `04-03-PLAN.md` — Promptimprover Configuration-Reference.md (lines 316-356)

**Key diffs from analog:** MAF env vars instead of Node PORT/PROMPT_REFINER vars. Much larger env var table (12 optional vars + 1 required). Group into: Required, Optional/MAF Core, Optional/Providers.

**Table format** (identical to analog — from 04-03-PLAN.md lines 324-330):

```markdown
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `GEMINI_API_KEY` | string | Yes | — | Gemini API key (primary; also read as MAF_API_KEY) |
```

**Full env var list** (from RESEARCH.md Configuration Reference section, lines 343-364):
- Required: `GEMINI_API_KEY`
- Optional: `MAF_MODEL`, `MAF_BASE_URL`, `MAF_REPO_ROOT`, `MAF_ENTITIES_DIR`, `MAF_CHECKPOINT_DIR`, `MAF_FALLBACK_CHAIN`, `MAF_ROUTE_LANE`, `ANTHROPIC_API_KEY`, `ANTHROPIC_MODEL`, `GEMINI_CLI_COMMAND`, `CLAUDE_CLI_COMMAND`, `CODEX_CLI_COMMAND`

---

### Wave 0: Manual Wiki Initialization Checkpoint

**Analog:** `04-00-PLAN.md` — Promptimprover wiki initialization (lines 43-72)

**Key diffs from analog:** Different wiki URL (`autogen/wiki` not `Promptimprover/wiki`). Different wiki.git URL (`autogen.wiki.git`). Different temp staging dir (`/tmp/ag-wiki` and `C:/tmp/ag-wiki`). Otherwise identical platform limitation and identical verification pattern.

**Human action required** (from 04-00-PLAN.md lines 55-62, adapted):
```
1. Open https://github.com/Coding-Autopilot-System/autogen/wiki in browser
2. Click "Create the first page" (green button)
3. Leave title as "Home" (default)
4. Add stub text in body (e.g., "autogen wiki — content coming soon")
5. Click "Save Page"
6. Run verification:
   git ls-remote https://github.com/Coding-Autopilot-System/autogen.wiki.git HEAD
7. Expected: 40-character SHA followed by tab and "HEAD"
```

**Acceptance criteria** (from 04-00-PLAN.md lines 64-67, adapted):
```
- `git ls-remote https://github.com/Coding-Autopilot-System/autogen.wiki.git HEAD` exits 0
- Output contains exactly one line matching `[0-9a-f]{40}\s+HEAD`
```

**Resume signal** (from 04-00-PLAN.md lines 68-70, adapted):
```
Run `git ls-remote https://github.com/Coding-Autopilot-System/autogen.wiki.git HEAD`
and paste the output. When it returns a SHA, type "wiki initialized" to continue.
```

---

## Shared Patterns

### Wiki Clone + Push Delivery Pattern
**Source:** `04-03-PLAN.md` lines 151-155 and 399-412
**Apply to:** All four wiki pages (Wave 2 plan)

```bash
# Clone (adapts Promptimprover pattern — change URL and dir)
rm -rf /tmp/ag-wiki
git clone https://x-access-token:$(gh auth token)@github.com/Coding-Autopilot-System/autogen.wiki.git /tmp/ag-wiki

# Commit and push (branch is always master for wiki.git — even though main repo uses main)
git -C /tmp/ag-wiki add Home.md Setup-Guide.md Architecture.md Configuration-Reference.md
git -C /tmp/ag-wiki -c user.email="bot@gsd" -c user.name="GSD Bot" commit -m "docs: add autogen wiki pages (AG-03)"
git -C /tmp/ag-wiki push origin master
```

**Windows path sync (CRITICAL — from 04-03-PLAN.md lines 358-367):**
```bash
# Write tool uses C:/tmp/ag-wiki/ (Windows path)
# Git clone lands at /tmp/ag-wiki = AppData/Local/Temp/ag-wiki (different location)
# Must cp after Write tool calls:
cp C:/tmp/ag-wiki/Home.md /tmp/ag-wiki/Home.md
cp C:/tmp/ag-wiki/Setup-Guide.md /tmp/ag-wiki/Setup-Guide.md
cp C:/tmp/ag-wiki/Architecture.md /tmp/ag-wiki/Architecture.md
cp C:/tmp/ag-wiki/Configuration-Reference.md /tmp/ag-wiki/Configuration-Reference.md
```

**Non-fast-forward recovery** (from 04-03-PLAN.md lines 410-412):
```bash
git -C /tmp/ag-wiki pull origin master --rebase
git -C /tmp/ag-wiki push origin master
```

### Badge URL Pattern
**Source:** `04-02-PLAN.md` lines 75-78; adapted with autogen-specific values
**Apply to:** README.md and Home.md (identical badge line in both)

```markdown
[![CI](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Coding-Autopilot-System/autogen/actions/workflows/ci.yml)
[![Python 3.12](https://img.shields.io/badge/python-3.12-blue)](https://www.python.org/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
```

**Critical diff:** `?branch=main` (not `?branch=master` as in Promptimprover). autogen default branch is `main`.

### README/Wiki Fetch-SHA-Then-Update Pattern
**Source:** `04-02-PLAN.md` lines 126-130
**Apply to:** README.md update (existing file requires SHA)

```
Step 1: Read via GitHub MCP `get_file_contents`:
  owner: "Coding-Autopilot-System"
  repo: "autogen"
  path: "README.md"
  → capture the `sha` field

Step 2: Call `create_or_update_file` with:
  sha: [captured SHA — mandatory, omitting causes 409 Conflict]
  branch: "main"
```

### CI Workflow File Creation Pattern
**Source:** `04-01-PLAN.md` lines 100-104
**Apply to:** `.github/workflows/ci.yml` (new file — no SHA needed)

```
Use GitHub MCP `create_or_update_file`:
  - For NEW files: do NOT include a `sha` parameter
  - For updates to existing files: sha is MANDATORY
```

---

## Critical Diffs: autogen vs Promptimprover (Phase 4)

| Property | Phase 4 Promptimprover | Phase 5 autogen | Impact |
|----------|------------------------|-----------------|--------|
| Default branch | `master` | `main` | CI trigger `branches: [ main ]`; badge URL `?branch=main`; gh CLI `branch: "main"` |
| CI badge branch param | `?branch=master` | `?branch=main` | Badge URL must use `main` |
| Test framework | Vitest (Node) | pytest (Python stdlib tests only) | `actions/setup-python@v5` not `setup-node@v4`; explicit file targeting not discover |
| Runtime action | `actions/setup-node@v4` | `actions/setup-python@v5` | Different action name |
| Node version | `node-version: '22'` | `python-version: '3.12'` | Different parameter key and value |
| Working directory | `defaults.run.working-directory: universal-refiner` | None (tests at repo root) | No `defaults:` block needed |
| Pre-test step | `node scripts/sync-version.mjs` | None | No equivalent step needed |
| Dependency install | `npm ci` | `pip install pytest` only | No requirements.txt exists |
| Test command | `npm test` (vitest run) | `python -m pytest tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py -v` | Explicit file args — never discover |
| Wiki clone dir | `/tmp/pi-wiki` / `C:/tmp/pi-wiki` | `/tmp/ag-wiki` / `C:/tmp/ag-wiki` | Different temp dir name |
| Technology badge | Node 22 (brightgreen) | Python 3.12 (blue) | Different badge text and color |
| Cross-repo links | links TO autogen | links TO Promptimprover | Reverse direction |
| Quickstart | `.\build_and_install.ps1` | `pip install agent-framework python-dotenv` | Python venv pattern |

---

## No Analog Found

None. All seven files have strong analogs from Phase 4.

---

## Metadata

**Analog search scope:** `.planning/phases/04-promptimprover-polish/` — all four plan files
**Files scanned:** 5 (04-00, 04-01, 04-02, 04-03, 05-RESEARCH.md)
**Pattern extraction date:** 2026-05-24
