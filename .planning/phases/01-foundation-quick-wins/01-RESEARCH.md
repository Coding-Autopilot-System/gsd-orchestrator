# Phase 1: Foundation & Quick Wins - Research

**Researched:** 2026-05-21
**Domain:** GitHub repository metadata — topics, descriptions, LICENSE files, org profile README, featured repo visibility
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FOUND-01 | All three repos have correct GitHub topics (5-10 per repo) | Topics PUT API verified working; topic lists crafted for employer discoverability |
| FOUND-02 | Org `.github` profile README showcases all three projects with system diagram | `.github` repo and `profile/README.md` exist; current content is stale (old autopilot-core focus); full rewrite needed; Mermaid renders in org profile |
| FOUND-03 | All three repos have LICENSE (MIT) file | All three repos confirmed MISSING license; GitHub Contents API for file creation verified; MIT template fetched |
| FOUND-04 | ci-autopilot excluded from org featured/pinned repos | No GraphQL/REST API for org pinning exists; ci-autopilot is currently position 7 in push order (below 6-repo display limit, so already not shown); pinning the 3 portfolio repos via GitHub UI is required for robustness — this is a MANUAL step |
| FOUND-05 | All repos have concise, accurate GitHub description (< 100 chars) | PATCH /repos API verified; all three current descriptions either too long or sub-optimal in framing |
</phase_requirements>

---

## Summary

Phase 1 is pure GitHub metadata and content work — no code changes to any of the three project repos. Every requirement maps to a direct GitHub API call (topics PUT, description PATCH, file create via Contents API) or a one-time file write to the `.github` org profile repo. The operations are idempotent and low-risk.

The most substantive deliverable is FOUND-02: a full rewrite of the `.github/profile/README.md`. The file exists but contains entirely wrong content (it was written for a different project epoch, referencing `autopilot-core` and `ci-autopilot` as the primary repos). The new content must introduce gsd-orchestrator, Promptimprover, and autogen as a three-layer AI platform, with a Mermaid system diagram and project cards.

FOUND-04 (ci-autopilot exclusion) has a constraint: GitHub provides no REST or GraphQL API for pinning org repos. Currently, ci-autopilot is position 7 in push order (below the 6-repo display threshold), so it is already not showing on the org page. However, to make this robust, the three portfolio repos should be pinned via GitHub UI — this is the one manual step in the phase.

**Primary recommendation:** Use `gh api` (GitHub REST/GraphQL via `gh` CLI) for all metadata operations. Verify every `gh api` call succeeds before writing PLAN tasks. The only manual step is org repo pinning (GitHub UI, ~2 minutes).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Repository topics | GitHub API (metadata) | — | `PUT /repos/{owner}/{repo}/topics` — pure metadata, no file changes |
| Repository description | GitHub API (metadata) | — | `PATCH /repos/{owner}/{repo}` — pure metadata |
| LICENSE file creation | GitHub Contents API | Local git push | File create via `PUT /repos/{owner}/{repo}/contents/LICENSE` or git commit |
| Org profile README | GitHub Contents API | Local git push | Update `profile/README.md` in `.github` repo |
| Org featured/pinned repos | GitHub UI (manual) | — | No API available — confirmed via exhaustive GraphQL schema search |

---

## Current State of Repos (Verified 2026-05-21)

| Repo | Topics | Description (chars) | LICENSE | Default Branch |
|------|--------|---------------------|---------|----------------|
| gsd-orchestrator | EMPTY | 133 chars — too long, needs trimming | MISSING | main |
| Promptimprover | EMPTY | 72 chars — acceptable length but framing weak | MISSING | master |
| autogen | EMPTY | 43 chars — too short, add context | MISSING | main |
| ci-autopilot | EMPTY | 152 chars | Has MIT (already) | — |
| .github | — | "Organization profile..." | — | main |

**[VERIFIED: gh CLI + GitHub REST API — queried live 2026-05-21]**

### .github Profile README — Current Content (Problem)

The file `profile/README.md` exists (SHA: `34f44b2cf767e165932494f2de63610564cd9abe`, 1242 bytes) but references the old `autopilot-core` project context. It mentions `ci-autopilot`, `autopilot-core`, `autopilot-demo` as the primary repos. **It does not mention gsd-orchestrator, Promptimprover, or autogen at all.** Full rewrite required.

### Org Pinned Repos — Current State

`hasPinnedItems: false` — the org page currently shows all public repos in push order (most recently pushed first). Current push order:

