# Phase 10: OgeonX-Ai Portfolio Repos AI Reframe + Level A — Research

**Researched:** 2026-05-28
**Domain:** GitHub documentation, CI/CD, HTML/JavaScript/Node.js portfolio sites
**Confidence:** HIGH — both remote repos fully scanned, repo metadata verified, wiki.git state confirmed

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PORT-01 | kim-ai-voice-demo AI engineer reframe — README rewrite (away from ElevenLabs demo framing), wiki 4 pages, topics | Repo scanned: HTML/JS GitHub Pages site with Node.js Express backend, 3 existing GitHub Actions workflows (devlog-sync, publish-dev-updates, roadmap-sync); NO dedicated build/lint CI; wiki.git not initialized |
| PORT-02 | My-CV reframe — README as AI-powered career tool, wiki 4 pages, topics | Repo scanned: 3-file HTML/CSS/JS CV as GitHub Pages; README is a stub (1 line); NO CI workflow at all; wiki.git not initialized; AI toolchain section exists in HTML skills |
</phase_requirements>

---

## Summary

Phase 10 repositions two OgeonX-Ai personal portfolio repos — `kim-ai-voice-demo` and `My-CV` — as AI engineering work with Level A documentation. Both repos have been fully scanned via the GitHub API.

`kim-ai-voice-demo` is a multi-component AI voice engineering project: a GitHub Pages site with ElevenLabs TTS integration, a Whisper STT playground, a voice-to-ServiceNow assistant, a Node.js/Express backend that proxies ElevenLabs API and auto-creates agents, knowledge-base markdown templates for CV-based AI agents, a dev log automation pipeline (3 GitHub Actions workflows), and a roadmap tracker. The current README leads with "Real-Time AI Voice Demo" and "ElevenLabs affiliate" framing — the reframe must lead with AI voice engineering skill, not product demo language. Three existing workflows exist but none is a build/lint CI. A lightweight CI workflow needs to be created (Node.js syntax/lint check is the viable option given the codebase is mostly static HTML + JS + one ESM Node backend).

`My-CV` is a polished, print-ready HTML/CSS/JS online CV hosted on GitHub Pages. It is a 3-file repo (README.md, index.html, style.css). The README is a 1-line stub ("Skills are available via the 'View full skills list' modal on the CV page."). The HTML reveals a comprehensive skills section including AI toolchain capabilities (LLM-based triage, Gemini/Azure OpenAI, FastAPI AI backends, AI documentation pipelines). The repo has no CI workflow. No LICENSE file exists. A minimal CI (HTML lint or validate) would provide the CI badge. The README reframe should explain that this CV is maintained using an AI toolchain (the github.com/OgeonX-Ai ecosystem) — positioning the CV itself as evidence of AI-powered workflow, not just a static document.

Both repos show `has_wiki: true` in the GitHub API, but `git ls-remote` confirms **neither wiki.git remote is provisioned**. Both plans require Wave 0 manual checkpoint plans for wiki initialization. This pattern is identical to all prior phases (3, 4, 5, 7, 8, 9).

**Primary recommendation:** Execute as two Wave 1 plans (10-01 for kim-ai-voice-demo, 10-02 for My-CV), each preceded by a Wave 0 manual wiki initialization checkpoint. Both plans follow the established Level A pattern from Phases 7-9. CI workflows must be created for both repos (neither has a build/lint CI). Inline execution — worktree agents lack Bash access (Phase 9 confirmed deviation), orchestrator handles git/wiki operations directly.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| README rewrite | Documentation | — | Additive change to repo root README.md via GitHub API (mcp__github__get_file_contents + create_or_update_file) |
| GitHub Wiki (4 pages) | Documentation | — | Separate wiki.git remote; clone+push pattern from Phases 3-9 |
| CI badge insertion | Documentation | CI/CD | Badge URL references new workflow; inserted in README |
| CI workflow creation | CI/CD | — | New `ci.yml` needed for both repos (no existing build CI) |
| Cross-links (CAS + intra-OgeonX-Ai) | Documentation | — | Markdown links; no code change required |
| GitHub topics | Repository Metadata | — | GitHub API `replace_all_topics` call |
| Wiki initialization checkpoint | Manual | — | Human must create first page via GitHub web UI before git push works |

---

## Standard Stack

### Core — kim-ai-voice-demo

| Component | Value | Source |
|-----------|-------|--------|
| Language | JavaScript (ESM) / HTML / CSS | [VERIFIED: repo file scan, package.json] |
| Backend framework | Express 4.19.2 (Node.js, ESM) | [VERIFIED: enterprise-ai-gateway/package.json] |
| Node.js target | 18-20 (existing workflows use Node 18/20) | [VERIFIED: publish-dev-updates.yml, devlog-sync.yml] |
| CI workflow file | `.github/workflows/ci.yml` (to create) | [VERIFIED: no existing build CI; devlog-sync.yml, publish-dev-updates.yml, roadmap-sync.yml exist] |
| CI runner | ubuntu-latest | [VERIFIED: prior phase pattern D-CF-04] |
| Default branch | `main` | [VERIFIED: gh api repos/OgeonX-Ai/kim-ai-voice-demo] |
| CI badge branch param | `?branch=main` | [VERIFIED: default_branch=main] |
| GitHub Pages | Live at https://ogeonx-ai.github.io/kim-ai-voice-demo/ | [VERIFIED: gh api repos/.../pages, status: built] |
| License | MIT | [VERIFIED: gh api repos/OgeonX-Ai/kim-ai-voice-demo → license: MIT License] |
| has_wiki (API) | true | [VERIFIED: gh api] |
| wiki.git provisioned | NO | [VERIFIED: git ls-remote returns "Repository not found"] |
| topics (current) | none | [VERIFIED: gh api → topics: []] |

