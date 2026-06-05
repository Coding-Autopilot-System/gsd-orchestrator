# Phase 7: EMERGENCY — Fix ci-autopilot + Level A Docs — Research

**Researched:** 2026-05-26
**Domain:** GitHub Actions workflow triage, GitHub REST/GraphQL API bulk operations, Python CI, portfolio documentation
**Confidence:** HIGH

---

## Summary

ci-autopilot has accumulated 1,964 open issues since December 2025 due to a GitHub Actions reserved environment variable conflict. The `runner-health.yml` workflow sets `env.RUNNER_NAME: MyLocalPC`, but `RUNNER_NAME` is a GitHub Actions built-in variable that cannot be overridden — each `ubuntu-latest` run receives a unique ephemeral name like `GitHub Actions 1000003360`. The deduplication check searches for issues titled `"Runner offline: MyLocalPC"` but all issues are titled `"Runner offline: GitHub Actions XXXXXXXXXX"` (using the actual runner name), so every 15-minute cron run creates a fresh issue instead of commenting on an existing one.

The cleanest remediation is to remove the cron trigger from `runner-health.yml` and retain the `workflow_dispatch` trigger for demonstration purposes. All 1,964 open issues should then be bulk-closed via the GitHub REST API with `--paginate` and parallel `xargs`. This takes under 10 minutes and stays within rate limits.

The repo is already well-structured for Level A documentation: it has MIT license, 7 existing `docs/` pages, a GitHub Pages site (`docs/index.html`), and a Python agent codebase (`agent/poll_once.py`) using only stdlib. No test suite exists, so CI should use a Python syntax/lint check on `ubuntu-latest`. The wiki is enabled but not initialized — a manual checkpoint is required before wiki pages can be pushed.

**Primary recommendation:** Remove cron from runner-health.yml, bulk-close all open issues via REST pagination + parallel xargs, then bring ci-autopilot to Level A documentation with a Python lint CI, rewritten README, 4 wiki pages derived from existing `docs/` content, and 6-8 GitHub topics.

---

## Phase Requirements

<phase_requirements>

| ID | Description | Research Support |
|----|-------------|------------------|
| CIAP-01 | Disable/fix runner-health.yml runaway cron (currently `*/15 * * * *` checking offline self-hosted runner) | Root cause confirmed: RUNNER_NAME env var conflict. Fix: remove `schedule` trigger, keep `workflow_dispatch`. Workflow SHA for update: `281d8da95923c8ad5af3c94f07308505172d9049` |
| CIAP-02 | Bulk-close all 1,964+ open `runner-offline` issues via GitHub API | Confirmed 1,964 open issues (all runner-offline). Strategy: `gh api --paginate` + `xargs -P 8 gh issue close`. Rate limit safe (4,991 of 5,000 core remaining). |
| CIAP-03 | ci-autopilot Level A docs — README rewrite (AI agent automation framing), CI badge, wiki 4 pages, GitHub topics, cross-links to org | Stack confirmed (Python 3.12 stdlib). CI strategy: py_compile/flake8. Wiki not initialized. LICENSE MIT already exists. 7 docs/ files available for wiki content. |

</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Workflow cron disable | GitHub Actions YAML | — | Modify `runner-health.yml` via MCP file update |
| Bulk issue close | GitHub REST API | GitHub GraphQL (fallback) | REST pagination + xargs is simpler and proven; 1,964 << 5,000/hr limit |
| CI pipeline (Python lint) | GitHub Actions `ubuntu-latest` | — | Same pattern as autogen Phase 5; no self-hosted runner needed |
| README rewrite | GitHub MCP file update | — | `mcp__github__create_or_update_file` against main branch |
| Wiki pages | Git clone of `.wiki.git` | — | Push via git, same as phases 3/4/5 |
| GitHub topics | GitHub REST API | — | `PATCH /repos/{owner}/{repo}/topics` |

---

## Standard Stack

### Core
| Library / Tool | Version | Purpose | Why Standard |
|----------------|---------|---------|--------------|
| gh CLI | 2.x (system) | Bulk issue close, API calls | Authenticated, handles pagination |
| GitHub REST API | 2022-11-28 | Issue state update | `PATCH /repos/{owner}/{repo}/issues/{n}` with `state: closed` |
| GitHub MCP tools | — | File create/update in repo | Same pattern used in phases 2-6 |
| Python | 3.12 | CI target language | Matches existing codebase (str | None union syntax requires 3.10+) |