1. gsd-orchestrator (2026-05-21 — today)
2. Promptimprover (2026-05-21 — today)
3. autogen (2026-05-21 — today)
4. cloud-security-service-model (2026-01-03)
5. autopilot-core (2025-12-22)
6. autopilot-demo (2025-12-22)
7. ci-autopilot (2025-12-22) ← **currently position 7, not shown in default 6-repo grid**
8. .github (2025-12-22)

ci-autopilot is already below the display threshold. However, this is fragile — any push to ci-autopilot would move it to position 1. The robust solution is to activate pinning (GitHub UI only).

**[VERIFIED: gh CLI `gh repo list --json name,pushedAt` 2026-05-21]**

---

## Standard Stack (GitHub API Operations)

### Core APIs

| Operation | HTTP Method | Endpoint | Verified |
|-----------|------------|----------|---------|
| Replace all topics | PUT | `/repos/{owner}/{repo}/topics` | YES — tested and reverted |
| Update description | PATCH | `/repos/{owner}/{repo}` | YES — tested and reverted |
| Create/update file | PUT | `/repos/{owner}/{repo}/contents/{path}` | YES — `mcp__github__create_or_update_file` confirmed in MCP |
| Read file (get SHA) | GET | `/repos/{owner}/{repo}/contents/{path}` | YES |
| List org repos | GET | `/orgs/{org}/repos` | YES |
| Pin org repos | — | NOT AVAILABLE via API | CONFIRMED ABSENT |

**[VERIFIED: live API calls and exhaustive GraphQL mutation schema search (266 mutations examined)]**

### GitHub MCP Tools Available

The `mcp__github__*` tool set exposes:

| MCP Tool | Purpose | Used For |
|----------|---------|---------|
| `mcp__github__create_or_update_file` | Create or update a file in a repo | LICENSE creation, profile README update |
| `mcp__github__get_file_contents` | Read file contents + SHA | Getting SHA before update |
| `mcp__github__push_files` | Push multiple files in one commit | Batch license creation |

For topics and descriptions, use `gh api` CLI directly — these operations are not in the MCP tool set. **[VERIFIED: GitHub MCP server README tool inventory]**

### gh CLI Commands (Verified)

```bash
# Replace all topics on a repo
gh api repos/Coding-Autopilot-System/REPO/topics \
  -X PUT \
  -H "Accept: application/vnd.github.mercy-preview+json" \
  --input - <<'EOF'
{"names": ["topic1", "topic2", "topic3"]}
EOF

# Update repo description
gh api repos/Coding-Autopilot-System/REPO \
  -X PATCH \
  -f description="Your new description here"

# Get file SHA (needed before update)
gh api repos/Coding-Autopilot-System/.github/contents/profile/README.md --jq '.sha'

# Create or update a file
gh api repos/Coding-Autopilot-System/REPO/contents/LICENSE \
  -X PUT \
  --input - <<'EOF'
{
  "message": "Add MIT LICENSE",
  "content": "<base64-encoded-content>",
  "branch": "main"
}
EOF
```

**Topic format rules:** lowercase letters and numbers only, hyphens allowed, must start with a lowercase letter or number, max 50 chars. Verified against GitHub validation error response.

---

## Architecture Patterns

### System Architecture Diagram (for org profile README)

Data flow for the org profile README Mermaid diagram. The three repos form a layered platform:

```
Entry point: Hiring manager arrives at github.com/Coding-Autopilot-System
     |
     v
profile/README.md (system overview + project cards)
     |
     +--- gsd-orchestrator (C#/.NET 10) [Layer 2: Autonomous Workflow]
     |         |--- consumes ---> Promptimprover (TypeScript) [Layer 1: Prompt Governance]
     |         |--- calls -----> GitHub API
     |         |--- calls -----> Anthropic Claude API
     |
     +--- Promptimprover (TypeScript) [Layer 1: Prompt Governance]
     |         |--- serves MCP protocol to gsd-orchestrator and autogen
     |
     +--- autogen (Python) [Layer 3: Multi-Agent Coordination]
               |--- delegates via MCP ---> gsd-orchestrator
               |--- routes to ---------> Claude API, Gemini API
```

### Recommended Project Structure (no code changes — metadata and content only)

```
Coding-Autopilot-System/.github/
└── profile/
    └── README.md          # FULL REWRITE — org landing page

Coding-Autopilot-System/gsd-orchestrator/
└── LICENSE                # CREATE — MIT 2026

Coding-Autopilot-System/Promptimprover/
└── LICENSE                # CREATE — MIT 2026

Coding-Autopilot-System/autogen/
└── LICENSE                # CREATE — MIT 2026
```

