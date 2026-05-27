---
phase: 08-cas-secondary-repos-level-a
verified: 2026-05-27T12:00:00Z
status: gaps_found
score: 17/18 must-haves verified
overrides_applied: 0
gaps:
  - truth: "cloud-security-service-model README has enterprise hero line, CI badge, and cross-links to org"
    status: partial
    reason: "CI badge present in README but Phase 8 README rewrite directly caused a NEW CI failure. The markdown linter (MD013) enforces 80-char line length on README.md; the new hero lines exceed this. The most recent CI run (SHA f92f00406d5, 2026-05-27) failed on the README committed by Plan 08-03. Pre-Phase-8 runs (2026-01-03) were already failing but on different content and different file errors. The badge now shows red, worsening visibility vs. the pre-Phase-8 state (no badge). ROADMAP success criteria say cloud-security-service-model README must 'explain the framework/methodology clearly' (VERIFIED), but a red CI badge is a portfolio-visible regression."
    artifacts:
      - path: "README.md (remote: Coding-Autopilot-System/cloud-security-service-model)"
        issue: "18 lines exceed 80-char markdown-lint limit introduced by Phase 8 rewrite; ci.yml markdown-lint step now fails on README.md content added in this phase"
    missing:
      - "Fix README.md line lengths to comply with markdown-lint MD013 (wrap long lines at 80 chars or configure markdownlint to allow longer lines)"
      - "Alternatively: add or update .markdownlint.json to relax MD013 line-length rule in this repo"
---

# Phase 8: CAS Secondary Repos Level A Verification Report

**Phase Goal:** autopilot-core, autopilot-demo, and cloud-security-service-model reach Level A documentation.
**Verified:** 2026-05-27T12:00:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Repo / Truth | Status | Evidence |
|---|-------------|--------|----------|
| 1 | autopilot-core: MIT LICENSE on main | VERIFIED | `gh api .../license --jq '.license.spdx_id'` → `MIT`; commit d344a71 |
| 2 | autopilot-core: ci.yml on ubuntu-latest, passes on main | VERIFIED | `gh run list --workflow ci.yml ... conclusion` → `success`; workflow named "CI", runs-on: ubuntu-latest confirmed |
| 3 | autopilot-core README has hero line and CI badge | VERIFIED | README contains "Org-level AI autopilot operator" + `ci.yml/badge.svg?branch=main` + `[![License: MIT]` |
| 4 | autopilot-core has 5+ GitHub topics set | VERIFIED | 9 topics: `["autonomous-agents","ci-automation","codex","devops","github-actions","github-org","operator","powershell","workflow-automation"]` |
| 5 | autopilot-core wiki has exactly 4 pages: Home, Setup-Guide, Architecture, Configuration-Reference | VERIFIED | Git clone confirms: `Architecture.md`, `Configuration-Reference.md`, `Home.md`, `Setup-Guide.md` — commit 3e781f2 |
| 6 | autopilot-core README links to Coding-Autopilot-System org and sibling repos | VERIFIED | README contains `[Coding-Autopilot-System]` org link + sibling links to `ci-autopilot` and `autopilot-demo` |
| 7 | autopilot-demo: MIT LICENSE on main | VERIFIED | `gh api .../license --jq '.license.spdx_id'` → `MIT`; commit 0705089 |
| 8 | autopilot-demo: ci.yml on ubuntu-latest, passes on main, named CI | VERIFIED | Conclusion: `success`; `name: CI`, `runs-on: ubuntu-latest` confirmed; `demo-ci.yml` still named "Demo CI" — unchanged |
| 9 | autopilot-demo README has hero line and CI badge | VERIFIED | README contains "Demo target for the Coding-Autopilot-System AI repair pipeline" + `ci.yml/badge.svg?branch=main` + `[![License: MIT]` |
| 10 | autopilot-demo has 5+ GitHub topics set | VERIFIED | 8 topics: `["autonomous-agents","ci-automation","codex","demo","devops","github-actions","powershell","workflow-automation"]` |
| 11 | autopilot-demo wiki has exactly 4 pages: Home, Setup-Guide, Architecture, Configuration-Reference | VERIFIED | Git clone confirms: `Architecture.md`, `Configuration-Reference.md`, `Home.md`, `Setup-Guide.md` — commit 1c8edfa |
| 12 | autopilot-demo README links to Coding-Autopilot-System org and sibling repos | VERIFIED | README contains `[Coding-Autopilot-System]` org link + `[autopilot-core]` cross-link |
| 13 | cloud-security-service-model README has enterprise hero line, CI badge, and cross-links to org | PARTIAL | Hero line present: "Enterprise cloud security operating model"; CI badge present: `ci.yml/badge.svg`; org cross-link present. BUT: Phase 8 README rewrite introduced 18 lines exceeding markdown-lint 80-char limit — most recent CI run (2026-05-27, SHA f92f004) fails on README.md content added by this phase. Badge now shows RED. |
| 14 | cloud-security-service-model has 8+ GitHub topics set | VERIFIED | 10 topics: `["azure","azure-security","cissp","cloud-security","devsecops","enterprise-security","hybrid-cloud","iso27001","operating-model","security-operations"]` |
| 15 | cloud-security-service-model wiki has exactly 4 pages: Home, Service-Definition-and-Operating-Model, Architecture-and-Reference, Metrics-and-Compliance | VERIFIED | Git clone confirms: `Architecture-and-Reference.md`, `Home.md`, `Metrics-and-Compliance.md`, `Service-Definition-and-Operating-Model.md` — commit 808e73a |
| 16 | cloud-security-service-model repo description updated to enterprise framing | VERIFIED | `gh api ... --jq '.description'` → `"Enterprise cloud security operating model for Azure and hybrid environments"` |
| 17 | autopilot-core README contains Mermaid diagram | VERIFIED | `mermaid` code block found in README (flowchart LR) |
| 18 | autopilot-demo README contains Mermaid diagram | VERIFIED | `mermaid` code block found in README (flowchart LR) |