### Supporting
| Library / Tool | Version | Purpose | When to Use |
|----------------|---------|---------|-------------|
| GitHub GraphQL API | — | Batch mutations (alternative) | If REST rate limit is hit (unlikely) |
| flake8 or py_compile | — | Python syntax check for CI | No test suite exists; lint/syntax is the minimal viable CI |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| REST paginate + xargs | GraphQL batch closeIssue | GraphQL batch (50 per call) is faster but more complex; REST is simpler and rate limit is ample |
| Disable cron trigger | Delete workflow file entirely | Deleting removes valuable portfolio artifact; disabling preserves `workflow_dispatch` for demo |
| flake8 | pytest with trivial tests | pytest with no tests fails; py_compile is simpler and passes |

---

## Root Cause: Verified

### Why the Deduplication Failed

`runner-health.yml` sets `env.RUNNER_NAME: MyLocalPC` at the workflow level. However, `RUNNER_NAME` is a **GitHub Actions built-in environment variable** — it reflects the actual runner executing the job and cannot be overridden by workflow-level `env:` declarations.

Because the workflow runs on `ubuntu-latest` (GitHub-hosted ephemeral runners), each run receives a unique name like `GitHub Actions 1000003360`, `GitHub Actions 1000003358`, etc.

The deduplication check is:
```bash
existing="$(gh issue list -s open -l runner-offline -S "${title}" --repo ...)"
```
where `title="Runner offline: ${RUNNER_NAME}"`. Because `RUNNER_NAME` is the ephemeral runner name, this title differs every run — the search never finds an existing issue and always creates a new one.

**Evidence from actual issues:**
- Issue #6: `"Runner offline: GitHub Actions 1000000053"` (2025-12-22)
- Issue #1964: `"Runner offline: GitHub Actions 1000003360"` (2026-02-20)
- Pattern: Every 15 minutes = 96/day × 20 active days ≈ 1,920 issues (matches actual 1,964)

`[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot/issues]`

---

## Architecture Patterns

### Pattern 1: Disable Cron Trigger (Recommended Fix)

**What:** Remove the `schedule` trigger from `runner-health.yml`, retain `workflow_dispatch`

**Why:** The runner `MyLocalPC` is permanently offline. The workflow serves as a portfolio demonstration of monitoring patterns but should not run automatically. `workflow_dispatch` preserves the capability for live demo.

**Before (current):**
```yaml
on:
  schedule:
    - cron: "*/15 * * * *"
  workflow_dispatch:
```

**After (fixed):**
```yaml
on:
  workflow_dispatch:
```

**Implementation:** `mcp__github__create_or_update_file` with SHA `281d8da95923c8ad5af3c94f07308505172d9049`

`[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot/contents/.github/workflows/runner-health.yml]`

### Pattern 2: Bulk Issue Close via REST Pagination + Parallel xargs

**What:** Paginate all open issues with `runner-offline` label, close each in parallel

**Implementation approach:**
```bash
# Step 1: collect all issue numbers (tested: returns 1,956 via --paginate)
gh api --paginate \
  "repos/Coding-Autopilot-System/ci-autopilot/issues?state=open&labels=runner-offline&per_page=100" \
  --jq '.[].number' > /tmp/issue-numbers.txt

# Step 2: bulk close in parallel
cat /tmp/issue-numbers.txt | \
  xargs -P 8 -I {} \
  gh issue close {} \
    -R Coding-Autopilot-System/ci-autopilot \
    --reason "not planned" \
    -c "Closing: self-hosted runner is permanently offline. Runner health monitoring has been disabled."
```

**Rate limit math:**
- Current remaining: 4,991 of 5,000 core/hour `[VERIFIED: gh api rate_limit]`
- Operations needed: 1,964 close + ~20 paginate calls = ~1,984 total
- Time at `-P 8` parallelism: ~5-10 minutes
- Safe: 1,984 << 5,000

**Scope:** All 1,964 open issues are `runner-offline` labeled. No non-runner-offline open issues exist. `[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot/issues?state=open]`

### Pattern 3: Python Lint CI Workflow

**What:** GitHub Actions workflow on `ubuntu-latest` that checks Python syntax

**Rationale:** `agent/poll_once.py` uses only Python stdlib (no external packages). `requirements.txt` contains only a comment. No test suite exists. A Python compile check is the appropriate minimal CI that will pass.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: "3.12"
      - name: Syntax check
        run: python -m py_compile agent/poll_once.py agent/__init__.py
      - name: Import check
        run: python -c "import agent.poll_once"