### Pattern: Topics via PUT (replace all)

Topics are set by replacing the entire list atomically. The API does not support add/remove individual topics — only replace-all. Send the complete desired topic array each time.

```bash
# Source: VERIFIED via live test 2026-05-21
gh api repos/Coding-Autopilot-System/gsd-orchestrator/topics \
  -X PUT \
  -H "Accept: application/vnd.github.mercy-preview+json" \
  --input - <<'EOF'
{"names": ["autonomous-agent", "github-automation", "dotnet", "csharp", "mcp", "model-context-protocol", "claude-ai", "state-machine", "agentic-ai", "dotnet10"]}
EOF
```

### Pattern: File create/update via Contents API

Requires base64-encoded content. For updates, must include the current SHA.

```bash
# Source: VERIFIED GitHub REST API docs [CITED: docs.github.com/rest/repos/contents]
CONTENT=$(echo "MIT License..." | base64 -w 0)
gh api repos/Coding-Autopilot-System/REPO/contents/LICENSE \
  -X PUT \
  --input - <<EOF
{
  "message": "Add MIT LICENSE",
  "content": "$CONTENT",
  "branch": "main"
}
EOF
```

For updating an existing file, add `"sha": "CURRENT_SHA"` to the JSON body.

### Anti-Patterns to Avoid

- **Adding topics individually with + operator:** Not supported — always replace all topics atomically.
- **Creating LICENSE without SHA when file already exists:** Will return 422. Always GET the file SHA first if the file might already exist.
- **Using `master` branch for Promptimprover LICENSE:** Promptimprover's default branch is `master` (not `main`). License must be committed to the `master` branch. [VERIFIED: `gh api repos/Coding-Autopilot-System/Promptimprover --jq '.default_branch'` returns `master`]
- **Topics with uppercase letters:** GitHub rejects them — format must be `[a-z0-9][a-z0-9-]*`.
- **Descriptions over 100 chars:** GitHub doesn't enforce a hard limit server-side but truncates in UI. Keep under 100 to avoid truncation.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Base64 encoding license | Custom encoder | `base64 -w 0` in bash or `Convert.ToBase64String` | Standard utility, handles edge cases |
| Topic validation | Custom regex | Trust GitHub's 422 response to catch bad formats | API returns clear error message |
| Profile README Mermaid diagram | PNG/SVG | Mermaid code block in markdown | GitHub renders natively, version-controlled |

**Key insight:** All Phase 1 operations are GitHub API calls and file writes. There is nothing to build — only to invoke existing APIs correctly.

---

## Prescribed Content for Each Requirement

### FOUND-01: Topics Per Repo

**gsd-orchestrator** (use exactly these — verified format, employer-optimal):
```json
["autonomous-agent", "github-automation", "dotnet", "csharp", "mcp", "model-context-protocol", "claude-ai", "state-machine", "agentic-ai", "dotnet10"]
```

**Promptimprover**:
```json
["mcp", "model-context-protocol", "typescript", "prompt-engineering", "prompt-governance", "rag", "llm", "mcp-server", "enterprise-ai", "ai-governance"]
```

**autogen**:
```json
["multi-agent", "python", "microsoft-autogen", "gemini", "claude-ai", "agent-framework", "agentic-ai", "ai-automation", "ag-ui", "llm"]
```

**Why these topics:** `mcp` and `model-context-protocol` are niche, high-signal topics in the 2025/2026 AI engineering job market. `agentic-ai` and `autonomous-agent` match 2025/2026 job description language. `dotnet10` signals current knowledge. These 10 topics per repo are within the GitHub-recommended 5-10 range.

[CITED: .planning/research/FEATURES.md — topic strategy section]
[ASSUMED: specific topic search ranking effectiveness — employer search behavior not directly measurable]

### FOUND-02: Org Profile README Content Specification

**File:** `Coding-Autopilot-System/.github/profile/README.md`
**Action:** Full rewrite (current content is for wrong project context)
**Current SHA:** `34f44b2cf767e165932494f2de63610564cd9abe` (required for update via Contents API)

Structure (from ARCHITECTURE.md prescriptions):
1. `# Coding-Autopilot-System` headline
2. 2-3 sentence value proposition (enterprise tone, polyglot credibility)
3. Mermaid system diagram (3-layer: Prompt Governance → Autonomous Workflow → Multi-Agent)
4. Three project cards (name, badges, one-paragraph description, enterprise patterns called out)
5. Technology Coverage table
6. Author link to OgeonX-Ai