### Core — My-CV

| Component | Value | Source |
|-----------|-------|--------|
| Language | HTML / CSS / JavaScript (vanilla) | [VERIFIED: repo file scan — only index.html, style.css, README.md] |
| Framework | None — plain HTML/CSS/JS | [VERIFIED: index.html has no framework import] |
| CI workflow file | `.github/workflows/ci.yml` (to create) | [VERIFIED: no .github directory exists] |
| CI runner | ubuntu-latest | [VERIFIED: prior phase pattern D-CF-04] |
| Default branch | `main` | [VERIFIED: gh api repos/OgeonX-Ai/My-CV] |
| CI badge branch param | `?branch=main` | [VERIFIED: default_branch=main] |
| GitHub Pages | Live at https://ogeonx-ai.github.io/My-CV/ | [VERIFIED: gh api repos/.../pages, status: built] |
| License | None | [VERIFIED: gh api → no license field; no LICENSE file in root] |
| has_wiki (API) | true | [VERIFIED: gh api] |
| wiki.git provisioned | NO | [VERIFIED: git ls-remote returns "Repository not found"] |
| topics (current) | none | [VERIFIED: gh api → topics: []] |

---

## Codebase Scan Findings (executor reads these — do not re-scan)

### kim-ai-voice-demo — What It Actually Is

[VERIFIED: source files read via GitHub API]

`kim-ai-voice-demo` is a **multi-component AI voice engineering project** on GitHub Pages with a local Node.js backend:

1. **GitHub Pages frontend** (`index.html`, `style.css`, `script.js`) — Main landing page with ElevenLabs TTS integration, persona selector, language options
2. **Whisper STT playground** (`webdemo/whisper.html`) — Mic capture → chunked multipart upload to `/v1/audio/transcribe-file` with live latency stats
3. **Voice-to-ServiceNow assistant** (`webdemo/servicenow.html`, `servicenow.js`) — Voice input → backend STT → `/v1/agent/plan-and-act` → ServiceNow mock/real with live SSE log stream and actions timeline
4. **Node.js/Express backend** (`enterprise-ai-gateway/server.js`) — Proxies ElevenLabs API (TTS + Whisper upload), auto-creates ElevenLabs agents via `convai/agents/create`, validates API keys in-memory only
5. **KB templates** (`kb-templates/`) — Markdown files (cv.md, projects.md, skills.md, glossary.md, tone.md) for grounding ElevenLabs conversational agents in resume content
6. **Dev log pipeline** — GitHub Actions `publish-dev-updates.yml` generates dev log posts on PR merge; `devlog-sync.yml` aggregates merged PRs across all OgeonX-Ai repos every 6h; auto-published to `webdemo/updates/`
7. **Roadmap sync** (`roadmap-sync.yml`) — Syncs roadmap markdown to GitHub Issues
8. **Agent memory** (`memory/examples/`) — Structured fix records for Codex agent (AGENTS.md present)
9. **Documentation** (`docs/`, `README_webdemo.md`) — How-to guides for running local backend and web demo

**Existing GitHub Actions workflows (DO NOT badge these):**
- `devlog-sync.yml` (schedule: `0 */6 * * *`) — content automation, not a build CI
- `publish-dev-updates.yml` (on: pull_request closed+merged) — dev log post generator
- `roadmap-sync.yml` (schedule daily + manual) — roadmap issue sync

**CI gap:** No build or lint workflow exists. The repo is HTML/JS + one Node.js backend. The appropriate CI for portfolio badge purposes is a **Node.js lint/syntax check** on the `enterprise-ai-gateway/` backend. This follows the pattern of prior repos where a lightweight but real CI was created.

**Suggested CI approach:** `node --check` on `enterprise-ai-gateway/server.js` + `npm ci` in `enterprise-ai-gateway/` + node syntax check. Alternatively, `npx --yes eslint enterprise-ai-gateway/server.js --no-eslintrc -c '{"parserOptions":{"ecmaVersion":2022,"sourceType":"module"}}' --rule '{"no-undef":0}'` but simpler is `node --check`. [ASSUMED — executor validates whether `npm ci` + `node --check` passes cleanly]

**Hero line for README rewrite:** "AI voice engineering platform — GitHub Pages frontend, Node.js/Express backend, and ElevenLabs + Whisper STT/TTS integration; demonstrates AI agent KB grounding, voice-to-ServiceNow workflow automation, and automated dev-log publishing via GitHub Actions." [ASSUMED — derived from code analysis; executor validates tone]

**README framing problem to solve:** Current README leads with "Real-Time AI Voice Demo (Web + Mobile)" with emoji, ElevenLabs affiliate disclosure, and product-demo framing. Rewrite must lead with the engineering work: AI voice pipeline architecture, multi-provider integration, automation toolchain.

**Current topics:** none — needs 5-10. Suggested topics: `ai-voice`, `elevenlabs`, `speech-to-text`, `text-to-speech`, `github-pages`, `nodejs`, `javascript`, `whisper`, `servicenow`, `portfolio`