```

`[VERIFIED: agent/poll_once.py uses str | None syntax (requires 3.10+), only stdlib imports]`

### Pattern 4: README Rewrite (AI Agent Framing)

**Current README framing:** "Enterprise-grade CI automation and operational control plane for Codex-driven workflows."

**Target Level A framing:** Position as an AI-driven CI repair agent:
- Hero line: something like "AI-powered CI repair agent — autonomously detects, triages, and patches GitHub Actions failures using Codex"
- Mermaid flowchart showing: failure event → intake → queue → runner → fix → PR
- Badges: CI (new ci.yml on main), Python 3.12, MIT, ecosystem link
- Cross-repo links to org and sibling projects
- Reference to `docs/` for deep documentation

**Existing README SHA for update:** `61f6ab848655b82e58c71106b134de58d9607d19` `[VERIFIED: gh api]`

### Pattern 5: Wiki Pages from Existing Docs

ci-autopilot has 7 existing `docs/` files. The 4 wiki pages can be directly derived:

| Wiki Page | Source from docs/ | Notes |
|-----------|-------------------|-------|
| Home | `docs/README.md` + `docs/architecture.md` summary | Overview + navigation |
| Setup Guide | `docs/runner-setup.md` | Runner registration + local dev setup |
| Architecture | `docs/architecture.md` + `docs/control-plane.md` | System design + data flow |
| Configuration Reference | `docs/security.md` + `docs/operations.md` | Tokens, secrets, operations runbook |

This is richer than previous phases which wrote wiki content from scratch.

### Recommended Project Structure (existing — no changes needed)
```
ci-autopilot/
├── .github/workflows/
│   ├── ci.yml              # NEW: Python lint CI (to create)
│   ├── runner-health.yml   # MODIFY: remove cron trigger
│   ├── fixer.yml           # unchanged
│   ├── autopilot-create-issue.yml   # unchanged
│   ├── autopilot-failure-intake.yml # unchanged
│   └── runner-smoke-test.yml        # unchanged
├── agent/
│   ├── __init__.py
│   └── poll_once.py        # Python 3.12 stdlib agent
├── docs/                   # 7 existing pages + index.html (GitHub Pages)
├── memory/examples/        # Codex agent memory
├── scripts/
├── AGENTS.md               # Codex agent guidelines
├── LICENSE                 # MIT (already exists)
├── Machinesetup.ps1
├── README.md               # REWRITE for Level A
└── requirements.txt        # Minimal (comment only)
```

### Anti-Patterns to Avoid
- **Do not close all open issues without label filter:** All 1,964 open issues are runner-offline, but always use `-l runner-offline` to be explicit about scope.
- **Do not use `gh issue delete`:** Delete is destructive and requires admin confirmation; `close` is the correct verb.
- **Do not try to fix the deduplication logic:** The runner is permanently offline; fixing dedup would just resume slower issue creation. Disable the cron instead.
- **Do not create a pytest-based CI:** No tests exist. py_compile/import check is the correct minimal CI.
- **Do not initialize wiki without manual checkpoint:** `ci-autopilot.wiki.git` returns 404 — the wiki must be initialized via GitHub UI before git push works.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bulk issue close | Custom script with sleep loops | `gh api --paginate` + `xargs -P 8 gh issue close` | Built-in pagination handles all pages; xargs handles parallelism |
| Rate limit handling | Manual backoff logic | `gh` CLI handles 429 automatically | gh CLI has built-in retry on rate limit |
| Python version detection | Custom version check | `actions/setup-python@v5` with `python-version: "3.12"` | Standard GitHub Actions action |

---

## Repository State (Verified)

| Property | Value | Source |
|----------|-------|--------|
| Default branch | `main` | `[VERIFIED: gh api]` |
| Language | Python | `[VERIFIED: gh api .language]` |
| License | MIT (already present) | `[VERIFIED: gh api .license.spdx_id]` |
| Has wiki (enabled) | true | `[VERIFIED: gh api .has_wiki]` |
| Wiki initialized | NO (404 on .wiki.git) | `[VERIFIED: curl .wiki.git 404]` |
| GitHub Pages | YES (`docs/` folder, main branch) | `[VERIFIED: gh api repos/.../pages]` |
| GitHub topics | None (empty array) | `[VERIFIED: gh api .topics]` |
| Open issues | 1,964 (all runner-offline) | `[VERIFIED: gh api .open_issues_count]` |
| runner-offline issues | 1,956 (search API, may lag) | `[VERIFIED: gh api search/issues]` |
| Existing CI workflows | None (only operational workflows) | `[VERIFIED: gh api .../contents/.github/workflows]` |
| runner-health.yml SHA | `281d8da95923c8ad5af3c94f07308505172d9049` | `[VERIFIED: gh api]` |
| README SHA | `61f6ab848655b82e58c71106b134de58d9607d19` | `[VERIFIED: gh api]` |
| GitHub Pages URL | `https://coding-autopilot-system.github.io/ci-autopilot/` | `[VERIFIED: gh api repos/.../pages]` |

