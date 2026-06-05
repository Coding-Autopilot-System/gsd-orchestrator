# Phase 10: OgeonX-Ai Portfolio Repos AI Reframe + Level A — Pattern Map

**Mapped:** 2026-05-28
**Files analyzed:** 12 deliverables (2 CI workflows + 2 README rewrites + 8 wiki pages + 2 topics + 1 LICENSE)
**Analogs found:** 13 / 13

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `OgeonX-Ai/kim-ai-voice-demo/.github/workflows/ci.yml` | CI workflow (new) | request-response | `08-cas-secondary-repos-level-a` Node.js CI pattern (D-CF-04) | role-match |
| `OgeonX-Ai/kim-ai-voice-demo/README.md` | documentation (README rewrite) | transform (rewrite existing) | `09-01-SUMMARY.md` Task 1 — enterprise-ai-gateway README rewrite | exact |
| `kim-ai-voice-demo.wiki.git/Home.md` | documentation (wiki) | file-I/O (git clone + push) | `09-02-SUMMARY.md` Task 2 — android Home.md wiki push | exact |
| `kim-ai-voice-demo.wiki.git/Setup-Guide.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Setup-Guide.md | exact |
| `kim-ai-voice-demo.wiki.git/Architecture.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Architecture.md | exact |
| `kim-ai-voice-demo.wiki.git/Configuration-Reference.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Configuration-Reference.md | exact |
| `OgeonX-Ai/My-CV/.github/workflows/ci.yml` | CI workflow (new) | request-response | `10-RESEARCH.md` My-CV CI strategy (node -e HTML check) | no prior analog — new pattern |
| `OgeonX-Ai/My-CV/README.md` | documentation (README rewrite) | transform (rewrite existing) | `09-01-SUMMARY.md` Task 1 — enterprise-ai-gateway README rewrite | exact |
| `My-CV.wiki.git/Home.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Home.md | exact |
| `My-CV.wiki.git/Setup-Guide.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Setup-Guide.md | exact |
| `My-CV.wiki.git/Architecture.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Architecture.md | exact |
| `My-CV.wiki.git/Configuration-Reference.md` | documentation (wiki) | file-I/O | `09-02-SUMMARY.md` — android Configuration-Reference.md | exact |
| `OgeonX-Ai/My-CV/LICENSE` | config (new file) | file-I/O | Phase 8/9 MIT LICENSE pattern (all CAS/OgeonX repos use MIT) | role-match |
| Wave 0: kim-ai-voice-demo wiki initialization | checkpoint (human gate) | event-driven | `09-02-SUMMARY.md` Task 0 — android wiki initialization | exact |
| Wave 0: My-CV wiki initialization | checkpoint (human gate) | event-driven | `09-02-SUMMARY.md` Task 0 — android wiki initialization | exact |

**Note:** Both CI workflows are NEW files (neither repo has a build/lint CI). Both repos are on `main` branch. Both badge URLs use `?branch=main`.

---

## Pattern Assignments

### `OgeonX-Ai/kim-ai-voice-demo/.github/workflows/ci.yml` (CI workflow, new file)

**Analog:** `10-RESEARCH.md` CI Strategy — kim-ai-voice-demo (lines 152-178)

**CRITICAL:** Writing to `.github/workflows/` requires `GITHUB_MCP_PAT` (workflow scope), NOT a repo-scoped PAT. Verified from Phase 7 D-09 and Phase 8 incident. Using the wrong token causes 403/404.

**New file pattern** (omit sha parameter entirely — file does not exist yet):
```
Step 1: mcp__github__create_or_update_file
        owner: "OgeonX-Ai"
        repo: "kim-ai-voice-demo"
        path: ".github/workflows/ci.yml"
        branch: "main"
        message: "ci: add Node.js syntax check CI workflow"
        content: [base64-encoded ci.yml content]
        sha: [OMIT — new file, no sha needed]
```

**CI workflow content** (10-RESEARCH.md lines 438-462 — use `npm install` not `npm ci`):
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

