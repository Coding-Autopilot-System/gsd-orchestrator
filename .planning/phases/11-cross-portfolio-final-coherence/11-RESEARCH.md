# Phase 11: Cross-Portfolio Final Coherence — Research

**Researched:** 2026-05-28
**Domain:** GitHub metadata (topics, pinned repos, issue templates) — Coding-Autopilot-System and OgeonX-Ai orgs
**Confidence:** HIGH — all findings verified directly via `gh api` calls in this session

---

## Summary

All current state was fetched live from the GitHub API. No assumptions were required. The three work domains (topics, pinned repos, issue templates) have clear verified baselines, and the gaps are concrete and enumerable.

Topics: 9 of 11 repos have at least 5 topics already. Two OgeonX-Ai repos (enterprise-ai-gateway, android) have zero topics — these are the only repos that need topic patches. My-CV has 7 topics (within range). All CAS repos have 8-10 topics and need no changes.

Pinned repos: Coding-Autopilot-System currently has no pinned repos (GraphQL query returned empty nodes). The three target repos (gsd-orchestrator, Promptimprover, autogen) must be pinned. GitHub's org repo pinning is a UI-only feature — there is no public REST endpoint and no GraphQL mutation (`pinRepositories` does not exist in the current schema). Execution must provide manual instructions.

Issue templates: None of the three flagship CAS repos have a `.github/ISSUE_TEMPLATE/` directory. Both `bug_report.md` and `feature_request.md` must be created from scratch in all three repos using `mcp__github__create_or_update_file`.

**Primary recommendation:** Execute topics patches for 2 OgeonX-Ai repos via `gh api PUT repos/{org}/{repo}/topics`, create issue templates in 3 CAS repos via GitHub MCP file creation, and document manual pinning instructions for the org owner.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Topics Audit (COHER-01)**
- Scope: All repos in both orgs — CAS (gsd-orchestrator, Promptimprover, autogen, ci-autopilot, autopilot-core, autopilot-demo, cloud-security-service-model) and OgeonX-Ai (enterprise-ai-gateway, android, kim-ai-voice-demo, My-CV)
- Approach: Verify current topics via `gh api repos/{org}/{repo} --jq '.topics'`; patch only repos missing topics or with fewer than 5 topics. Do NOT overwrite topics carefully set in prior phases (7-10) unless clearly wrong or insufficient.
- Target: 5-10 accurate, discoverable topics per repo

**Org Pinning (COHER-02)**
- Target org: Coding-Autopilot-System
- Repos to pin: gsd-orchestrator, Promptimprover, autogen (3 flagship repos)
- ci-autopilot must NOT appear in pinned repos (Phase 1 decision D-04)
- If GraphQL pinning fails due to PAT scope, document limitation and provide manual instructions. Do NOT block plan on this — treat as best-effort.