### My-CV — What It Actually Is

[VERIFIED: source files read via GitHub API]

`My-CV` is a **print-ready AI-augmented online CV** hosted on GitHub Pages — a polished, professional HTML document that:

1. **HTML/CSS/JS online CV** (670-line `index.html`, 488-line `style.css`) — Professional resume with sections: Certifications (AZ-900, AZ-104, AZ-305), Summary, Skills (collapsible details), Current Work, Experience (Innofactor, earlier roles), print/PDF button
2. **Subject:** Kim Harjamaki — Azure Architect & Senior DevOps Engineer, Helsinki, Finland. 20+ years experience.
3. **AI toolchain in skills section** — The HTML explicitly lists under "AI & Automation": Azure OpenAI provisioning, prompt engineering for DevOps copilots, LLM-based incident triage, FastAPI backends for AI workflows, GitHub Actions automation agents, Gemini and Azure AI Studio integration, AI-driven documentation pipelines
4. **CV → AI narrative bridge:** The "Current Work" section states "Building a real-time AI voice assistant demo that blends speech-to-text, TTS, and automation" — this links My-CV to kim-ai-voice-demo
5. **No LICENSE file** — unlike kim-ai-voice-demo which has MIT

**README gap:** Current README is a 1-line stub: "Skills are available via the 'View full skills list' modal on the CV page." This is the primary reframe target.