**Why `npm install` not `npm ci`:** `package-lock.json` presence in `enterprise-ai-gateway/` is unconfirmed (A3 from RESEARCH.md). Executor must check for `package-lock.json` via `mcp__github__get_file_contents` before writing the workflow — use `npm ci` if lockfile exists, `npm install` if absent. [10-RESEARCH.md line 539]

**Do NOT touch** existing workflows: `devlog-sync.yml`, `publish-dev-updates.yml`, `roadmap-sync.yml`. Write ONLY `ci.yml` (new file). [Pitfall 7, 10-RESEARCH.md lines 363-369]

**Badge URL** (for README insertion):
```markdown
[![CI](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml)
```

---

### `OgeonX-Ai/My-CV/.github/workflows/ci.yml` (CI workflow, new file)

**Analog:** `10-RESEARCH.md` CI Strategy — My-CV (lines 184-220 — no prior phase analog; this is a new pattern)

**CRITICAL:** Same `GITHUB_MCP_PAT` requirement as kim-ai-voice-demo. My-CV has NO `.github/` directory — this creates both the directory and the file. [10-RESEARCH.md line 73]

**New file pattern** (omit sha — file does not exist, directory does not exist):
```
Step 1: mcp__github__create_or_update_file
        owner: "OgeonX-Ai"
        repo: "My-CV"
        path: ".github/workflows/ci.yml"
        branch: "main"
        message: "ci: add HTML structure validation CI workflow"
        content: [base64-encoded ci.yml content]
        sha: [OMIT — new file]
```

**CI workflow content** (10-RESEARCH.md lines 464-492 — pure `node -e`, NO npm):
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

**Why `node -e` not npm:** My-CV has NO `package.json`. `npm ci` or `npm install` will fail. Node.js is pre-installed on ubuntu-latest — the `node -e` check requires zero dependencies. [Pitfall 8, 10-RESEARCH.md lines 373-377]

**Why no `rg`:** `rg` (ripgrep) is not on ubuntu-latest. This workflow avoids it entirely. [Pitfall 4, 10-RESEARCH.md lines 340-345]

**Badge URL** (for README insertion):
```markdown
[![CI](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml)
```

---

### `OgeonX-Ai/kim-ai-voice-demo/README.md` (documentation, transform)

**Analog:** `09-01-SUMMARY.md` Task 1 — enterprise-ai-gateway README rewrite (lines 33-111). Match quality: exact.

**Fetch-SHA-then-update pattern** (mandatory for existing files — 09-PATTERNS.md Shared Patterns lines 507-518):
```
Step 1: mcp__github__get_file_contents
        owner: "OgeonX-Ai"
        repo: "kim-ai-voice-demo"
        path: "README.md"
        → capture the `sha` field (MANDATORY — omitting causes 409 Conflict)
Step 2: Compose full new README content
Step 3: mcp__github__create_or_update_file
        sha: [captured]
        branch: "main"
```

**README section order** (09-PATTERNS.md lines 44-70, adapted for kim-ai-voice-demo):
```markdown
# kim-ai-voice-demo

[Hero line — one sentence, enterprise tone, no emoji]

[![CI](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml)  [![JavaScript](https://img.shields.io/badge/javascript-ES2022-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)  [![Node.js](https://img.shields.io/badge/node.js-20-green)](https://nodejs.org)  [![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[![Coding-Autopilot-System](https://img.shields.io/badge/ecosystem-Coding--Autopilot--System-blue)](https://github.com/Coding-Autopilot-System)

Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover) | [autogen](https://github.com/Coding-Autopilot-System/autogen)

**See also:** [OgeonX-Ai/My-CV](https://github.com/OgeonX-Ai/My-CV) — AI-augmented career portfolio | [OgeonX-Ai/enterprise-ai-gateway](https://github.com/OgeonX-Ai/enterprise-ai-gateway) — AI service bus backend

## Architecture

```mermaid
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

[Architecture prose]

## Features

[bullet points — AI capabilities lead]

## Quick Start