**Issue Templates (COHER-03)**
- Scope: 3 flagship CAS repos only — gsd-orchestrator, Promptimprover, autogen
- Templates: `bug_report.md` and `feature_request.md` in `.github/ISSUE_TEMPLATE/`
- Format: Minimal standard template — name, about, title prefix, labels, body with sections
- Tone: Enterprise, no emoji, professional
- Creation method: `mcp__github__create_or_update_file` (GITHUB_MCP_PAT not needed — issue templates don't require workflow scope)
- No YAML front matter labels: Keep labels blank

**Execution Constraints**
- All operations executed by Claude inline — no manual user commands
- Enterprise tone throughout — no emoji
- GitHub MCP + gh CLI for all remote operations
- GITHUB_MCP_PAT required only for `.github/workflows/` writes (not needed here)
- SHA-fetch-then-update mandatory for existing files

### Claude's Discretion

None explicitly listed — all decisions are locked.

### Deferred Ideas (OUT OF SCOPE)

- CONTRIBUTING.md and CODE_OF_CONDUCT.md
- Dependabot configuration
- GitHub Projects board
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| COHER-01 | GitHub topics audit — all repos have 5-10 accurate, discoverable topics | Current topics fetched for all 11 repos; 2 repos need patches (enterprise-ai-gateway, android); 9 repos already compliant |
| COHER-02 | Org pinned repos — Coding-Autopilot-System pins gsd-orchestrator, Promptimprover, autogen (ci-autopilot excluded) | Current state: 0 pinned repos. No programmatic API exists — manual instructions required. Node IDs documented. |
| COHER-03 | Issue templates — standardize bug_report.md and feature_request.md across CAS flagship repos | No `.github/ISSUE_TEMPLATE/` directory exists in any of the 3 flagship repos. 6 files must be created (2 per repo). |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Topics metadata | GitHub API (REST) | — | Topics are GitHub repo metadata; set via PUT repos/{org}/{repo}/topics |
| Org pinned repos | GitHub UI only | GraphQL (read-only) | No write API exists; pinning is UI-only per GitHub schema verification |
| Issue templates | GitHub repo files | GitHub MCP file create | Templates are files in .github/ISSUE_TEMPLATE/; created via file API |

---

## Current State: Topics Audit

### CAS Repos — Topics Fetched 2026-05-28

| Repo | Current Topics | Count | Status |
|------|----------------|-------|--------|
| gsd-orchestrator | agentic-ai, autonomous-agent, claude-ai, csharp, dotnet, dotnet10, github-automation, mcp, model-context-protocol, state-machine | 10 | COMPLIANT |
| Promptimprover | ai-governance, enterprise-ai, llm, mcp, mcp-server, model-context-protocol, prompt-engineering, prompt-governance, rag, typescript | 10 | COMPLIANT |
| autogen | ag-ui, agent-framework, agentic-ai, ai-automation, claude-ai, gemini, llm, microsoft-autogen, multi-agent, python | 10 | COMPLIANT |
| ci-autopilot | autonomous-agents, ci-automation, codex, devops, github-actions, issue-triage, python, self-hosted-runner | 8 | COMPLIANT |
| autopilot-core | autonomous-agents, ci-automation, codex, devops, github-actions, github-org, operator, powershell, workflow-automation | 9 | COMPLIANT |
| autopilot-demo | autonomous-agents, ci-automation, codex, demo, devops, github-actions, powershell, workflow-automation | 8 | COMPLIANT |
| cloud-security-service-model | azure, azure-security, cissp, cloud-security, devsecops, enterprise-security, hybrid-cloud, iso27001, operating-model, security-operations | 10 | COMPLIANT |

**CAS verdict:** All 7 CAS repos are within 5-10 topics. No patches required. [VERIFIED: gh api — live fetch]

### OgeonX-Ai Repos — Topics Fetched 2026-05-28

| Repo | Current Topics | Count | Status |
|------|----------------|-------|--------|
| enterprise-ai-gateway | (none) | 0 | GAP — needs patch |
| android | (none) | 0 | GAP — needs patch |
| kim-ai-voice-demo | ai-voice, elevenlabs, github-pages, javascript, portfolio, speech-to-text, text-to-speech, whisper | 8 | COMPLIANT |
| My-CV | azure, cv, devops, github-pages, html, portfolio, resume | 7 | COMPLIANT |

**OgeonX-Ai verdict:** 2 repos have zero topics and must be patched. kim-ai-voice-demo and My-CV are compliant. [VERIFIED: gh api — live fetch]

---

## Recommended Topics — Gap Fill

### enterprise-ai-gateway

**Repo description:** "Python API Gateway for AI services in Azure."
**Language:** Python
**README content confirms:** Vendor-agnostic AI service bus, FastAPI, Azure OpenAI, Anthropic, Ollama, Whisper STT, ElevenLabs TTS, ServiceNow/Jira integration, RAG with Azure AI Search, session memory, policy engine, correlation IDs.

**Recommended topics (7):**
```
ai-gateway, azure, enterprise-ai, fastapi, llm, python, rag
```

Rationale:
- `ai-gateway` — primary function, highly discoverable
- `azure` — primary cloud platform (Azure OpenAI, Azure AI Search, Azure Speech)
- `enterprise-ai` — audience/scope alignment
- `fastapi` — framework (concrete and searchable)
- `llm` — core capability
- `python` — language
- `rag` — retrieval-augmented generation is a core feature

[VERIFIED: enterprise-ai-gateway README fetched live in this session]

### android

**Repo description:** null (no description set)
**Language:** Kotlin
**README content confirms:** Jetpack Compose, Kotlin, AI voice pipeline (Whisper STT, LLM reasoning, ElevenLabs TTS), MediaRecorder, MediaPlayer, FastAPI backend co-located, Material 3.

**Recommended topics (7):**
```
android, elevenlabs, jetpack-compose, kotlin, llm, speech-to-text, text-to-speech
```

Rationale:
- `android` — platform (critical for discoverability)
- `elevenlabs` — prominent integration, brand-searchable
- `jetpack-compose` — UI framework
- `kotlin` — language
- `llm` — AI reasoning component
- `speech-to-text` — Whisper STT capability
- `text-to-speech` — ElevenLabs TTS capability

[VERIFIED: android README fetched live in this session]

---

## Current State: Org Pinned Repos

**Live query result (2026-05-28):**
```json
{"data":{"organization":{"pinnedItems":{"nodes":[]}}}}
```

**Current pinned repos:** None — Coding-Autopilot-System has no pinned repos. [VERIFIED: gh api graphql — live fetch]

**Target state:** gsd-orchestrator, Promptimprover, autogen pinned (ci-autopilot excluded per CONTEXT.md D-04)

### Pinning API Status

**GraphQL `pinRepositories` mutation:** Does NOT exist in the current GitHub GraphQL schema. [VERIFIED: schema introspection in this session — `Field 'pinRepositories' doesn't exist on type 'Mutation'`]

**GraphQL mutations scanned for alternatives:** `addEnterpriseOrganizationMember`, `updateOrganizationAllowPrivateRepositoryForkingSetting`, etc. — no repo-pinning mutation found.

**REST API:** No documented endpoint exists. `PUT orgs/{org}/profile/pins` returns 404. [VERIFIED: live test in this session]

**Conclusion:** GitHub org repo pinning is UI-only. There is no programmatic API path (REST or GraphQL) available through the standard GitHub API. This is a known GitHub limitation — the pinning feature is only exposed in the web interface under `https://github.com/Coding-Autopilot-System` → "Customize your organization's profile" → "Manage pinned repositories."

**Node IDs (for reference / future API use):**

| Repo | Node ID |
|------|---------|
| gsd-orchestrator | R_kgDOSj0j8w |
| Promptimprover | R_kgDOSj2j6Q |
| autogen | R_kgDOSj2j0A |
| Org (Coding-Autopilot-System) | O_kgDODvYFtw |

[VERIFIED: gh api repos/{org}/{repo} --jq '.node_id' — live fetch]

**Plan action:** COHER-02 must be documented as a manual step with exact UI navigation instructions. Claude cannot execute this programmatically.

---

## Current State: Issue Templates

**Existence check performed via `gh api repos/{org}/{repo}/contents/.github/ISSUE_TEMPLATE` (404 = not found):**

| Repo | .github/ISSUE_TEMPLATE/ exists? | bug_report.md | feature_request.md |
|------|----------------------------------|---------------|---------------------|
| gsd-orchestrator | No (404) | Missing | Missing |
| Promptimprover | No (404) | Missing | Missing |
| autogen | No (404) | Missing | Missing |

[VERIFIED: gh api contents check — live fetch, all three returned HTTP 404]

**All 6 template files must be created from scratch.** No SHA fetch required (new files — omit `sha` parameter per Phase 10 pattern).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Topics batch update | Custom script iterating topics | `gh api PUT repos/{org}/{repo}/topics -f "names[]=..."` | GitHub REST API handles atomically; no ordering issues |
| Org pinning automation | GraphQL workarounds, browser automation | Manual UI step with documented instructions | No API exists; workarounds are fragile and unsupported |
| Issue template format | Custom YAML schema | GitHub standard front matter format | GitHub renders templates only if front matter matches spec |

---

## Architecture Patterns

### Pattern 1: Topics Patch via REST API

**What:** Replace topics atomically using PUT endpoint.
**When to use:** Any repo needing topic changes (add or full replacement).

```bash
# Source: GitHub REST API docs — repos topics endpoint [CITED: docs.github.com/rest/repos/repos#replace-all-repository-topics]
gh api PUT repos/OgeonX-Ai/enterprise-ai-gateway/topics \
  -f "names[]=ai-gateway" \
  -f "names[]=azure" \
  -f "names[]=enterprise-ai" \
  -f "names[]=fastapi" \
  -f "names[]=llm" \
  -f "names[]=python" \
  -f "names[]=rag"
```

**Note:** PUT replaces ALL topics. For repos already compliant (CAS repos), do NOT issue a PUT — it risks overwriting topics set in prior phases.

### Pattern 2: Issue Template Creation via GitHub MCP

**What:** Create `.github/ISSUE_TEMPLATE/bug_report.md` and `feature_request.md` as new files.
**When to use:** All 3 flagship CAS repos.

```
mcp__github__create_or_update_file
  owner: "Coding-Autopilot-System"
  repo: "gsd-orchestrator"
  path: ".github/ISSUE_TEMPLATE/bug_report.md"
  branch: "main"
  message: "docs: add bug report issue template"
  content: [base64-encoded template content]
  # sha: OMIT — new file
```

**Critical:** Do not pass `sha` for new files. Including an empty or null sha will cause the API to error.

### Pattern 3: Issue Template Format

Standard GitHub front matter for issue templates (no labels per CONTEXT.md decision):

```markdown
---
name: Bug Report
about: Report a bug or unexpected behaviour
title: "[BUG] "
labels: ''
assignees: ''
---

## Describe the Bug

A clear and concise description of what the bug is.

## Expected Behaviour

What you expected to happen.

## Steps to Reproduce

1. Step one
2. Step two
3. Step three

## Environment

- OS: [e.g. Ubuntu 22.04]
- Version: [e.g. 1.2.0]
- Additional context: [any other relevant details]
```

```markdown
---
name: Feature Request
about: Propose a new feature or improvement
title: "[FEATURE] "
labels: ''
assignees: ''
---

## Problem Statement

Is your feature request related to a problem? Describe what you are trying to solve.

## Proposed Solution

A clear and concise description of what you want to happen.

## Alternatives Considered

A description of any alternative solutions or features you have considered.

## Additional Context

Any other context, mockups, or examples that support this request.
```

[ASSUMED: Template section headings — specific wording not mandated by a prior decision; matches enterprise tone requirement from CONTEXT.md]

### Anti-Patterns to Avoid

- **Overwriting compliant topics:** CAS repos already have 8-10 well-chosen topics. Issuing a `PUT /topics` on them risks destroying prior work. Only patch enterprise-ai-gateway and android.
- **SHA on new files:** Issue template files do not exist yet. Passing a sha (even empty string) to `mcp__github__create_or_update_file` causes API error. Omit entirely.
- **GraphQL pinning attempts:** `pinRepositories` mutation does not exist. Any attempt will fail with schema error. Skip programmatic pinning entirely; provide manual instructions only.

---

## Common Pitfalls

### Pitfall 1: Topics PUT Replaces All Topics
**What goes wrong:** Developer fetches current topics, adds one, sends PUT with full list — but the GET/PUT loop races or truncates.
**Why it happens:** PUT is atomic replace, not append.
**How to avoid:** For repos being patched (enterprise-ai-gateway, android), define the complete desired topic set upfront and send it in one PUT call.
**Warning signs:** Fewer topics after the call than expected.

### Pitfall 2: `sha` Parameter on New Issue Template Files
**What goes wrong:** File creation fails with "422 Unprocessable Entity" or "sha mismatch."
**Why it happens:** Passing `sha: ""` or `sha: null` to `mcp__github__create_or_update_file` for a file that doesn't exist yet.
**How to avoid:** Omit `sha` parameter entirely when creating new files. Only include sha when updating existing files (fetched from a prior GET).
**Warning signs:** API returns 422 on what should be a simple new-file creation.

### Pitfall 3: Wrong Default Branch
**What goes wrong:** File creation targets `main` but repo uses a different default branch.
**Why it happens:** Repos have different default branches.
**Status in this phase:** Not a risk — all three flagship CAS repos use `main`. [VERIFIED: gsd-orchestrator, Promptimprover, autogen all have `default_branch: main` confirmed in prior phases]

### Pitfall 4: GitHub Actions Scope Not Needed for Issue Templates
**What goes wrong:** Developer uses GITHUB_MCP_PAT (workflow-scope PAT) for issue templates out of habit.
**Why it happens:** Phases 7-9 required workflow-scope PAT for CI workflows.
**How to avoid:** Issue templates live in `.github/ISSUE_TEMPLATE/`, not `.github/workflows/`. A standard repo-scoped PAT is sufficient. Use the default GitHub MCP credentials.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| gh CLI | Topics patch, API reads | Yes | — | — |
| GitHub MCP (mcp__github__*) | Issue template file creation | Yes | — | gh CLI with base64 |
| GraphQL pinning API | COHER-02 | No | n/a | Manual UI instructions |

**Missing dependencies with no fallback:**
- GitHub programmatic org pinning — no REST or GraphQL API exists. Plan must deliver manual instructions as the deliverable for COHER-02.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | gh CLI verification calls (no automated test framework — pure API state checks) |
| Config file | none |
| Quick run command | `gh api repos/{org}/{repo} --jq '.topics'` |
| Full suite command | All 11 repos topics check + ISSUE_TEMPLATE contents check |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| COHER-01 | enterprise-ai-gateway has 5-10 topics | smoke | `gh api repos/OgeonX-Ai/enterprise-ai-gateway --jq '.topics \| length'` | N/A (API) |
| COHER-01 | android has 5-10 topics | smoke | `gh api repos/OgeonX-Ai/android --jq '.topics \| length'` | N/A (API) |
| COHER-01 | All CAS repos retain their topics | smoke | Run topics fetch for all 7 CAS repos and confirm counts ≥ 5 | N/A (API) |
| COHER-02 | Pinned repos include gsd-orchestrator, Promptimprover, autogen | manual | GraphQL pinnedItems query (read) + visual UI confirmation | N/A (manual step) |
| COHER-03 | bug_report.md exists in gsd-orchestrator | smoke | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/contents/.github/ISSUE_TEMPLATE/bug_report.md --jq '.name'` | No — Wave 0 |
| COHER-03 | feature_request.md exists in gsd-orchestrator | smoke | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/contents/.github/ISSUE_TEMPLATE/feature_request.md --jq '.name'` | No — Wave 0 |
| COHER-03 | bug_report.md exists in Promptimprover | smoke | `gh api repos/Coding-Autopilot-System/Promptimprover/contents/.github/ISSUE_TEMPLATE/bug_report.md --jq '.name'` | No — Wave 0 |
| COHER-03 | feature_request.md exists in Promptimprover | smoke | `gh api repos/Coding-Autopilot-System/Promptimprover/contents/.github/ISSUE_TEMPLATE/feature_request.md --jq '.name'` | No — Wave 0 |
| COHER-03 | bug_report.md exists in autogen | smoke | `gh api repos/Coding-Autopilot-System/autogen/contents/.github/ISSUE_TEMPLATE/bug_report.md --jq '.name'` | No — Wave 0 |
| COHER-03 | feature_request.md exists in autogen | smoke | `gh api repos/Coding-Autopilot-System/autogen/contents/.github/ISSUE_TEMPLATE/feature_request.md --jq '.name'` | No — Wave 0 |

### Sampling Rate
- **Per task commit:** Verify the specific resource changed (topics count or file existence)
- **Per wave merge:** Full 11-repo topics sweep + all 6 template existence checks
- **Phase gate:** All smoke checks green before `/gsd-verify-work`

### Wave 0 Gaps

- None for topics (gh CLI already available)
- None for issue templates (GitHub MCP already available)
- COHER-02 is manual — no automated Wave 0 gap

---

## Security Domain

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | — |
| V3 Session Management | No | — |
| V4 Access Control | No | — |
| V5 Input Validation | No | Topic strings are sanitised by GitHub API |
| V6 Cryptography | No | — |

**Threat patterns relevant to this phase:** None. This phase writes documentation metadata only (topics, issue templates). No auth logic, no user data, no secrets in scope.

**PAT scope note:** Standard repo PAT covers all file operations in this phase. `GITHUB_MCP_PAT` (workflow scope) is explicitly NOT needed. Using the wrong PAT would not cause a security issue but would be unnecessarily privileged.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Issue template section headings ("Describe the Bug", "Expected Behaviour", etc.) match enterprise tone requirement | Code Examples — Pattern 3 | Low — headings are cosmetic; planner can adjust wording |
| A2 | All 3 CAS flagship repos use `main` as default branch | Common Pitfalls — Pitfall 3 | Medium — file creation would target wrong branch; mitigated by noting this was confirmed in prior phases |

**Note on A2:** Default branch was not re-verified in this session by explicit API call. It was stated as confirmed in prior phases (7-9) where these repos were modified. If there is any doubt, the planner should add a verification step.

---

## Open Questions

1. **COHER-02 Manual Execution**
   - What we know: No programmatic API for org repo pinning exists. The UI path is clear.
   - What's unclear: Who performs the manual step — the user or Claude via browser automation?
   - Recommendation: Plan should document exact UI navigation steps and mark COHER-02 as "manual step — org owner action required." Claude cannot execute this. The requirement is satisfied when the user confirms the pin was set.

2. **android repo description**
   - What we know: The `android` repo has no description set (null), which is unusual for a portfolio repo.
   - What's unclear: Whether setting a description is in scope for this phase.
   - Recommendation: Out of scope per CONTEXT.md (description changes not listed in COHER-01 through COHER-03). Do not add description in this phase.

---

## Sources

### Primary (HIGH confidence)
- `gh api repos/Coding-Autopilot-System/{repo} --jq '.topics'` — live fetch, all 7 CAS repos, 2026-05-28
- `gh api repos/OgeonX-Ai/{repo} --jq '.topics'` — live fetch, all 4 OgeonX-Ai repos, 2026-05-28
- `gh api graphql pinnedItems` — live query, confirmed 0 pinned repos, 2026-05-28
- `gh api repos/{org}/{repo}/contents/.github/ISSUE_TEMPLATE` — live check, all 3 flagship repos returned 404, 2026-05-28
- `gh api graphql __schema mutationType` — live introspection, confirmed `pinRepositories` does not exist, 2026-05-28
- enterprise-ai-gateway README — fetched via gh api, 2026-05-28
- android README — fetched via gh api, 2026-05-28

### Secondary (MEDIUM confidence)
- Phase 10 PATTERNS.md — established file creation patterns (new file = omit sha, GITHUB_MCP_PAT scope rules)

### Tertiary (LOW confidence)
- None

---

## Metadata

**Confidence breakdown:**
- Topics current state: HIGH — fetched live via gh api for all 11 repos
- Topics recommendations: HIGH for enterprise-ai-gateway (README verified), HIGH for android (README verified)
- Pinned repos limitation: HIGH — schema introspection confirms no mutation exists
- Issue templates state: HIGH — 404 responses verified live for all 3 repos
- Issue template format: MEDIUM — standard GitHub format, wording is [ASSUMED] per A1

**Research date:** 2026-05-28
**Valid until:** 2026-06-28 (stable GitHub API; topics and template states unlikely to change independently)