The Mermaid diagram from `.planning/research/ARCHITECTURE.md` is the correct content — the `graph TB` subgraph diagram showing the three layers with external system connections.

### FOUND-03: MIT LICENSE Template

```
MIT License

Copyright (c) 2026 OgeonX-Ai

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

**[VERIFIED: `gh api licenses/mit` 2026-05-21 — template confirmed]**

Same LICENSE text for all three repos. Year: 2026. Author: OgeonX-Ai.

### FOUND-04: ci-autopilot Exclusion

**Current state:** ci-autopilot is already position 7 in push order — not visible in the default 6-repo org grid.

**Robust solution:** Pin the three portfolio repos via GitHub UI (Settings → Customize your organization → Pin repositories). This activates `hasPinnedItems=true` and shows ONLY the pinned repos on the org page.

**API constraint:** No REST or GraphQL API exists for org repo pinning. Confirmed by:
1. Exhaustive search of all 266 GraphQL mutations — no pin/showcase mutations for orgs
2. `GET /orgs/Coding-Autopilot-System` response — no pinning fields
3. `itemShowcase.hasPinnedItems` is readable but not mutable via API

**This is the only manual step in Phase 1.** Estimated time: 2 minutes in GitHub UI.

**Planner note:** Create a task documenting this as a manual step with exact UI instructions. Do not create a task that tries to do this via API — it will fail.

### FOUND-05: Descriptions (< 100 chars, employer-facing)

Current descriptions and recommended replacements:

| Repo | Current (chars) | Recommended (chars) | Change |
|------|----------------|---------------------|--------|
| gsd-orchestrator | 133 — too long | "Autonomous .NET 10 agent: reads GitHub issues, plans via Claude AI, branches, edits, and opens PRs" (99) | Trim + clarify |
| Promptimprover | 72 — weak framing | "TypeScript MCP server for prompt governance: RAG-powered refinement, ISO 27001 compliance framing" (98) | Enterprise reframe |
| autogen | 43 — too sparse | "Python multi-agent automation: Microsoft AutoGen + Gemini/Claude fallback, AG-UI observability" (95) | Add detail |

[ASSUMED: exact wording of descriptions — user may prefer different phrasing; these are researched defaults]

---

## Common Pitfalls

### Pitfall 1: Topics Accept Only Lowercase

**What goes wrong:** Sending `"dotNet"` or `"MCP"` in the topics array returns HTTP 422 Validation Failed.
**Why it happens:** GitHub enforces lowercase-only topic names.
**How to avoid:** All topics in the prescribed lists above are already lowercase. Validate format before sending.
**Warning signs:** HTTP 422 with message "must start with a lowercase letter or number..."

### Pitfall 2: File Update Without SHA Overwrites Can Fail

**What goes wrong:** Sending a PUT to create/update a file that already exists without the `sha` field returns HTTP 422 or 409 Conflict.
**Why it happens:** GitHub uses the SHA as an optimistic lock to prevent blind overwrites.
**How to avoid:** Always GET the file first to retrieve its SHA, then include `"sha": "..."` in the PUT body. The `profile/README.md` SHA is `34f44b2cf767e165932494f2de63610564cd9abe`.
**Warning signs:** HTTP 422 "sha" required error.

### Pitfall 3: Promptimprover Uses `master` Branch, Not `main`

**What goes wrong:** Committing LICENSE to the `main` branch on Promptimprover creates a new orphan branch instead of updating the default branch.
**Why it happens:** Promptimprover's default branch is `master` (confirmed via API).
**How to avoid:** Specify `"branch": "master"` in the file creation API call for Promptimprover. For gsd-orchestrator and autogen, use `"branch": "main"`.
**Warning signs:** License appears on a new `main` branch that doesn't exist as the default.

### Pitfall 4: Org Profile Mermaid Subgraph Labels

**What goes wrong:** Mermaid subgraph labels with colons fail to render — e.g., `subgraph "Layer 1: Prompt Governance"` causes a parse error.
**Why it happens:** GitHub's Mermaid renderer doesn't support colons in subgraph labels.
**How to avoid:** Use em dashes instead — `subgraph "Layer 1 — Prompt Governance"`. [VERIFIED: .planning/research/ARCHITECTURE.md Mermaid rules section]
**Warning signs:** Diagram renders as a fenced code block instead of a diagram.

### Pitfall 5: Pinning Cannot Be Automated

**What goes wrong:** Planner or executor tries to pin org repos via API and gets 404 or "Field doesn't exist on type Mutation" error.
**Why it happens:** GitHub never published an API for org repo pinning.
**How to avoid:** Document FOUND-04 as a manual GitHub UI step. Do not attempt programmatic pinning.
**Warning signs:** Any task that tries `gh api graphql ... pinPinnedItem` or similar.

### Pitfall 6: Base64 Line Wrapping Breaks File Content

**What goes wrong:** `base64` (without `-w 0`) wraps output at 76 chars, creating newlines in the encoded string. GitHub's Contents API rejects or misinterprets the content.
**Why it happens:** Default base64 behavior wraps at 76 chars.
**How to avoid:** Always use `base64 -w 0` (or equivalent `--wrap=0`) to produce a single-line base64 string.

---

## Code Examples

### Create LICENSE File (gsd-orchestrator, main branch)

```bash
# Source: VERIFIED — GitHub REST API Contents endpoint
LICENSE_CONTENT=$(cat <<'EOF'
MIT License

Copyright (c) 2026 OgeonX-Ai

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
EOF
)
ENCODED=$(echo "$LICENSE_CONTENT" | base64 -w 0)
gh api repos/Coding-Autopilot-System/gsd-orchestrator/contents/LICENSE \
  -X PUT \
  --input - <<EOF
{
  "message": "Add MIT LICENSE",
  "content": "$ENCODED",
  "branch": "main"
}
EOF
```

### Set Topics (gsd-orchestrator)

```bash
# Source: VERIFIED via live test 2026-05-21
gh api repos/Coding-Autopilot-System/gsd-orchestrator/topics \
  -X PUT \
  -H "Accept: application/vnd.github.mercy-preview+json" \
  --input - <<'EOF'
{"names": ["autonomous-agent", "github-automation", "dotnet", "csharp", "mcp", "model-context-protocol", "claude-ai", "state-machine", "agentic-ai", "dotnet10"]}
EOF
```

### Update Org Profile README

```bash
# Source: VERIFIED GitHub REST API Contents endpoint
# Step 1: Get current SHA
SHA=$(gh api repos/Coding-Autopilot-System/.github/contents/profile/README.md --jq '.sha')

