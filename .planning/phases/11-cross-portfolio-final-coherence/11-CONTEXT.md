---
phase: 11-cross-portfolio-final-coherence
phase_number: 11
generated: "2026-05-28"
mode: auto
---

# Phase 11 Context — Cross-Portfolio Final Coherence

## Domain

Topics audit, org pinned repos, and issue templates across Coding-Autopilot-System and OgeonX-Ai. This phase closes the final coherence gaps after all repos reached Level A in Phases 7-10.

## Decisions

### Topics Audit (COHER-01)

- **Scope:** All repos in both orgs — CAS (gsd-orchestrator, Promptimprover, autogen, ci-autopilot, autopilot-core, autopilot-demo, cloud-security-service-model) and OgeonX-Ai (enterprise-ai-gateway, android, kim-ai-voice-demo, My-CV)
- **Approach:** Verify current topics via `gh api repos/{org}/{repo} --jq '.topics'`; patch only repos missing topics or with fewer than 5 topics. Do NOT overwrite topics carefully set in prior phases (7-10) unless they are clearly wrong or insufficient.
- **Target:** 5-10 accurate, discoverable topics per repo
- **Rationale:** Prior phases already set topics on most repos; this is a verification + gap-fill pass, not a wholesale replacement

### Org Pinning (COHER-02)

- **Target org:** Coding-Autopilot-System
- **Repos to pin:** gsd-orchestrator, Promptimprover, autogen (the 3 flagship repos)
- **Mechanism:** GitHub GraphQL API via `gh api graphql` — pinning repos uses the `pinRepositories` mutation. The org must own the repos and the PAT must have `admin:org` scope.
- **Constraint note:** If GraphQL pinning fails due to PAT scope, document the limitation and provide manual instructions. Do NOT block plan on this — treat as best-effort.
- **ci-autopilot exclusion:** ci-autopilot must NOT appear in pinned repos (per Phase 1 decision D-04)

### Issue Templates (COHER-03)

- **Scope:** 3 flagship CAS repos only — gsd-orchestrator, Promptimprover, autogen
- **Templates to create:** `bug_report.md` and `feature_request.md` in `.github/ISSUE_TEMPLATE/`
- **Format:** Minimal standard template — name, about, title prefix, labels, body with sections (Describe the bug / Expected behavior / Steps to reproduce / Environment for bug; Feature request with Is your feature request related to a problem / Describe the solution / Alternatives for feature_request)
- **Tone:** Enterprise, no emoji, professional — consistent with existing repo documentation style
- **Creation method:** `mcp__github__create_or_update_file` (GITHUB_MCP_PAT not needed — issue templates don't require workflow scope)
- **No YAML front matter labels:** Keep labels blank (repos don't have label sets defined yet)

### Execution Constraints (carried from prior phases)

- All operations executed by Claude inline — no manual user commands
- Enterprise tone throughout — no emoji
- GitHub MCP + gh CLI for all remote operations
- GITHUB_MCP_PAT required only for `.github/workflows/` writes (not needed here)
- SHA-fetch-then-update mandatory for existing files

## Canonical Refs

- `.planning/ROADMAP.md` — phase goal and requirements
- `.planning/REQUIREMENTS.md` — COHER-01, COHER-02, COHER-03 definitions
- `.planning/phases/01-foundation-quick-wins/` — original topics decisions (Phase 1)
- `.planning/phases/10-ogeonx-ai-portfolio-repos-ai-reframe/10-PATTERNS.md` — execution patterns confirmed in prior phases

## Code Context

No local source files modified. All operations are remote GitHub API calls (topics, pinned repos, issue templates). Pattern established across phases 7-10: use `mcp__github__create_or_update_file` for file creation, `gh api` for metadata (topics, pinning).

## Deferred Ideas

- CONTRIBUTING.md and CODE_OF_CONDUCT.md — out of scope per PROJECT.md constraints
- Dependabot configuration — deferred
- GitHub Projects board — deferred