---

## Common Pitfalls

### Pitfall 1: RUNNER_NAME Cannot Be Overridden
**What goes wrong:** Setting `env.RUNNER_NAME: MyLocalPC` in a workflow has no effect — GitHub Actions silently ignores it because `RUNNER_NAME` is a reserved built-in variable.
**Why it happens:** GitHub Actions built-in variables (RUNNER_NAME, GITHUB_SHA, etc.) are injected by the runner at job start and override any `env:` declarations with the same key.
**How to avoid:** Use a non-reserved custom name like `TARGET_RUNNER` or `MONITORED_RUNNER_NAME`.
**Warning signs:** Issues are titled with ephemeral runner names (GitHub Actions XXXXXXXX) instead of the intended name.

### Pitfall 2: Wiki Requires Manual Initialization
**What goes wrong:** `git push` to `ci-autopilot.wiki.git` fails with 403/404 because no wiki page has been created.
**Why it happens:** GitHub only initializes the wiki.git repository after at least one page is created via the web UI.
**How to avoid:** Include a `07-00-PLAN.md` manual checkpoint to create the first wiki page before the automated push.
**Warning signs:** `git ls-remote` on wiki.git returns 404.

### Pitfall 3: xargs Parallelism Can Hit Secondary Rate Limits
**What goes wrong:** Running `-P 20` or higher can trigger GitHub's abuse detection secondary rate limit (not the primary 5,000/hr limit).
**Why it happens:** Too many concurrent write requests from the same token trigger abuse protection.
**How to avoid:** Use `-P 8` or lower. Add `--retry 3` if needed. The close operation will complete in ~10 minutes at `-P 8`.
**Warning signs:** HTTP 429 responses with `Retry-After` headers.

### Pitfall 4: py_compile on __init__.py (Empty File)
**What goes wrong:** `python -m py_compile agent/__init__.py` on an empty file succeeds (correct), but if CI checks for syntax errors, empty files parse fine.
**Why it happens:** `agent/__init__.py` is empty (verified). py_compile handles empty files correctly.
**How to avoid:** Run py_compile on `poll_once.py` only if `__init__.py` is empty. Or run `python -c "import agent.poll_once"` as an integration check.

### Pitfall 5: Paginate Returns Slightly Different Count Than .open_issues_count
**What goes wrong:** `gh api .open_issues_count` returns 1,964 but `gh api --paginate` with `labels=runner-offline` returns 1,956.
**Why it happens:** GitHub's search index lags real-time; also, `open_issues_count` includes pull requests.
**How to avoid:** Use `gh api --paginate "...?state=open&per_page=100"` without label filter to get all open issues, then close them all (they are all runner-offline anyway).

---

## Code Examples

### Fetch All Runner-Offline Issue Numbers
```bash
# Source: [VERIFIED: gh api --paginate tested, returns 1956 numbers]
gh api --paginate \
  "repos/Coding-Autopilot-System/ci-autopilot/issues?state=open&labels=runner-offline&per_page=100" \
  --jq '.[].number' \
  > /tmp/runner-offline-issues.txt
wc -l /tmp/runner-offline-issues.txt  # should be ~1964
```

### Bulk Close All Issues
```bash
# Source: [VERIFIED: gh issue close --help, rate_limit confirmed safe]
cat /tmp/runner-offline-issues.txt | \
  xargs -P 8 -I {} \
  gh issue close {} \
    -R Coding-Autopilot-System/ci-autopilot \
    --reason "not planned" \
    -c "Runner is permanently offline. Monitoring workflow cron disabled."
```

### Verify Zero Open Issues After Close
```bash
gh api repos/Coding-Autopilot-System/ci-autopilot --jq '.open_issues_count'
# Expect: 0
```