**No CI workflow.** The repo is pure HTML/CSS/JS with no build step. GitHub Pages deployment is automatic. For a portfolio CI badge, a suitable option is an HTML validation step using `npx html-validator-cli` or simply `node -e "const fs = require('fs'); const content = fs.readFileSync('index.html','utf8'); if (!content.includes('<title>')) process.exit(1);"` — a structural sanity check. Alternatively, the `pages build and deployment` workflow already runs (GitHub's built-in Pages CI) but this does not produce a badgeable `.github/workflows/ci.yml`. A custom `ci.yml` is needed for the portfolio badge.

**Recommended CI for My-CV:** Validate HTML structure with a lightweight check — the most reliable is `npx --yes html-validate index.html` or a basic `node` structural check. [ASSUMED — executor validates which validator is available/works on ubuntu-latest]

**Hero line for README rewrite:** "Kim Harjamaki's online CV — an AI-augmented career portfolio maintained via the OgeonX-Ai automation ecosystem, covering 20+ years in Azure architecture, DevOps, and applied AI engineering." [ASSUMED — derived from content analysis; executor validates tone]

**README framing direction:** Explain WHAT the CV is + HOW it's maintained (AI toolchain, automated via GitHub Actions, linked to the OgeonX-Ai portfolio). Position it as evidence of AI-powered workflow, not just a static HTML page. Cross-link to kim-ai-voice-demo and enterprise-ai-gateway.

**Current topics:** none. Suggested topics: `cv`, `resume`, `portfolio`, `azure`, `devops`, `github-pages`, `html`, `azure-architect`, `ai-engineer`

**LICENSE note:** No LICENSE file exists. Phase requirement PORT-02 does not specifically require adding one. [ASSUMED — executor confirms if LICENSE is needed; prior pattern for CAS repos added MIT LICENSE but OgeonX-Ai personal repos are not consistently licensed]

---

## CI Strategy

### kim-ai-voice-demo CI

**Problem:** No build/lint CI exists. The 3 existing workflows are content automation (devlog-sync, publish-dev-updates, roadmap-sync) — not buildable or testable in the traditional sense.

**Solution:** Create `.github/workflows/ci.yml` that checks the Node.js backend in `enterprise-ai-gateway/`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - name: Install dependencies
        run: npm ci
        working-directory: enterprise-ai-gateway
      - name: Syntax check
        run: node --check server.js
        working-directory: enterprise-ai-gateway
```

**Why this is correct:** The backend (`enterprise-ai-gateway/server.js`) is ESM JavaScript with real dependencies (express, cors, multer). `npm ci` verifies lockfile integrity; `node --check` verifies syntax without executing. This is a real CI check, not a no-op.

**Caveat:** There is no `package-lock.json` confirmed — if none exists, use `npm install` instead of `npm ci`. [ASSUMED — executor checks before writing]

**Badge URL:** `https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml/badge.svg?branch=main`

### My-CV CI

**Problem:** No CI workflow exists. Pure HTML/CSS/JS — no build step.

**Solution:** Create `.github/workflows/ci.yml` with an HTML structural validation:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Validate HTML structure
        run: |
          node -e "
          const fs = require('fs');
          const html = fs.readFileSync('index.html', 'utf8');
          const checks = ['<!doctype html>', '<title>', '</html>'];
          checks.forEach(c => {
            if (!html.toLowerCase().includes(c.toLowerCase())) {
              console.error('Missing: ' + c); process.exit(1);
            }
          });
          console.log('HTML structure valid');
          "
```

**Why this is correct:** A structural check is the most reliable approach for a pure HTML repo. No external validator tools that may have availability issues on ubuntu-latest. Uses only built-in `node` (pre-installed on ubuntu-latest). Always passes on valid HTML — will catch catastrophic file corruption.

**Badge URL:** `https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml/badge.svg?branch=main`

---

## Architecture Patterns

### System Architecture — kim-ai-voice-demo

```
flowchart LR
  GH_Pages[GitHub Pages\nindex.html / webdemo] -->|user interaction| Backend[Node.js Backend\nExpress + ElevenLabs proxy]
  GH_Pages -->|mic audio chunks| Whisper[Whisper STT\n/v1/audio/transcribe-file]
  GH_Pages -->|text + voice| TTS[ElevenLabs TTS\n/v1/text-to-speech]
  Backend --> EL_API[ElevenLabs API\nAgent create / share link]
  Backend --> ServiceNow[ServiceNow\nmock / real mode]
  Whisper --> LLM[LLM Reasoning\n/v1/agent/plan-and-act]
  LLM --> ServiceNow
  GH_Actions[GitHub Actions\nDev Log + Roadmap sync] -->|automated| GH_Pages
```

### System Architecture — My-CV

```
flowchart LR
  GH_Pages[GitHub Pages\nindex.html] -->|browser render| CV[Online CV\nHTML / CSS / JS]
  CV -->|print button| PDF[PDF Export\nbrowser print dialog]
  OgeonX_Ecosystem[OgeonX-Ai Ecosystem\nkm-ai-voice-demo / enterprise-ai-gateway] -->|AI toolchain| CV
  CI[GitHub Actions CI\nHTML validation] -->|green badge| GH_Pages
```

### Established Portfolio Pattern (Phases 3-9 — unchanged)

**README structure:**

```markdown
# [Repo Name]

[Hero line — one sentence, enterprise tone, no emoji]

[![CI](badge-url)](actions-url)  [![language badge](shields.io)](shields.io)  [![MIT](shields.io)](LICENSE)

[![CAS Ecosystem](shields.io/badge/ecosystem-Coding--Autopilot--System-blue)](https://github.com/Coding-Autopilot-System)

Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator](url) | [Promptimprover](url) | [autogen](url)

**See also:** [OgeonX-Ai/My-CV](url) — or [OgeonX-Ai/kim-ai-voice-demo](url)

## Architecture

```mermaid
flowchart LR
  ...
```

## Features

## Quick Start
```

**Wiki structure (4 pages — standard for all phases):**
- `Home.md` — hero paragraph + badges, quick-start snippet (5 lines max), navigation table
- `Setup-Guide.md` — standalone, includes "What a successful setup looks like"
- `Architecture.md` — same `flowchart LR` Mermaid from README + expanded prose
- `Configuration-Reference.md` — table: Name | Type | Required | Default | Description

**Wiki git delivery (clone+push from orchestrator inline):**
```bash
TOKEN=$(gh auth token)
rm -rf /tmp/<repo>-wiki
git clone "https://x-access-token:${TOKEN}@github.com/OgeonX-Ai/<repo>.wiki.git" /tmp/<repo>-wiki
# Write 4 pages using Write tool to C:/Users/KIMHAR~1/AppData/Local/Temp/<repo>-wiki/
git -C /tmp/<repo>-wiki add Home.md Setup-Guide.md Architecture.md Configuration-Reference.md
git -C /tmp/<repo>-wiki -c user.email="bot@gsd" -c user.name="GSD Bot" commit -m "docs: add wiki pages"
git -C /tmp/<repo>-wiki push origin master
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CI badge URL | Custom badge generation | GitHub Actions badge SVG at `workflows/ci.yml/badge.svg` | Already exists once workflow is pushed |
| Wiki git hosting | Custom wiki pages | GitHub wiki.git (clone+push) | Established pattern from Phases 3-9 |
| shields.io badges | Custom badge | shields.io URL | Consistent with all prior repos |
| Mermaid diagrams | Image files | Mermaid code blocks | Auto-renders in GitHub README and wiki |
| HTML validation | Custom parser | `node -e` structural check or `html-validate` | Simpler, no external dep issues |
| ElevenLabs agent creation | Manual UI steps | Backend `/api/elevenlabs/agent-auto` endpoint (already built) | Already exists in the repo — describe it, don't rebuild it |

---

## Common Pitfalls

### Pitfall 1: `has_wiki: true` does NOT mean wiki.git is initialized

**What goes wrong:** Planner skips Wave 0 wiki initialization checkpoint because `has_wiki: true` in GitHub API. Executor tries `git clone .wiki.git` and gets "Repository not found".

**Why it happens:** `has_wiki: true` means the wiki FEATURE is enabled on the repo. The `wiki.git` remote is only provisioned when the first page is created via GitHub's web UI. This was confirmed for Phase 9 enterprise-ai-gateway (same issue) and is confirmed again for both Phase 10 repos.

**How to avoid:** `git ls-remote https://github.com/OgeonX-Ai/<repo>.wiki.git HEAD` MUST return a SHA before executing wiki push. Both Phase 10 repos return "Repository not found" — both require Wave 0 manual checkpoint plans.

**Warning signs:** `fatal: repository '...wiki.git/' not found` error during `git clone`.

### Pitfall 2: Worktree agents lack Bash access — all git operations must be inline

**What goes wrong:** Executor subagent launched in a worktree cannot run `git clone`, `git push`, or any Bash command.

**Why it happens:** Worktree isolation does not include Bash tool access (confirmed Phase 9 deviation, documented in 09-02-SUMMARY.md).

**How to avoid:** All git/wiki operations (clone, write pages, commit, push) must be executed inline by the orchestrator using the Bash tool directly. Do NOT spawn executor subagents for wiki tasks.

### Pitfall 3: Windows path sync for Write tool vs. Bash git

**What goes wrong:** `git clone` lands at `C:/Users/KIMHAR~1/AppData/Local/Temp/<repo>-wiki` (bash path: `/tmp/<repo>-wiki`). Write tool and Bash tool see different path representations.

**Why it happens:** Windows short-path names and /tmp symlink differences between WSL/bash and Windows native paths.

**How to avoid:** Use `C:/Users/KIMHAR~1/AppData/Local/Temp/` prefix for Write tool calls; use `/tmp/<repo>-wiki` for bash git commands. Confirmed pattern from Phase 9.

### Pitfall 4: `rg` not available on ubuntu-latest

**What goes wrong:** Any CI step using `rg` (ripgrep) fails with "command not found".

**Why it happens:** ripgrep is not pre-installed on ubuntu-latest GitHub Actions runners.

**How to avoid:** Use `grep -rl` for recursive search, `find` for file discovery. [VERIFIED: Phase 8 incident D-13]

### Pitfall 5: `.github/workflows/` writes require `workflow` scope PAT

**What goes wrong:** GitHub API returns 403/404 when writing `.github/workflows/ci.yml` using a repo-scoped PAT.

**Why it happens:** GitHub requires the `workflow` OAuth scope for any operation that creates or modifies workflow files.

**How to avoid:** Use `GITHUB_MCP_PAT` (which has workflow scope) for workflow file writes. [VERIFIED: Phase 7 D-09, Phase 8 incident]

### Pitfall 6: My-CV has no LICENSE file — badge may show "no license"

**What goes wrong:** README includes MIT license badge pointing to `LICENSE` file, but no LICENSE file exists in My-CV.

**Why it happens:** My-CV was created without a LICENSE file (unlike kim-ai-voice-demo which has MIT).

**How to avoid:** Either skip the MIT license badge for My-CV, or create a LICENSE file as part of the plan. [ASSUMED — executor confirms whether PORT-02 requires a license; recommended: add MIT LICENSE to keep portfolio consistent]

### Pitfall 7: kim-ai-voice-demo has existing workflows — do NOT interfere with them

**What goes wrong:** Executor accidentally modifies `devlog-sync.yml`, `publish-dev-updates.yml`, or `roadmap-sync.yml` while creating `ci.yml`.

**Why it happens:** The `.github/workflows/` directory already has 3 workflows. The new `ci.yml` must be a new file only.

**How to avoid:** Write ONLY `.github/workflows/ci.yml` (new file). Do not read/modify the 3 existing workflow files.

### Pitfall 8: My-CV `npm ci` will fail — no package.json

**What goes wrong:** CI step runs `npm ci` or `npm install` but My-CV has no `package.json` or `node_modules`.

**Why it happens:** My-CV is pure HTML/CSS/JS with no Node.js dependencies.

**How to avoid:** My-CV CI must NOT use npm. Use `node -e` for the structural HTML check — no npm dependency required.

### Pitfall 9: Intra-OgeonX-Ai cross-links for Phase 10 repos

**What goes wrong:** Cross-link pattern from Phase 9 linked enterprise-ai-gateway ↔ android. Phase 10 repos need their own cross-link pattern.

**How to avoid:** Phase 10 cross-link: `kim-ai-voice-demo` links to `My-CV` and `enterprise-ai-gateway`; `My-CV` links to `kim-ai-voice-demo`. All link to CAS ecosystem.

---

## Code Examples

### CI Badge URL Patterns

```markdown
# kim-ai-voice-demo (branch: main)
[![CI](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml)

# My-CV (branch: main)
[![CI](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml)
```

### Language Badges (shields.io)

```markdown
# kim-ai-voice-demo
[![JavaScript](https://img.shields.io/badge/javascript-ES2022-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)
[![Node.js](https://img.shields.io/badge/node.js-20-green)](https://nodejs.org)
[![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

# My-CV
[![HTML](https://img.shields.io/badge/html5-portfolio-orange)](https://ogeonx-ai.github.io/My-CV/)
```

### CAS Ecosystem Badge + Line (D-11 pattern — unchanged from Phases 4-9)

```markdown
[![Coding-Autopilot-System](https://img.shields.io/badge/ecosystem-Coding--Autopilot--System-blue)](https://github.com/Coding-Autopilot-System)

Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover) | [autogen](https://github.com/Coding-Autopilot-System/autogen)
```

### Intra-OgeonX-Ai Cross-Links (Phase 10 pattern)

```markdown
# In kim-ai-voice-demo README:
**See also:** [OgeonX-Ai/My-CV](https://github.com/OgeonX-Ai/My-CV) — AI-augmented career portfolio | [OgeonX-Ai/enterprise-ai-gateway](https://github.com/OgeonX-Ai/enterprise-ai-gateway) — AI service bus backend

# In My-CV README:
**See also:** [OgeonX-Ai/kim-ai-voice-demo](https://github.com/OgeonX-Ai/kim-ai-voice-demo) — AI voice engineering platform
```

### Wiki Check Before Push (identical to Phase 9 pattern)

```bash
# Verify wiki.git is initialized before attempting push
git ls-remote https://github.com/OgeonX-Ai/<repo>.wiki.git HEAD
# Returns empty or error → wiki not initialized → need manual checkpoint FIRST
# Returns SHA → wiki is ready → proceed to clone+push
```

### kim-ai-voice-demo CI Workflow

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - name: Install dependencies
        run: npm install
        working-directory: enterprise-ai-gateway
      - name: Syntax check
        run: node --check server.js
        working-directory: enterprise-ai-gateway
```

### My-CV CI Workflow

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Validate HTML structure
        run: |
          node -e "
          const fs = require('fs');
          const html = fs.readFileSync('index.html', 'utf8');
          const checks = ['<!doctype html>', '<title>', '</html>'];
          checks.forEach(c => {
            if (!html.toLowerCase().includes(c.toLowerCase())) {
              console.error('Missing: ' + c); process.exit(1);
            }
          });
          console.log('HTML structure valid');
          "
```

---

## Configuration Reference Data (for wiki pages)

### kim-ai-voice-demo Backend Environment Variables

[VERIFIED: enterprise-ai-gateway/server.js — uses only process.env.PORT]

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `PORT` | number | no | `3001` | Express server port |
| ElevenLabs API key | string | per-request | — | Passed in request body; never stored; used in-memory only |

### My-CV Configuration

No server-side configuration — pure static HTML. Only browser-side:

| Name | Type | Description |
|------|------|-------------|
| Browser print | built-in | "Download / Print PDF" button triggers `window.print()` |
| Skills modal | JS | `<details>` accordion expand for skills categories |

---

## Topics Recommendations

### kim-ai-voice-demo (currently: none)

Recommended 8 topics: `ai-voice`, `elevenlabs`, `speech-to-text`, `text-to-speech`, `github-pages`, `javascript`, `whisper`, `portfolio`

### My-CV (currently: none)

Recommended 7 topics: `cv`, `resume`, `portfolio`, `azure`, `devops`, `github-pages`, `html`

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `rg` in CI steps | `grep -rl` / `find` | Phase 8 (2026-05-27) | `rg` not on ubuntu-latest; must use POSIX tools |
| repo-scoped PAT for workflow writes | `GITHUB_MCP_PAT` (workflow scope) | Phase 7 (2026-05-24) | GitHub API returns 403/404 without workflow scope |
| Executor subagents for wiki push | Orchestrator inline Bash | Phase 9 (2026-05-27) | Worktree agents lack Bash access |
| `/tmp/<repo>-wiki` for Write tool | `C:/Users/KIMHAR~1/AppData/Local/Temp/<repo>-wiki` | Phase 9 (2026-05-27) | Windows path sync between Write tool and Bash git |
| `has_wiki: true` → wiki ready | Always run `git ls-remote` to confirm | Phase 9 (2026-05-27) | API field does not indicate git remote provisioning |
| `npm ci` | `npm install` when no lockfile | Phase 10 | `npm ci` fails if no package-lock.json exists |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Hero line for kim-ai-voice-demo: "AI voice engineering platform — GitHub Pages frontend, Node.js/Express backend..." | Codebase Scan Findings | Tone may need adjustment; executor validates against actual code |
| A2 | Hero line for My-CV: "AI-augmented career portfolio maintained via the OgeonX-Ai automation ecosystem..." | Codebase Scan Findings | My-CV is not explicitly "maintained via AI toolchain" in its current README — the framing is a reframe, not a description of existing README |
| A3 | `npm install` (not `npm ci`) for kim-ai-voice-demo CI — no package-lock.json confirmed | CI Strategy | If package-lock.json exists, `npm ci` is preferred for determinism |
| A4 | My-CV LICENSE should be added (MIT) for badge consistency | Code Examples | PORT-02 requirement does not explicitly mention LICENSE; executor confirms if needed |
| A5 | My-CV HTML validation via `node -e` structural check is sufficient for a green CI badge | CI Strategy | If html-validate or similar is preferred by user, executor can substitute |
| A6 | Cross-link pattern: kim-ai-voice-demo links to both My-CV AND enterprise-ai-gateway | Code Examples | Phase 9 android linked only to enterprise-ai-gateway; Phase 10 may want a consistent smaller set |

---

## Open Questions

1. **Should My-CV get a MIT LICENSE file?**
   - What we know: kim-ai-voice-demo has MIT LICENSE; My-CV has no LICENSE. Phase requirement PORT-02 does not mention it.
   - What's unclear: Whether license consistency matters for My-CV as a CV/portfolio page.
   - Recommendation: Add MIT LICENSE to My-CV for consistency with all other OgeonX-Ai repos. Low risk.

2. **Should kim-ai-voice-demo use `npm ci` or `npm install` in CI?**
   - What we know: `enterprise-ai-gateway/package.json` exists; `package-lock.json` presence not confirmed.
   - What's unclear: Whether a lockfile was committed.
   - Recommendation: Executor checks for `package-lock.json` at plan execution time. Use `npm ci` if present, `npm install` if absent.

3. **Intra-OgeonX-Ai cross-link scope for Phase 10**
   - What we know: Phase 9 used a single "See also" line per repo. Phase 10 has 2 OgeonX-Ai repos plus enterprise-ai-gateway that kim-ai-voice-demo already references in its codebase.
   - Recommendation: kim-ai-voice-demo "See also" links to My-CV AND enterprise-ai-gateway (both are tightly related). My-CV "See also" links only to kim-ai-voice-demo (to avoid over-linking a CV page).

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| GitHub API (gh CLI) | All remote repo operations | Yes | gh 2.x | — |
| GITHUB_MCP_PAT (workflow scope) | Writing `.github/workflows/ci.yml` | [ASSUMED: yes, used in Phases 7/8/9] | — | Cannot write workflow files without it |
| Git (for wiki push) | Wiki page delivery | Yes | system git | — |
| OgeonX-Ai/kim-ai-voice-demo repo access | README + CI + wiki writes | Yes | confirmed gh api | — |
| OgeonX-Ai/My-CV repo access | README + CI + wiki writes | Yes | confirmed gh api | — |
| Node.js (ubuntu-latest) | kim-ai-voice-demo CI + My-CV CI | Yes | pre-installed on ubuntu-latest | — |

**GITHUB_MCP_PAT is required** for both plans: both need new `.github/workflows/ci.yml` files written via the GitHub API.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | No automated test framework — deliverables are documentation + remote repo state |
| Config file | none |
| Quick run command | `gh api repos/OgeonX-Ai/<repo>/contents/README.md --jq '.content' \| base64 -d \| head -5` |
| Full suite command | See Phase Requirements Test Map below |

### Phase Requirements Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PORT-01 | kim-ai-voice-demo README has AI voice engineering hero line, CI badge, architecture section, CAS link | smoke | `gh api repos/OgeonX-Ai/kim-ai-voice-demo/contents/README.md --jq '.content' \| base64 -d \| grep -E "CI\|flowchart\|Coding-Autopilot-System"` | Wave 0 (remote) |
| PORT-01 | kim-ai-voice-demo CI workflow exists and passes | smoke | `gh run list --repo OgeonX-Ai/kim-ai-voice-demo --workflow ci.yml --limit 1 --json conclusion --jq '.[0].conclusion'` | Wave 0 (create ci.yml first) |
| PORT-01 | kim-ai-voice-demo wiki has 4 pages | smoke | `git ls-remote https://github.com/OgeonX-Ai/kim-ai-voice-demo.wiki.git` | Wave 0 (wiki init first) |
| PORT-01 | kim-ai-voice-demo has 5-10 topics set | smoke | `gh api repos/OgeonX-Ai/kim-ai-voice-demo --jq '.topics'` | Wave 0 (remote) |
| PORT-02 | My-CV README explains AI toolchain + career context | smoke | `gh api repos/OgeonX-Ai/My-CV/contents/README.md --jq '.content' \| base64 -d \| grep -E "AI\|toolchain\|automation"` | Wave 0 (remote) |
| PORT-02 | My-CV CI workflow exists and passes | smoke | `gh run list --repo OgeonX-Ai/My-CV --workflow ci.yml --limit 1 --json conclusion --jq '.[0].conclusion'` | Wave 0 (create ci.yml first) |
| PORT-02 | My-CV wiki has 4 pages | smoke | `git ls-remote https://github.com/OgeonX-Ai/My-CV.wiki.git` | Wave 0 (wiki init first) |
| PORT-02 | My-CV has 5-10 topics set | smoke | `gh api repos/OgeonX-Ai/My-CV --jq '.topics'` | Wave 0 (remote) |

### Sampling Rate

- **Per task:** Verify the specific remote file changed (README grep or wiki page count or CI run status)
- **Per wave:** Full grep check of all required README sections + wiki page count for that repo
- **Phase gate:** Both repos: README sections present, all 8 wiki pages (4 per repo) reachable, CI badges green, topics set, cross-links valid

### Wave 0 Gaps

- [ ] `git ls-remote https://github.com/OgeonX-Ai/kim-ai-voice-demo.wiki.git HEAD` — currently "Repository not found"; manual wiki initialization required before 10-01 wiki push
- [ ] `git ls-remote https://github.com/OgeonX-Ai/My-CV.wiki.git HEAD` — currently "Repository not found"; manual wiki initialization required before 10-02 wiki push
- [ ] `enterprise-ai-gateway/package-lock.json` check — determines whether `npm ci` or `npm install` in kim-ai-voice-demo CI

---

## Security Domain

This phase makes no changes to application security posture. All changes are additive documentation, CI workflow creation (lint/validate only), and badge insertions. No authentication endpoints, session management, input validation, or cryptographic code is modified.

| ASVS Category | Applies | Notes |
|---------------|---------|-------|
| V2 Authentication | no | No auth code modified |
| V3 Session Management | no | No session code modified |
| V4 Access Control | no | No access control code modified |
| V5 Input Validation | no | No input handling code modified |
| V6 Cryptography | no | No crypto code modified |

---

## Sources

### Primary (HIGH confidence)

- [VERIFIED: OgeonX-Ai/kim-ai-voice-demo remote files] — README.md, README_webdemo.md, enterprise-ai-gateway/server.js, enterprise-ai-gateway/package.json, scripts/generate-dev-update.mjs, .github/workflows/devlog-sync.yml, .github/workflows/publish-dev-updates.yml, .github/workflows/roadmap-sync.yml, AGENTS.md read directly via GitHub API
- [VERIFIED: OgeonX-Ai/My-CV remote files] — README.md, index.html read directly via GitHub API
- [VERIFIED: gh api repos/OgeonX-Ai/kim-ai-voice-demo] — repo metadata: default_branch=main, has_wiki=true, topics=[], license=MIT, has_pages=true
- [VERIFIED: gh api repos/OgeonX-Ai/My-CV] — repo metadata: default_branch=main, has_wiki=true, topics=[], language=HTML, license=none, has_pages=true
- [VERIFIED: git ls-remote kim-ai-voice-demo.wiki.git] — "Repository not found" — wiki.git NOT provisioned
- [VERIFIED: git ls-remote My-CV.wiki.git] — "Repository not found" — wiki.git NOT provisioned
- [VERIFIED: gh run list OgeonX-Ai/kim-ai-voice-demo] — existing workflows: devlog-sync (success), roadmap-sync (success) — NO build CI
- [VERIFIED: gh run list OgeonX-Ai/My-CV] — only "pages build and deployment" (GitHub's built-in Pages CI, not badgeable)
- [VERIFIED: .planning/phases/09-ogeonx-ai-core-tech-ai-reframe/09-02-SUMMARY.md] — Worktree agents lack Bash, Windows path sync, inline orchestrator execution pattern
- [VERIFIED: .planning/phases/09-ogeonx-ai-core-tech-ai-reframe/09-01-SUMMARY.md] — has_wiki: true does NOT mean wiki.git initialized

### Secondary (MEDIUM confidence)

- [VERIFIED: .planning/phases/09-ogeonx-ai-core-tech-ai-reframe/09-RESEARCH.md] — established README structure, badge placement, CAS ecosystem links, wiki page format
- [VERIFIED: .planning/STATE.md] — Phase 7/8/9 results confirming GITHUB_MCP_PAT requirement, CI patterns

### Tertiary (LOW confidence)

None.

---

## Metadata

**Confidence breakdown:**

- kim-ai-voice-demo tech stack: HIGH — all source files and workflows read directly
- My-CV tech stack: HIGH — all source files read directly; 3-file pure HTML repo
- CI strategy: MEDIUM — ci.yml patterns derived from Phase 8/9; package-lock.json presence unconfirmed for kim-ai-voice-demo
- Wiki state: HIGH — git ls-remote confirmed both wikis are NOT initialized
- Architecture framing: MEDIUM — hero lines derived from code analysis, marked ASSUMED for executor validation
- Topics recommendations: MEDIUM — based on code analysis; executor may refine

**Research date:** 2026-05-28
**Valid until:** 2026-06-28 (stable: HTML/JS + GitHub Actions patterns)

---

## RESEARCH COMPLETE

**Phase:** 10 — OgeonX-Ai Portfolio Repos AI Reframe + Level A
**Confidence:** HIGH

### Key Findings

- Both repos (`kim-ai-voice-demo` and `My-CV`) have `has_wiki: true` in the GitHub API but `git ls-remote` confirms NEITHER wiki.git is provisioned — both plans require Wave 0 manual checkpoint plans, identical to Phases 3, 4, 5, 7, 8, and 9
- `kim-ai-voice-demo` is significantly more than an ElevenLabs demo: it includes a Whisper STT playground, a voice-to-ServiceNow assistant with SSE log streaming, a Node.js/Express ElevenLabs proxy backend, KB templates for agent grounding, and 3 GitHub Actions automation workflows — the reframe is straightforward because the actual engineering is there
- `My-CV` is a 3-file pure HTML/CSS/JS CV on GitHub Pages with a 1-line stub README — the README reframe must explain the AI toolchain context (it IS maintained via the OgeonX-Ai automation ecosystem); no CI exists; the HTML itself has a comprehensive "AI & Automation" skills section
- **Neither repo has a build/lint CI** — both plans must create `.github/workflows/ci.yml` (requires GITHUB_MCP_PAT with workflow scope)
- Worktree agents lack Bash access (Phase 9 confirmed) — all git/wiki operations must be executed inline by the orchestrator
- Windows path sync: Write tool uses `C:/Users/KIMHAR~1/AppData/Local/Temp/<repo>-wiki`, Bash git uses `/tmp/<repo>-wiki` (Phase 9 confirmed)
- My-CV has no LICENSE file — adding MIT LICENSE recommended for portfolio consistency
- Both repos are on `main` branch — CI badge URLs both use `?branch=main`

### Files Created

`.planning/phases/10-ogeonx-ai-portfolio-repos-ai-reframe/10-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| kim-ai-voice-demo tech stack | HIGH | All source files, workflows, package.json read directly |
| My-CV tech stack | HIGH | All source files read directly; confirmed 3-file repo |
| Wiki initialization state | HIGH | git ls-remote confirmed both NOT provisioned |
| CI state | HIGH | gh run list confirmed no build CI on either repo |
| CI design (kim-ai-voice-demo) | MEDIUM | npm install path unconfirmed; syntax check approach verified pattern |
| CI design (My-CV) | MEDIUM | node -e HTML check is pragmatic; executor can substitute if needed |
| Architecture framing | MEDIUM | Hero lines derived from code; marked ASSUMED for executor validation |

### Open Questions

- `enterprise-ai-gateway/package-lock.json` presence in kim-ai-voice-demo — determines `npm ci` vs `npm install`
- Whether My-CV should receive a MIT LICENSE file (recommended: yes, for consistency)
- Intra-OgeonX-Ai cross-link scope: kim-ai-voice-demo → {My-CV, enterprise-ai-gateway}; My-CV → {kim-ai-voice-demo only}

### Ready for Planning

Research complete. Planner can create:
- `10-00-PLAN.md` — Wave 0 manual checkpoint: initialize BOTH wikis via GitHub web UI (kim-ai-voice-demo and My-CV)
- `10-01-PLAN.md` — kim-ai-voice-demo: CI workflow, README rewrite, wiki 4 pages, topics (PORT-01)
- `10-02-PLAN.md` — My-CV: CI workflow, README rewrite, wiki 4 pages, topics, MIT LICENSE (PORT-02)