**Score:** 17/18 truths verified (1 partial / blocker gap)

---

### Required Artifacts

| Artifact | Provided By | Status | Details |
|----------|------------|--------|---------|
| `LICENSE` (autopilot-core remote) | Plan 08-01 | VERIFIED | MIT, commit d344a71, confirmed via GitHub license API |
| `.github/workflows/ci.yml` (autopilot-core remote) | Plan 08-01 | VERIFIED | `name: CI`, `ubuntu-latest`, CI passes; no self-hosted dependency |
| `README.md` (autopilot-core remote) | Plan 08-01 | VERIFIED | Hero line, Mermaid, CI badge, License badge, org+sibling cross-links; commit a9ff4be |
| `autopilot-core.wiki.git` 4 pages | Plan 08-01 | VERIFIED | Home, Setup-Guide, Architecture, Configuration-Reference; commit 3e781f2; no placeholder content |
| `LICENSE` (autopilot-demo remote) | Plan 08-02 | VERIFIED | MIT, commit 0705089, confirmed via GitHub license API |
| `.github/workflows/ci.yml` (autopilot-demo remote) | Plan 08-02 | VERIFIED | `name: CI` (not "Demo CI"), `ubuntu-latest`, CI passes; demo-ci.yml unchanged |
| `README.md` (autopilot-demo remote) | Plan 08-02 | VERIFIED | Hero line, Mermaid, CI badge, License badge, org+sibling cross-links; commit fee614a |
| `autopilot-demo.wiki.git` 4 pages | Plan 08-02 | VERIFIED | Home, Setup-Guide, Architecture, Configuration-Reference; commit 1c8edfa; no placeholder content |
| `README.md` (cloud-security-service-model remote) | Plan 08-03 | PARTIAL | Hero line, CI badge, org cross-link present; but README rewrite triggered new CI failure (markdown-lint MD013 on 18 long lines). Commit f92f004. |
| `cloud-security-service-model.wiki.git` 4 pages | Plan 08-03 | VERIFIED | Home, Service-Definition-and-Operating-Model, Architecture-and-Reference, Metrics-and-Compliance; commit 808e73a; substantive content (KPI tables, architecture diagrams, service scope text) — no placeholder content |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `ci.yml push` (autopilot-core) | Green CI badge in README | GitHub Actions on main | WIRED | Badge URL `ci.yml/badge.svg?branch=main` confirmed in README; latest run: `success` |
| `ci.yml push` (autopilot-demo) | Green CI badge in README (named CI) | GitHub Actions on main | WIRED | Badge URL `ci.yml/badge.svg?branch=main` confirmed; `name: CI`; latest run: `success`; `demo-ci.yml` unchanged ("Demo CI") |
| `existing ci.yml` (cloud-security-service-model) | CI badge in README | `ci.yml/badge.svg?branch=main` | PARTIAL | Badge URL present in README; however the badge resolves to RED because Phase 8 README rewrite added lines that break the markdown-lint step. Pre-existing failure (Jan 2026) on other content; Phase 8 introduced new lint errors in README.md. |

---

### Data-Flow Trace (Level 4)