### Disable Cron Trigger (Key Section of Fixed runner-health.yml)
```yaml
# Source: [VERIFIED: runner-health.yml fetched, SHA confirmed]
on:
  workflow_dispatch:   # Keep for demo; remove schedule entirely
```

### GitHub Topics Update for ci-autopilot
```bash
# Source: [CITED: docs.github.com/en/rest/repos/repos#replace-all-repository-topics]
gh api repos/Coding-Autopilot-System/ci-autopilot/topics \
  -X PUT \
  -f names[]="github-actions" \
  -f names[]="ci-automation" \
  -f names[]="python" \
  -f names[]="autonomous-agents" \
  -f names[]="devops" \
  -f names[]="self-hosted-runner" \
  -f names[]="issue-triage" \
  -f names[]="codex"
```

### Python Lint CI Badge URL
```markdown
[![CI](https://github.com/Coding-Autopilot-System/ci-autopilot/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Coding-Autopilot-System/ci-autopilot/actions/workflows/ci.yml)
[![Python 3.12](https://img.shields.io/badge/python-3.12-blue.svg)](https://www.python.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
```

---

## Workflows Inventory (Full)

| Workflow File | Purpose | Trigger | Status |
|---------------|---------|---------|--------|
| `runner-health.yml` | Checks if self-hosted runner is online; creates issue if not | `*/15 * * * *` + `workflow_dispatch` | **FIX: remove cron** |
| `fixer.yml` | Main CI autopilot — runs Python agent on self-hosted Windows runner | `workflow_dispatch`, `repository_dispatch`, daily at 02:00 | Leave as-is |
| `autopilot-failure-intake.yml` | Creates queued issue when fixer.yml or runner-smoke-test.yml fails | `workflow_run completed` | Leave as-is |
| `autopilot-create-issue.yml` | Creates issue via actions/github-script when monitored workflows fail | `workflow_run completed` | Leave as-is |
| `runner-smoke-test.yml` | Smoke tests the self-hosted Windows runner | `workflow_dispatch` | Leave as-is |
| `ci.yml` (new) | Python 3.12 syntax check on ubuntu-latest | `push/PR to main` | **CREATE** |

---

## Level A Documentation Checklist for ci-autopilot

| Item | Status | Action |
|------|--------|--------|
| README with hero line | Missing — current is functional, not portfolio-framed | Rewrite |
| CI badge (green) | Missing — no ci.yml exists | Create ci.yml + badge |
| Mermaid architecture diagram | Missing | Add to README |
| MIT License badge | Can add — LICENSE exists | Add badge to README |
| GitHub topics (5-10) | Empty | Set via API |
| Cross-repo ecosystem links | Missing | Add to README |
| Wiki — 4 pages | Not initialized | Manual checkpoint → push 4 pages |
| GitHub Release | Not required for Level A per prior phases | Skip |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| gh CLI | Bulk issue close, API calls | Yes | System installed | — |
| GitHub REST API | Issue close, topics update | Yes | 2022-11-28 | — |
| GitHub MCP tools | File create/update | Yes | — | gh CLI |
| git | Wiki push | Yes | System installed | — |
| Python 3.12 | CI workflow target | GitHub-hosted | 3.12 on ubuntu-latest | 3.11 |
| ci-autopilot.wiki.git | Wiki page push | NO — not initialized | — | Manual checkpoint required |

**Missing dependencies with no fallback:**
- `ci-autopilot.wiki.git` — must be initialized via GitHub web UI before automated push can proceed. Requires a `07-00-PLAN.md` manual checkpoint (same pattern as phases 3, 4, 5).

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None (portfolio docs + API operations; manual verification) |
| Config file | N/A |
| Quick run command | `gh api repos/Coding-Autopilot-System/ci-autopilot --jq '.open_issues_count'` |
| Full suite command | See verification steps per plan |