# Step 2: Build content and update
CONTENT=$(cat <<'EOF'
[... new README content ...]
EOF
)
ENCODED=$(printf '%s' "$CONTENT" | base64 -w 0)
gh api repos/Coding-Autopilot-System/.github/contents/profile/README.md \
  -X PUT \
  --input - <<EOF
{
  "message": "Rewrite org profile README — showcase gsd-orchestrator, Promptimprover, autogen",
  "content": "$ENCODED",
  "sha": "$SHA",
  "branch": "main"
}
EOF
```

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| gh CLI | All GitHub API calls | Yes | — | mcp__github__ tools for file ops |
| GitHub REST API | Topics, descriptions, files | Yes | v3 | — |
| GitHub GraphQL API | Org query | Yes | v4 | — |
| `base64` utility | File content encoding | Yes | standard | Python `base64` module |
| GitHub MCP server | `mcp__github__create_or_update_file` | Yes (running on localhost:8765) | v1.0.5 | gh CLI |

**No missing dependencies.**

---

## Validation Architecture

> `nyquist_validation: true` — section required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | None — no test framework in this repo |
| Config file | none |
| Quick run command | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/topics` (verify topics set) |
| Full suite command | see verification commands below |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FOUND-01 | Topics set on all 3 repos | API verification | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/topics --jq '.names | length'` returns ≥5 | ❌ Wave 0 |
| FOUND-02 | Org profile README mentions all 3 repos | Content check | `gh api repos/Coding-Autopilot-System/.github/contents/profile/README.md --jq '.content' \| base64 -d \| grep -c 'gsd-orchestrator\|Promptimprover\|autogen'` returns 3 | ❌ Wave 0 |
| FOUND-03 | LICENSE files exist in all 3 repos | API check | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/license --jq '.license.key'` returns `mit` | ❌ Wave 0 |
| FOUND-04 | ci-autopilot not in featured | GraphQL check | `gh api graphql -f query='{organization(login:"Coding-Autopilot-System"){pinnedItems(first:10,types:REPOSITORY){nodes{...on Repository{name}}}}}' --jq '.data.organization.pinnedItems.nodes[].name' \| grep -v ci-autopilot` | ❌ Wave 0 (manual step) |
| FOUND-05 | Descriptions < 100 chars | Length check | `gh api repos/Coding-Autopilot-System/gsd-orchestrator --jq '.description \| length'` returns < 100 | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** Run the specific `gh api` verification for that task's requirement
- **Per wave merge:** Run all 5 requirement checks
- **Phase gate:** All 5 verifications pass before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] Verification script `verify-phase1.sh` — runs all 5 requirement checks in sequence
- [ ] No test framework needed — all verification is via `gh api` CLI calls

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No auth flows in this phase |
| V3 Session Management | No | No sessions |
| V4 Access Control | No | No access control logic |
| V5 Input Validation | Yes (minimal) | GitHub API rejects invalid topic formats with 422 |
| V6 Cryptography | No | No crypto |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| License content injection | Tampering | File content is static MIT text — no user input |
| README content injection | Tampering | Content is authored content — not generated from user input |
| Token in gh CLI commands | Info Disclosure | `gh` uses stored auth token from `gh auth login` — not passed in command line; safe |