[5-line bash snippet — git clone + cd + npm install in enterprise-ai-gateway]
```

**Hero line** (10-RESEARCH.md line 112, ASSUMED — executor validates tone):
"AI voice engineering platform — GitHub Pages frontend, Node.js/Express backend, and ElevenLabs + Whisper STT/TTS integration; demonstrates AI agent KB grounding, voice-to-ServiceNow workflow automation, and automated dev-log publishing via GitHub Actions."

**Reframe direction** (10-RESEARCH.md lines 113-115): Move AWAY from "Real-Time AI Voice Demo" and "ElevenLabs affiliate" framing. Lead with the engineering: AI voice pipeline architecture, multi-provider integration, automation toolchain.

**Topics to set** (10-RESEARCH.md lines 521-522 — 8 topics):
`ai-voice`, `elevenlabs`, `speech-to-text`, `text-to-speech`, `github-pages`, `javascript`, `whisper`, `portfolio`

**Topics API call:**
```
mcp__github__replace_all_topics
owner: "OgeonX-Ai"
repo: "kim-ai-voice-demo"
names: ["ai-voice", "elevenlabs", "speech-to-text", "text-to-speech", "github-pages", "javascript", "whisper", "portfolio"]
```

**Commit message pattern** (09-01-SUMMARY.md line 18, adapted):
```
docs: Level A README — hero line, CI badge, architecture diagram, CAS ecosystem link, cross-links to My-CV and enterprise-ai-gateway
```

---

### `OgeonX-Ai/My-CV/README.md` (documentation, transform)

**Analog:** `09-01-SUMMARY.md` Task 1 — enterprise-ai-gateway README rewrite (lines 33-111). Match quality: exact.

**Fetch-SHA-then-update pattern** (same as kim-ai-voice-demo — mandatory for existing file):
```
Step 1: mcp__github__get_file_contents
        owner: "OgeonX-Ai"
        repo: "My-CV"
        path: "README.md"
        → capture the `sha` field (MANDATORY)
Step 2: Compose full new README content
Step 3: mcp__github__create_or_update_file
        sha: [captured]
        branch: "main"
```

**README section order** (adapted for My-CV — simplified, no backend/Quick Start needed):
```markdown
# My-CV

[Hero line — one sentence, AI toolchain framing, no emoji]