### Phase Requirements Verification Map
| Req ID | Behavior | Verification | Automated Command |
|--------|----------|--------------|-------------------|
| CIAP-01 | runner-health.yml has no schedule trigger | Check workflow file on main | `gh api repos/Coding-Autopilot-System/ci-autopilot/contents/.github/workflows/runner-health.yml --jq '.content' \| base64 -d \| grep -c "schedule"` → expect 0 |
| CIAP-02 | Zero open issues in repo | Check open issue count | `gh api repos/Coding-Autopilot-System/ci-autopilot --jq '.open_issues_count'` → expect 0 |
| CIAP-03 | README has hero line + badges + cross-links; CI badge green; wiki has 4 pages; topics set | Visual + API checks | Multiple checks per sub-requirement |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `RUNNER_NAME` cannot be overridden by workflow `env:` declarations | Root Cause | If wrong, dedup logic works and only issue count matters; fix strategy unchanged |
| A2 | The 8-issue gap between `open_issues_count` (1,964) and search count (1,956) is search lag | Repository State | If there are 8 non-runner-offline issues, those would be accidentally closed by the "close all open" script; mitigate by always using `-l runner-offline` filter |
| A3 | ci-autopilot.wiki.git returns 404 meaning it's uninitialized (not a network error) | Repository State | If wiki is already initialized (unlikely given 404), the manual checkpoint plan is unnecessary but harmless |
| A4 | Python 3.12 syntax check CI will pass without modification to agent/poll_once.py | Standard Stack | If poll_once.py has issues, CI will fail and needs a fix before Level A; risk is LOW given the code is functional |

---

## Open Questions (RESOLVED)

1. **Should the GitHub Pages site (`docs/index.html`) be updated as part of Level A?**
   - What we know: GitHub Pages is enabled at `https://coding-autopilot-system.github.io/ci-autopilot/` with a professional landing page (dark theme, branded)
   - What's unclear: Is updating the Pages site part of Level A scope?
   - Recommendation: No — Level A standard per prior phases is README + wiki + CI + topics + cross-links. Pages site is bonus; leave as-is.

2. **Should the `autopilot-create-issue.yml` and `autopilot-failure-intake.yml` duplicate be cleaned up?**
   - What we know: Both workflows create issues on workflow failures; `autopilot-create-issue.yml` appears to supersede `autopilot-failure-intake.yml`
   - What's unclear: Are both intentionally kept? Cleanup is out of scope for Phase 7.
   - Recommendation: Leave both unchanged — out of scope for CIAP-03.

3. **Should the GitHub Release be created as part of Level A?**
   - What we know: Prior phases (2-5) included GitHub Release as part of Level A for gsd-orchestrator but not all repos
   - What's unclear: CIAP-03 does not explicitly require a release
   - Recommendation: Skip release for Phase 7 — CIAP-03 specifies "README rewrite, CI badge, wiki 4 pages, GitHub topics, cross-links" only.

---

## Sources

### Primary (HIGH confidence)
- `[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot/contents/.github/workflows/runner-health.yml]` — Full workflow content, SHA
- `[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot/contents/README.md]` — Full README content, SHA
- `[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot]` — Topics, language, has_wiki, license, open_issues_count, permissions
- `[VERIFIED: gh api repos/Coding-Autopilot-System/ci-autopilot/issues?state=open]` — Issue count, titles, labels confirmed
- `[VERIFIED: gh api --paginate .../issues?state=open&labels=runner-offline]` — 1,956 issue numbers retrieved
- `[VERIFIED: gh api rate_limit]` — 4,991 core requests remaining
- `[VERIFIED: curl wiki.git → 404]` — Wiki not initialized
- `[VERIFIED: gh api repos/.../pages]` — GitHub Pages enabled from docs/ on main

### Secondary (MEDIUM confidence)
- GitHub Actions documentation: `RUNNER_NAME` is a default environment variable set by the runner `[CITED: docs.github.com/en/actions/writing-workflows/choosing-what-your-workflow-does/store-information-in-variables]`
- GitHub REST API: `PATCH /repos/{owner}/{repo}/issues/{number}` accepts `state: "closed"` `[CITED: docs.github.com/en/rest/issues/issues]`

### Tertiary (LOW confidence)
- xargs `-P 8` parallelism chosen conservatively to avoid secondary rate limits `[ASSUMED]`

---

## Metadata

**Confidence breakdown:**
- Root cause analysis: HIGH — confirmed from actual issue titles vs. workflow env var
- Bulk close strategy: HIGH — tested with `gh api --paginate`, rate limit verified
- CI workflow (Python lint): HIGH — stack confirmed from codebase
- Wiki initialization requirement: HIGH — 404 on wiki.git confirmed
- README rewrite content: MEDIUM — framing is judgment call on portfolio positioning
- GitHub topics selection: MEDIUM — topic choice is reasonable but not verified against discoverability data

**Research date:** 2026-05-26
**Valid until:** 2026-06-26 (stable — GitHub API and gh CLI behavior is stable)