**Security note:** The GITHUB_PERSONAL_ACCESS_TOKEN in `.env` is used by the orchestrator but NOT exposed in any Phase 1 API call syntax. All `gh api` calls use the ambient `gh auth` context.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Recommended description text for all three repos | Prescribed Content — FOUND-05 | User may prefer different wording; planner should mark as user-confirmable before execution |
| A2 | "OgeonX-Ai" is the correct copyright holder name for MIT LICENSE | FOUND-03 content | Wrong name in LICENSE is a minor but visible error; confirm with user |
| A3 | Specific topic search ranking effectiveness on GitHub recruiter search | FOUND-01 | Topics are researched but employer search behavior is not directly measurable |

---

## Open Questions

1. **Promptimprover `master` vs `main` branch**
   - What we know: Default branch is `master` (verified via API)
   - What's unclear: Should we rename it to `main` as part of Phase 1, or leave it?
   - Recommendation: Leave for Phase 4 (Promptimprover Polish). Phase 1 should only target LICENSE/topics/description. Add note to Phase 4 research.

2. **FOUND-04 manual step confirmation**
   - What we know: No API exists for org pinning; the user must do this in GitHub UI
   - What's unclear: Whether the user is aware this is a manual step
   - Recommendation: Include explicit manual step task in PLAN.md with exact UI navigation instructions (Settings → Customize your organization → Pin repositories)

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `application/vnd.github.mercy-preview+json` header required for topics | Still recommended for compatibility | Stable since 2017 | Include header in all topics PUT calls |
| Topics via GitHub UI only | `PUT /repos/{owner}/{repo}/topics` REST API | 2017 | Automatable |
| Org profile via special repo | `.github` repo + `profile/README.md` | 2021 | Already exists in this org |

**Deprecated/outdated:**
- The `application/vnd.github.mercy-preview+json` preview header: still works and should be included for compatibility; it signals the topics API even though it's now GA [ASSUMED — header still included in official examples as of training cutoff].

---

## Sources

### Primary (HIGH confidence)
- Live `gh api` calls — topics GET/PUT, description PATCH, file contents GET, org GraphQL query — all verified 2026-05-21
- GitHub REST API — `/repos/{owner}/{repo}/topics` PUT — tested working live
- GitHub REST API — `/repos/{owner}/{repo}` PATCH for description — tested working live
- GitHub REST API — `/repos/{owner}/{repo}/contents/{path}` PUT — docs verified
- GitHub GraphQL schema — 266 mutations enumerated, no org pin mutations found
- `gh api licenses/mit` — MIT license template text fetched live

### Secondary (MEDIUM confidence)
- `.planning/research/ARCHITECTURE.md` — org profile structure, Mermaid diagram content
- `.planning/research/FEATURES.md` — topic selection strategy, description standards
- `.planning/research/PITFALLS.md` — common mistakes to avoid

### Tertiary (LOW confidence / ASSUMED)
- GitHub topic search ranking effectiveness for employer discoverability
- Exact description wording preferences

---

## Metadata

**Confidence breakdown:**
- API mechanics (topics, description, file creation): HIGH — all verified live
- FOUND-04 API limitation (no org pin API): HIGH — exhaustive schema search confirmed
- Current repo state: HIGH — queried live 2026-05-21
- Topic keyword effectiveness: MEDIUM — based on industry patterns, not directly measured
- Description wording: ASSUMED — researched defaults, user confirmation recommended

**Research date:** 2026-05-21
**Valid until:** 2026-06-21 (stable GitHub API — low decay risk)