Not applicable — this phase produces documentation and CI configuration on remote repositories, not runnable application code with dynamic data rendering.

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| autopilot-core: MIT license detected | `gh api .../autopilot-core/license --jq '.license.spdx_id'` | `MIT` | PASS |
| autopilot-core: CI passes | `gh run list --workflow ci.yml --limit 1 --json conclusion` | `success` | PASS |
| autopilot-core: 9 topics set | `gh api .../autopilot-core --jq '.topics \| length'` | `9` | PASS |
| autopilot-core: wiki HEAD ref exists | `git ls-remote autopilot-core.wiki.git` | `3e781f2c...` | PASS |
| autopilot-core: 4 wiki pages, no placeholders | `ls` + `grep -i placeholder` | 4 files, 0 matches | PASS |
| autopilot-demo: MIT license detected | `gh api .../autopilot-demo/license --jq '.license.spdx_id'` | `MIT` | PASS |
| autopilot-demo: CI passes (named CI) | `gh run list --workflow ci.yml --limit 1 --json conclusion` | `success` | PASS |
| autopilot-demo: demo-ci.yml unchanged | `base64 -d \| grep "^name:"` | `name: Demo CI` | PASS |
| autopilot-demo: 8 topics set | `gh api .../autopilot-demo --jq '.topics \| length'` | `8` | PASS |
| autopilot-demo: wiki HEAD ref exists | `git ls-remote autopilot-demo.wiki.git` | `1c8edfa...` | PASS |
| cloud-security-service-model: description updated | `gh api ... --jq '.description'` | `"Enterprise cloud security operating model..."` | PASS |
| cloud-security-service-model: 10 topics set | `gh api ... --jq '.topics \| length'` | `10` | PASS |
| cloud-security-service-model: CI badge in README | `base64 -d \| grep ci.yml/badge.svg` | Badge line found | PASS |
| cloud-security-service-model: CI passes | `gh run list --workflow ci.yml --limit 1 --json conclusion` | `failure` | FAIL — Phase 8 README introduced 18 long lines that break markdown-lint MD013 |
| cloud-security-service-model: wiki HEAD ref exists | `git ls-remote csm.wiki.git` | `808e73aa...` | PASS |
| cloud-security-service-model: 4 wiki pages, no placeholders | `ls` + `grep -i placeholder` | 4 files, 0 matches | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| ACOR-01 | 08-01-PLAN.md | autopilot-core Level A docs — README rewrite, CI badge, wiki 4 pages, topics, cross-links | SATISFIED | MIT license, CI green, 9 topics, 4 wiki pages, enterprise README — all confirmed via live API |
| ACOR-02 | 08-02-PLAN.md | autopilot-demo Level A docs — README rewrite, CI badge, wiki 4 pages, topics, cross-links | SATISFIED | MIT license, CI green (named CI), 8 topics, 4 wiki pages, enterprise README, demo-ci.yml unchanged — all confirmed via live API |
| CSEC-01 | 08-03-PLAN.md | cloud-security-service-model documentation — README rewrite (framework/methodology framing), wiki 4 pages, topics | PARTIALLY SATISFIED | Framework/methodology framing verified; 10 topics set; 4 wiki pages with substantive content; repo description updated. CI badge present but shows RED because Phase 8 README introduced markdown-lint violations. |

**Orphaned requirements check:** REQUIREMENTS.md maps only ACOR-01, ACOR-02, and CSEC-01 to Phase 8. All three are claimed by plans. No orphaned requirements.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `README.md` (cloud-security-service-model, remote) | Lines 12, 13, 25, 26, 42, 43, 64, 79, 82, 88 (and others) | 18 lines exceed 80-char markdown-lint MD013 limit | BLOCKER | Triggered new CI failure (2026-05-27) on Phase 8 README commit. CI badge now shows red in the portfolio-visible README. Portfolio visibility is the stated goal of this milestone. |

---

### Human Verification Required

None. All critical verifications were completed programmatically via live GitHub API calls and wiki clone inspection.

---

## Gaps Summary

**1 gap found — blocking full CSEC-01 satisfaction.**

**Root cause:** Plan 08-03 rewrote `cloud-security-service-model/README.md` with long lines (up to 153 chars on some lines) without accounting for the repo's existing markdown-lint CI workflow that enforces a strict 80-char line length limit (`MD013`). The Phase 8 README commit (SHA `f92f00406d5`) is the `headSha` of the most recent CI failure run (2026-05-27T10:27:12Z). The SUMMARY.md claimed this was a "pre-existing failure" — this is partially true (CI was already failing on other content since January 2026) but the most recent failure is specifically triggered by Phase 8 content.

**Impact:** The CI badge in the portfolio-visible README now displays red (`failure`), which is worse than no badge. The stated milestone goal is portfolio visibility for hiring managers. A red CI badge is a portfolio regression.

**Fix options (for gap closure plan):**
1. Wrap long lines in `README.md` to comply with the 80-char MD013 rule (preferred — keeps CI green).
2. Add a `.markdownlint.json` to the repo root that sets `"MD013": false` or increases the line-length limit (faster but disables the rule org-wide).
3. Update `ci.yml` to exclude `README.md` from the markdown-lint scope (least preferred — reduces CI value).

**ACOR-01 and ACOR-02 are fully satisfied.** autopilot-core and autopilot-demo achieved all Level A documentation requirements with green CI, correct wiki pages, enterprise READMEs, and proper topics. The ROADMAP success criteria "autopilot-core and autopilot-demo have passing CI badge" is met.

---

_Verified: 2026-05-27T12:00:00Z_
_Verifier: Claude (gsd-verifier)_