[![CI](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml)  [![HTML](https://img.shields.io/badge/html5-portfolio-orange)](https://ogeonx-ai.github.io/My-CV/)

[![Coding-Autopilot-System](https://img.shields.io/badge/ecosystem-Coding--Autopilot--System-blue)](https://github.com/Coding-Autopilot-System)

Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover) | [autogen](https://github.com/Coding-Autopilot-System/autogen)

**See also:** [OgeonX-Ai/kim-ai-voice-demo](https://github.com/OgeonX-Ai/kim-ai-voice-demo) — AI voice engineering platform

## Architecture

```mermaid
flowchart LR
  GH_Pages[GitHub Pages\nindex.html] -->|browser render| CV[Online CV\nHTML / CSS / JS]
  CV -->|print button| PDF[PDF Export\nbrowser print dialog]
  OgeonX_Ecosystem[OgeonX-Ai Ecosystem\nkim-ai-voice-demo / enterprise-ai-gateway] -->|AI toolchain| CV
  CI[GitHub Actions CI\nHTML validation] -->|green badge| GH_Pages
```

[Architecture prose]

## Skills Covered

[bullets from index.html skills section — AI & Automation section leads]

## View Online

[link to https://ogeonx-ai.github.io/My-CV/]
```

**Hero line** (10-RESEARCH.md line 136, ASSUMED — executor validates tone):
"Kim Harjamaki's online CV — an AI-augmented career portfolio maintained via the OgeonX-Ai automation ecosystem, covering 20+ years in Azure architecture, DevOps, and applied AI engineering."

**Reframe direction** (10-RESEARCH.md lines 137-139): Explain WHAT the CV is + HOW it's maintained (AI toolchain, automated via GitHub Actions, linked to OgeonX-Ai portfolio). Position it as evidence of AI-powered workflow, not just a static HTML page. Cross-link to kim-ai-voice-demo.

**LICENSE note** (10-RESEARCH.md lines 355-361 and Pitfall 6): My-CV has no LICENSE file. If MIT badge is included in README, a LICENSE file MUST be created first. Options:
1. Create MIT LICENSE via separate step before README write (recommended for portfolio consistency)
2. Skip MIT badge and use only CI + HTML badges

**Topics to set** (10-RESEARCH.md lines 524-525 — 7 topics):
`cv`, `resume`, `portfolio`, `azure`, `devops`, `github-pages`, `html`

**Topics API call:**
```
mcp__github__replace_all_topics
owner: "OgeonX-Ai"
repo: "My-CV"
names: ["cv", "resume", "portfolio", "azure", "devops", "github-pages", "html"]
```

**Commit message pattern:**
```
docs: Level A README — hero line, CI badge, architecture diagram, CAS ecosystem link, kim-ai-voice-demo cross-link
```

---

### `OgeonX-Ai/My-CV/LICENSE` (config, new file)

**Analog:** MIT LICENSE pattern used in all CAS and OgeonX-Ai repos (kim-ai-voice-demo, enterprise-ai-gateway, android all have MIT LICENSE).

**New file pattern** (omit sha — file does not exist):
```
mcp__github__create_or_update_file
owner: "OgeonX-Ai"
repo: "My-CV"
path: "LICENSE"
branch: "main"
message: "chore: add MIT LICENSE"
content: [base64-encoded MIT LICENSE text with year 2024 or current year, name: Kim Harjamaki]
sha: [OMIT — new file]
```

**MIT LICENSE content template:**
```
MIT License

Copyright (c) 2024 Kim Harjamaki

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

**Prerequisite order:** LICENSE must be created BEFORE the README write (so the MIT badge link `(LICENSE)` resolves).

---

### `kim-ai-voice-demo.wiki.git/` — 4 wiki pages (documentation, file-I/O)

**Analog:** `09-02-SUMMARY.md` Task 2 — android wiki push (lines 73-83). Match quality: exact.

**Wiki clone + push delivery pattern** (09-02-SUMMARY.md lines 99-103, Phase 9 confirmed path):

```bash
TOKEN=$(gh auth token)
rm -rf /tmp/kim-ai-voice-demo-wiki
git clone "https://x-access-token:${TOKEN}@github.com/OgeonX-Ai/kim-ai-voice-demo.wiki.git" /tmp/kim-ai-voice-demo-wiki
```

Write 4 pages using Write tool to `C:/Users/KIMHAR~1/AppData/Local/Temp/kim-ai-voice-demo-wiki/`:
- `C:/Users/KIMHAR~1/AppData/Local/Temp/kim-ai-voice-demo-wiki/Home.md`
- `C:/Users/KIMHAR~1/AppData/Local/Temp/kim-ai-voice-demo-wiki/Setup-Guide.md`
- `C:/Users/KIMHAR~1/AppData/Local/Temp/kim-ai-voice-demo-wiki/Architecture.md`
- `C:/Users/KIMHAR~1/AppData/Local/Temp/kim-ai-voice-demo-wiki/Configuration-Reference.md`

```bash
git -C /tmp/kim-ai-voice-demo-wiki add Home.md Setup-Guide.md Architecture.md Configuration-Reference.md
git -C /tmp/kim-ai-voice-demo-wiki -c user.email="bot@gsd" -c user.name="GSD Bot" commit -m "docs: add kim-ai-voice-demo wiki pages — Home, Setup Guide, Architecture, Configuration Reference"
git -C /tmp/kim-ai-voice-demo-wiki push origin master
```

**CRITICAL: wiki.git ALWAYS uses `master` branch** — even though kim-ai-voice-demo source repo uses `main`. [09-PATTERNS.md Shared Patterns line 500]

**Home.md structure** (09-PATTERNS.md lines 135-161, adapted):
```markdown
# kim-ai-voice-demo

[![CI](https://github.com/OgeonX-Ai/kim-ai-voice-demo/actions/workflows/ci.yml/badge.svg?branch=main)](...)
[![JavaScript](https://img.shields.io/badge/javascript-ES2022-yellow)](...)
[![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[hero paragraph — same as README]

## Quick Start

```bash
git clone https://github.com/OgeonX-Ai/kim-ai-voice-demo.git
cd kim-ai-voice-demo/enterprise-ai-gateway
npm install
node server.js
# Open https://ogeonx-ai.github.io/kim-ai-voice-demo/ in browser
```

## Documentation

| Page | Description |
|------|-------------|
| [Setup Guide](Setup-Guide) | Prerequisites, installation, and first voice interaction |
| [Architecture](Architecture) | AI voice pipeline and component design |
| [Configuration Reference](Configuration-Reference) | Environment variables, API key handling |
```

**Setup-Guide.md structure** (09-PATTERNS.md lines 173-193, adapted):
```
## Prerequisites
## Installation
## Configuration
## Running the Backend
## What a Successful Setup Looks Like
```
"What a Successful Setup Looks Like" is REQUIRED — include: backend starts on port 3001, ElevenLabs proxy responds, GitHub Pages frontend loads in browser. [05-PATTERNS.md pattern — mandatory section]

**Architecture.md structure** (09-PATTERNS.md lines 200-211):
- Intro sentence
- `## Pipeline Diagram` — IDENTICAL `flowchart LR` Mermaid from README (do NOT create a new diagram)
- `## Components` — one sub-section per component (GitHub Pages frontend, Whisper STT playground, Voice-to-ServiceNow, Node.js/Express backend, KB templates, Dev log pipeline)

**Configuration-Reference.md structure** (09-PATTERNS.md lines 230-237, from 10-RESEARCH.md lines 498-514):
```markdown
## Environment Variables

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `PORT` | number | no | `3001` | Express server port |
| ElevenLabs API key | string | per-request | — | Passed in request body; never stored; used in-memory only |
```

---

### `My-CV.wiki.git/` — 4 wiki pages (documentation, file-I/O)

**Analog:** `09-02-SUMMARY.md` Task 2 — android wiki push (lines 73-83). Match quality: exact.

**Wiki clone + push delivery pattern** (identical procedure, different repo name):

```bash
TOKEN=$(gh auth token)
rm -rf /tmp/My-CV-wiki
git clone "https://x-access-token:${TOKEN}@github.com/OgeonX-Ai/My-CV.wiki.git" /tmp/My-CV-wiki
```

Write 4 pages using Write tool to `C:/Users/KIMHAR~1/AppData/Local/Temp/My-CV-wiki/`:
- `C:/Users/KIMHAR~1/AppData/Local/Temp/My-CV-wiki/Home.md`
- `C:/Users/KIMHAR~1/AppData/Local/Temp/My-CV-wiki/Setup-Guide.md`
- `C:/Users/KIMHAR~1/AppData/Local/Temp/My-CV-wiki/Architecture.md`
- `C:/Users/KIMHAR~1/AppData/Local/Temp/My-CV-wiki/Configuration-Reference.md`

```bash
git -C /tmp/My-CV-wiki add Home.md Setup-Guide.md Architecture.md Configuration-Reference.md
git -C /tmp/My-CV-wiki -c user.email="bot@gsd" -c user.name="GSD Bot" commit -m "docs: add My-CV wiki pages — Home, Setup Guide, Architecture, Configuration Reference"
git -C /tmp/My-CV-wiki push origin master
```

**CRITICAL: wiki.git always uses `master` branch.**

**Home.md structure** (adapted for pure HTML/CSS/JS CV — no backend):
```markdown
# My-CV

[![CI](https://github.com/OgeonX-Ai/My-CV/actions/workflows/ci.yml/badge.svg?branch=main)](...)
[![HTML](https://img.shields.io/badge/html5-portfolio-orange)](https://ogeonx-ai.github.io/My-CV/)

[hero paragraph — same as README]

## View the CV

[https://ogeonx-ai.github.io/My-CV/](https://ogeonx-ai.github.io/My-CV/)

## Documentation

| Page | Description |
|------|-------------|
| [Setup Guide](Setup-Guide) | How to view, fork, and customise the CV |
| [Architecture](Architecture) | Structure and AI toolchain context |
| [Configuration Reference](Configuration-Reference) | Browser features and print settings |
```

**Setup-Guide.md structure** (adapted — no server, no npm):
```
## Prerequisites
## Viewing the CV Online
## Running Locally
## Customisation
## What a Successful Setup Looks Like
```
"What a Successful Setup Looks Like" is REQUIRED — include: CV loads in browser, skills modal expands, print button triggers browser print dialog.

**Architecture.md structure** (identical 3-section pattern, adapted):
- Intro sentence explaining static site + AI toolchain context
- `## Architecture Diagram` — IDENTICAL `flowchart LR` Mermaid from README
- `## Components` — `index.html` (CV structure), `style.css` (print-ready layout), `script.js` (skills modal), GitHub Pages (hosting), GitHub Actions CI (HTML validation), OgeonX-Ai ecosystem context

**Configuration-Reference.md structure** (from 10-RESEARCH.md lines 506-514):
```markdown
## Browser Features

| Name | Type | Description |
|------|------|-------------|
| Browser print | built-in | "Download / Print PDF" button triggers `window.print()` |
| Skills modal | JS `<details>` | Accordion expand for skills categories |
```
(No server-side configuration — pure static HTML.)

---

### Wave 0: wiki initialization checkpoints (both repos)

**Analog:** `09-02-SUMMARY.md` Task 0 — android wiki initialization (lines 38-43). Match quality: exact.

**Pre-condition check** (must run FIRST, before any wiki push attempt):
```bash
git ls-remote https://github.com/OgeonX-Ai/kim-ai-voice-demo.wiki.git HEAD
git ls-remote https://github.com/OgeonX-Ai/My-CV.wiki.git HEAD
# If either returns "Repository not found" — human action required
# If either returns [SHA]\tHEAD — wiki.git is provisioned and ready
```

**Human action required** (09-02-SUMMARY.md lines 38-43 pattern, adapted for both repos):
```
For kim-ai-voice-demo:
1. Open https://github.com/OgeonX-Ai/kim-ai-voice-demo/wiki in browser
2. Click "Create the first page" (green button)
3. Leave title as "Home", add stub text (e.g., "kim-ai-voice-demo wiki")
4. Click "Save Page"

For My-CV:
1. Open https://github.com/OgeonX-Ai/My-CV/wiki in browser
2. Click "Create the first page" (green button)
3. Leave title as "Home", add stub text (e.g., "My-CV wiki")
4. Click "Save Page"
```

**Acceptance verification** (run after human action):
```bash
git ls-remote https://github.com/OgeonX-Ai/kim-ai-voice-demo.wiki.git HEAD
# Expected: [0-9a-f]{40}\tHEAD
git ls-remote https://github.com/OgeonX-Ai/My-CV.wiki.git HEAD
# Expected: [0-9a-f]{40}\tHEAD
```

**Why required:** Both repos return "Repository not found" for wiki.git (verified by git ls-remote in research). `has_wiki: true` in the GitHub API only means the wiki FEATURE is enabled — it does NOT mean the wiki.git remote is provisioned. [10-RESEARCH.md Pitfall 1, 09-01-SUMMARY.md lines 65-66]

---

## Shared Patterns

### Wiki Delivery (git clone + push to wiki.git)
**Source:** `09-02-SUMMARY.md` lines 97-103 (Phase 9 confirmed — Windows path sync deviation)
**Apply to:** All 8 wiki pages (4 per repo)

```bash
# Confirmed working pattern from Phase 9 android wiki push:
TOKEN=$(gh auth token)
rm -rf /tmp/<REPO>-wiki
git clone "https://x-access-token:${TOKEN}@github.com/OgeonX-Ai/<REPO>.wiki.git" /tmp/<REPO>-wiki

# Write tool writes to C:/Users/KIMHAR~1/AppData/Local/Temp/<REPO>-wiki/
# Bash git commands use /tmp/<REPO>-wiki/
# These are the SAME directory — Windows short path / /tmp symlink
# No cp needed — confirmed in Phase 9 execution

git -C /tmp/<REPO>-wiki add Home.md Setup-Guide.md Architecture.md Configuration-Reference.md
git -C /tmp/<REPO>-wiki -c user.email="bot@gsd" -c user.name="GSD Bot" commit -m "docs: add <REPO> wiki pages"
git -C /tmp/<REPO>-wiki push origin master

# Non-fast-forward recovery if needed:
git -C /tmp/<REPO>-wiki pull origin master --rebase
git -C /tmp/<REPO>-wiki push origin master
```

**Inline execution constraint:** All git/wiki operations MUST be executed inline by the orchestrator. Worktree agents lack Bash access (confirmed Phase 9 — 09-02-SUMMARY.md lines 87-95). Do NOT spawn executor subagents for wiki tasks.

### GitHub MCP File Update (SHA-safe pattern)
**Source:** `09-PATTERNS.md` Shared Patterns — GitHub MCP File Update (lines 507-518)
**Apply to:** Both README.md rewrites (both are existing files on remote)

```
1. mcp__github__get_file_contents → capture live `sha` field
2. Compose full new content
3. mcp__github__create_or_update_file:
   - sha: [captured] — MANDATORY for existing files; omitting causes 409 Conflict
   - branch: "main"  (both repos use main)
   - For NEW files (ci.yml, LICENSE): omit sha entirely
```

**Both repos use `main` branch** (unlike Phase 9 android which used `master`). [10-RESEARCH.md lines 59-60, 74-75]

### Enterprise Tone Constraint
**Source:** `.planning/PROJECT.md` Constraints section (referenced in 09-PATTERNS.md lines 520-528)
**Apply to:** All deliverables (README files, wiki pages, commit messages)

- No emoji anywhere
- No toy/demo language ("simple", "easy", "just", "demo")
- Precise, technical language; assume tech lead / hiring manager audience
- Feature list ordered for credibility (AI capabilities lead, implementation details follow)
- D-CF-01: "No emoji in README or wiki"

### CAS Ecosystem Badge + Line Pattern
**Source:** `09-PATTERNS.md` Shared Patterns lines 530-538 (unchanged from Phases 4-9)
**Apply to:** Both README files

```markdown
[![Coding-Autopilot-System](https://img.shields.io/badge/ecosystem-Coding--Autopilot--System-blue)](https://github.com/Coding-Autopilot-System)

Part of the Coding-Autopilot-System ecosystem: [gsd-orchestrator](https://github.com/Coding-Autopilot-System/gsd-orchestrator) | [Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover) | [autogen](https://github.com/Coding-Autopilot-System/autogen)
```

### Mermaid Diagram Pattern
**Source:** `09-PATTERNS.md` Shared Patterns lines 540-551 (from Phase 4 D-03, D-CF-02)
**Apply to:** Both README files and both Architecture.md wiki pages

- Always `flowchart LR` — never `graph LR`
- Never connect `-->` directly into a subgraph declaration — connect to nodes inside
- Architecture wiki page MUST use IDENTICAL diagram from README (no new diagram variant)
- Diagrams already specified in 10-RESEARCH.md lines 229-248

### Verification Pattern (per deliverable)
**Source:** `09-PATTERNS.md` Shared Patterns lines 553-564
**Apply to:** After each README write, after each CI workflow creation, after wiki push

```bash
# README verification:
gh api repos/OgeonX-Ai/<REPO>/contents/README.md --jq '.content' | base64 -d | grep -E "CI|flowchart|Coding-Autopilot-System"

# CI workflow verification (check run result after push triggers it):
gh run list --repo OgeonX-Ai/<REPO> --workflow ci.yml --limit 1 --json conclusion --jq '.[0].conclusion'
# Expected: "success"

# Wiki push verification:
git ls-remote https://github.com/OgeonX-Ai/<REPO>.wiki.git
# Expected: HEAD ref with commit SHA

# Topics verification:
gh api repos/OgeonX-Ai/<REPO> --jq '.topics'
```

### GITHUB_MCP_PAT Requirement
**Source:** `10-RESEARCH.md` lines 349-354 (Pitfall 5); Phase 7 D-09, Phase 8 incident
**Apply to:** Both `.github/workflows/ci.yml` write operations

GitHub API returns 403/404 when writing `.github/workflows/` files using a repo-scoped PAT. The `workflow` OAuth scope is required. Use `GITHUB_MCP_PAT` for both CI workflow file writes. For README, LICENSE, and wiki files, a repo-scoped PAT is sufficient.

---

## Critical Differences Table (Phase 10)

| Property | kim-ai-voice-demo | My-CV |
|----------|-------------------|-------|
| Default branch | `main` | `main` |
| CI badge `?branch=` | `?branch=main` | `?branch=main` |
| `create_or_update_file` branch | `"main"` | `"main"` |
| CI workflow exists | NO — create `ci.yml` | NO — create `ci.yml` |
| CI approach | `npm install` + `node --check server.js` in `enterprise-ai-gateway/` | `node -e` HTML structural check — NO npm |
| LICENSE | MIT (exists) | None — create MIT LICENSE |
| Wiki state | NOT initialized | NOT initialized |
| Wiki temp dir | `/tmp/kim-ai-voice-demo-wiki` | `/tmp/My-CV-wiki` |
| Write tool path | `C:/Users/KIMHAR~1/AppData/Local/Temp/kim-ai-voice-demo-wiki/` | `C:/Users/KIMHAR~1/AppData/Local/Temp/My-CV-wiki/` |
| Language badge | JavaScript ES2022 + Node.js 20 (yellow/green) | HTML5 (orange) |
| MIT badge in README | Yes — `(LICENSE)` link resolves | Yes — only after LICENSE file created |
| Hero framing | AI voice engineering platform | AI-augmented career portfolio |
| Cross-links (See also) | My-CV + enterprise-ai-gateway | kim-ai-voice-demo only |
| Topics (current) | none — set 8 | none — set 7 |
| Has package.json | Yes (`enterprise-ai-gateway/package.json`) | NO — no package.json |
| Existing `.github/` dir | Yes (3 workflows — do NOT touch) | NO — first file creates the directory |

---

## Phase 10 Execution Sequence

For planner: the correct execution order within each plan is:

**Plan 10-00 (Wave 0 — both repos):**
1. `git ls-remote` check for both wikis (confirm both NOT initialized)
2. Human action: initialize both wikis via browser
3. Verify both `git ls-remote` return SHA

**Plan 10-01 (kim-ai-voice-demo):**
1. Check for `enterprise-ai-gateway/package-lock.json` (determines `npm ci` vs `npm install` in ci.yml)
2. Create `.github/workflows/ci.yml` (GITHUB_MCP_PAT required)
3. Fetch README.md sha → write full README rewrite (sha required)
4. Set topics via `replace_all_topics`
5. Verify CI run passes (`gh run list --workflow ci.yml`)
6. Clone wiki, write 4 pages (Write tool to KIMHAR~1 path), commit, push to master
7. Verify wiki push (git ls-remote returns updated SHA)

**Plan 10-02 (My-CV):**
1. Create `LICENSE` (new file — no sha)
2. Create `.github/workflows/ci.yml` (GITHUB_MCP_PAT required, no `.github/` dir exists)
3. Fetch README.md sha → write full README rewrite (sha required)
4. Set topics via `replace_all_topics`
5. Verify CI run passes
6. Clone wiki, write 4 pages (Write tool to KIMHAR~1 path), commit, push to master
7. Verify wiki push

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `My-CV/.github/workflows/ci.yml` (HTML `node -e` check) | CI workflow | request-response | No prior phase has a pure HTML static site CI. The `node -e` inline structural check is new to Phase 10. Closest analog is the kim-ai-voice-demo CI in this same phase. |

All other deliverables have direct analogs from Phases 5, 9, or within Phase 10 itself.

---

## Metadata

**Analog search scope:**
- `.planning/phases/09-ogeonx-ai-core-tech-ai-reframe/` — 09-PATTERNS.md, 09-01-SUMMARY.md, 09-02-SUMMARY.md
- `.planning/phases/10-ogeonx-ai-portfolio-repos-ai-reframe/` — 10-RESEARCH.md

**Files scanned:** 4 upstream files (all required reading loaded)
**Pattern extraction date:** 2026-05-28
