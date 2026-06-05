# Roadmap — Enterprise GitHub Portfolio

**Milestone 1.0 — Portfolio Launch**
Goal: Transform Coding-Autopilot-System into a job-landing enterprise portfolio.

---

## Phase 1 — Foundation & Quick Wins

**Goal:** Every repo is discoverable, described, and legally clean. Org profile exists. Noise removed.

**Requirements:** FOUND-01, FOUND-02, FOUND-03, FOUND-04, FOUND-05

**Plans:** 4 plans

Plans:
- [ ] 01-01-PLAN.md — Set GitHub topics (FOUND-01) and descriptions (FOUND-05) on all three repos
- [ ] 01-02-PLAN.md — Create MIT LICENSE files in gsd-orchestrator, Promptimprover, and autogen (FOUND-03)
- [ ] 01-03-PLAN.md — Rewrite org profile README with system diagram and project cards (FOUND-02)
- [ ] 01-04-PLAN.md — Verify and pin portfolio repos; exclude ci-autopilot from org featured (FOUND-04)

**Success Criteria:**
- Searching "autonomous-agent dotnet" on GitHub surfaces gsd-orchestrator
- Org page shows a profile README with system diagram
- All three repos have green license badge
- ci-autopilot not visible as first impression of the org

**Depends on:** Nothing
**Estimated effort:** Small (metadata + file creation)

---

## Phase 2 — gsd-orchestrator CI & Diagrams

**Goal:** gsd-orchestrator has a passing CI badge and architecture diagrams that demonstrate systems thinking.

**Requirements:** GSD-01, GSD-02, GSD-03, GSD-09

**Plans:** 2 plans

Plans:
- [x] 02-01-PLAN.md — Create .github/workflows/ci.yml (.NET 10 build on windows-latest) (GSD-01) — 2026-05-22
- [x] 02-02-PLAN.md — Add badges, stateDiagram-v2 state machine, and flowchart component diagram to README (GSD-02, GSD-03, GSD-09) — 2026-05-22

**Success Criteria:**
- CI runs green on push to main
- README shows passing CI badge
- Two Mermaid diagrams render correctly on GitHub
- A hiring manager can understand the system in 60 seconds from the README

**Depends on:** Phase 1 (LICENSE for badge)
**Estimated effort:** Small-medium

---

## Phase 3 — gsd-orchestrator Wiki & Release

**Goal:** gsd-orchestrator has enterprise-grade documentation and a v1.0.0 release.

**Requirements:** GSD-04, GSD-05, GSD-06, GSD-07, GSD-08

**Plans:** 3 plans

Plans:
- [x] 03-00-PLAN.md — Manual checkpoint: initialize wiki.git via GitHub web UI (GSD-04, GSD-05, GSD-06, GSD-07 blocker) — 2026-05-23
- [x] 03-01-PLAN.md — Clone wiki repo and push all 4 pages: Home, Setup Guide, Architecture, Configuration Reference (GSD-04, GSD-05, GSD-06, GSD-07) — 2026-05-23
- [x] 03-02-PLAN.md — Create GitHub Release v1.0.0 with feature-narrative release notes (GSD-08) — 2026-05-23

**Success Criteria:**
- Wiki has 4 pages, all with substantive content
- Release v1.0.0 appears on repo page
- Setup Guide tested — instructions are copy-pasteable and accurate
- Architecture page has at least one diagram

**Depends on:** Phase 2 (CI must pass before releasing)
**Estimated effort:** Medium

---

## Phase 4 — Promptimprover Polish

**Goal:** Promptimprover has an enterprise README, passing CI, and Wiki.

**Requirements:** PI-01, PI-02, PI-03, PI-04, PI-05

**Plans:** 4 plans

Plans:
- [x] 04-00-PLAN.md — Manual checkpoint: initialize Promptimprover wiki.git via GitHub web UI (PI-03 blocker) — 2026-05-24
- [x] 04-01-PLAN.md — Create .github/workflows/ci.yml (Node 22 / TypeScript build on ubuntu-latest) (PI-02) — 2026-05-24
- [x] 04-02-PLAN.md — Rewrite README with hero line, badges, Mermaid architecture diagram, and cross-repo links (PI-01, PI-04, PI-05) — 2026-05-24
- [x] 04-03-PLAN.md — Clone Promptimprover.wiki.git and push 4 wiki pages: Home, Setup Guide, Architecture, Configuration Reference (PI-03) — 2026-05-24

**Success Criteria:**
- README no longer uses internal language; hero line and architecture diagram present
- CI badge is green on master branch
- Wiki has 4 pages with substantive content
- README links to gsd-orchestrator and autogen via cross-repo ecosystem line

**Depends on:** Phase 1
**Estimated effort:** Medium

---

## Phase 5 — autogen Polish

**Goal:** autogen has an enterprise README, passing CI, and Wiki.

**Requirements:** AG-01, AG-02, AG-03, AG-04, AG-05

**Plans:** 4 plans

Plans:
- [x] 05-00-PLAN.md — Manual checkpoint: initialize autogen wiki.git via GitHub web UI (AG-03 blocker) — 2026-05-24
- [x] 05-01-PLAN.md — Create .github/workflows/ci.yml (Python 3.12 / pytest on ubuntu-latest) (AG-02) — 2026-05-24
- [x] 05-02-PLAN.md — Rewrite README with hero line, badges, Mermaid architecture diagram, and cross-repo links (AG-01, AG-04, AG-05) — 2026-05-24
- [x] 05-03-PLAN.md — Clone autogen.wiki.git and push 4 wiki pages: Home, Setup Guide, Architecture, Configuration Reference (AG-03) — 2026-05-24

**Success Criteria:**
- README positions autogen as "enterprise multi-agent system" not "starter"
- CI badge is green on main branch
- Wiki has 4 pages with substantive content
- README links to gsd-orchestrator and Promptimprover

**Depends on:** Phase 1
**Estimated effort:** Medium

---

## Phase 6 — Coherence & Personal Profile

**Goal:** All repos tell one story. Personal profile connects identity to the work.

**Requirements:** COH-01, COH-02, COH-03

**Plans:** 2 plans

Plans:
- [ ] 06-01-PLAN.md — Add Coding-Autopilot-System ecosystem line to gsd-orchestrator README (COH-02)
- [ ] 06-02-PLAN.md — Rewrite OgeonX-Ai personal profile README + update org profile diagram (COH-01, COH-03)

**Success Criteria:**
- OgeonX-Ai GitHub profile shows portfolio work
- Any repo README leads to the org and the other two repos
- Org profile README is the definitive landing page for the portfolio

**Depends on:** Phases 2-5 (all repos polished before linking them)
**Estimated effort:** Small

---

## Coverage Check (Milestone 1.0)

| Requirement | Phase |
|-------------|-------|
| FOUND-01--05 | 1 |
| GSD-01, 02, 03, 09 | 2 |
| GSD-04, 05, 06, 07, 08 | 3 |
| PI-01--05 | 4 |
| AG-01--05 | 5 |
| COH-01--03 | 6 |

**All 23 requirements covered across 6 phases ✓**

---

# Milestone 2.0 — Full Org Documentation

**Goal:** Every public repo in Coding-Autopilot-System and OgeonX-Ai meets Level A documentation standard (README rewrite + wiki 4 pages + CI badge + release + cross-links). All repos tell one coherent AI engineer story.

**Repos in scope:**
- CAS: ci-autopilot, autopilot-core, autopilot-demo, cloud-security-service-model
- OgeonX-Ai: enterprise-ai-gateway, android, kim-ai-voice-demo, My-CV

---

## Phase 7 — EMERGENCY: Fix ci-autopilot + Level A Docs

**Goal:** Stop the runaway `runner-health.yml` workflow from generating issues. Bulk-close 1,964+ existing issues. Then bring ci-autopilot to Level A documentation standard.

**Requirements:** CIAP-01, CIAP-02, CIAP-03

**Plans:** 3 plans

Plans:
- [x] 07-00-PLAN.md — Manual checkpoint: initialize ci-autopilot wiki.git via GitHub web UI (CIAP-03 blocker) [Wave 0] — 2026-05-26
- [x] 07-01-PLAN.md — Disable runner-health.yml cron + bulk-close all `runner-offline` issues via GitHub API (CIAP-01, CIAP-02) [Wave 1] — 2026-05-26
- [x] 07-02-PLAN.md — ci-autopilot Level A: README rewrite, CI badge, wiki 4 pages, GitHub topics, cross-links (CIAP-03) [Wave 2] — 2026-05-26

**Success Criteria:**
- `runner-health.yml` no longer creates new issues (cron disabled)
- All 1,964 open `runner-offline` issues closed
- ci-autopilot README leads with "AI-powered CI autopilot" not Azure/DevOps noise
- Wiki has 4 pages, CI badge green, 8 topics set

**Depends on:** Phase 6 (Phase 1.0 complete)
**Estimated effort:** Medium (emergency triage + Level A docs)

---

## Phase 8 — CAS Secondary Repos Level A

**Goal:** autopilot-core, autopilot-demo, and cloud-security-service-model reach Level A documentation.

**Requirements:** ACOR-01, ACOR-02, CSEC-01

**Plans:** 4 plans

Plans:
- [x] 08-01-PLAN.md — autopilot-core Level A: README rewrite, CI badge, wiki 4 pages, topics, cross-links (ACOR-01) [Wave 1]
- [x] 08-02-PLAN.md — autopilot-demo Level A: README rewrite, CI badge, wiki 4 pages, topics, cross-links (ACOR-02) [Wave 1]
- [x] 08-03-PLAN.md — cloud-security-service-model documentation: README (framework framing), wiki 4 pages, topics (CSEC-01) [Wave 2]
- [x] 08-04-PLAN.md — CSEC-01 gap closure: add .markdownlint.json to fix MD013 CI failure, restore green badge (CSEC-01) [Wave 3]

**Success Criteria:**
- All three repos have enterprise-grade README with hero line and architecture section
- autopilot-core and autopilot-demo have passing CI badge
- cloud-security-service-model README explains the framework/methodology clearly

**Depends on:** Phase 7
**Estimated effort:** Medium

---

## Phase 9 — OgeonX-Ai Core Tech AI Reframe + Level A

**Goal:** enterprise-ai-gateway and android are repositioned as AI engineering work with full Level A docs.

**Requirements:** TECH-01, TECH-02

**Plans:** 2 plans

Plans:
- [x] 09-01-PLAN.md — enterprise-ai-gateway AI reframe: README hero line, architecture diagram, wiki 4 pages, CI badge, cross-links to CAS (TECH-01) [Wave 1]
- [x] 09-02-PLAN.md — android AI reframe: scan codebase, README (Android + AI framing), wiki 4 pages, CI badge (TECH-02) [Wave 1]

**Success Criteria:**
- enterprise-ai-gateway README positions it as AI infrastructure, not generic gateway
- android README explains its purpose in AI engineer context with accurate tech description
- Both repos have wiki and CI badge

**Depends on:** Phase 6 (personal profile already updated)
**Estimated effort:** Medium (android requires codebase scan)

---

## Phase 10 — OgeonX-Ai Portfolio Repos AI Reframe + Level A

**Goal:** kim-ai-voice-demo and My-CV repositioned from demos to portfolio artifacts with Level A docs.

**Requirements:** PORT-01, PORT-02

**Plans:** 3 plans

Plans:
- [x] 10-00-PLAN.md — Manual checkpoint: initialize kim-ai-voice-demo and My-CV wiki.git via GitHub web UI (PORT-01, PORT-02 blocker) [Wave 0] — 2026-05-28
- [x] 10-01-PLAN.md — kim-ai-voice-demo full Level A: CI workflow (Node.js), README rewrite (AI voice engineering framing), 4 wiki pages, 8 topics (PORT-01) [Wave 1] — 2026-05-28
- [x] 10-02-PLAN.md — My-CV full Level A: MIT LICENSE, CI workflow (HTML validation), README rewrite (AI-powered career tool), 4 wiki pages, 7 topics (PORT-02) [Wave 1] — 2026-05-28

**Success Criteria:**
- kim-ai-voice-demo README leads with AI voice engineering, not ElevenLabs product demo
- My-CV README explains AI toolchain used to build and maintain CV
- Both repos have 4 wiki pages
- Both repos have CI badges (green)
- Both repos have GitHub topics set

**Depends on:** Phase 9
**Estimated effort:** Small-medium

---

## Phase 11 — Cross-Portfolio Final Coherence

**Goal:** Topics, pinned repos, and issue templates are consistent and discoverable across all orgs.

**Requirements:** COHER-01, COHER-02, COHER-03

**Plans:** 2 plans

Plans:
- [x] 11-01-PLAN.md — Topics audit: all repos get 5-10 accurate topics; org pinned repos set to gsd-orchestrator, Promptimprover, autogen (COHER-01, COHER-02) [Wave 1] — 2026-05-28
- [x] 11-02-PLAN.md — Issue templates: standardize bug_report.md and feature_request.md across all CAS repos (COHER-03) [Wave 2] — 2026-05-28

**Success Criteria:**
- All repos discoverable by relevant GitHub topic searches
- Coding-Autopilot-System org page pins flagship 3 repos
- CAS repos have consistent issue templates

**Depends on:** Phases 7-10 (all repos polished)
**Estimated effort:** Small

---

## Coverage Check (Milestone 2.0)

| Requirement | Phase |
|-------------|-------|
| CIAP-01–03 | 7 |
| ACOR-01–02, CSEC-01 | 8 |
| TECH-01–02 | 9 |
| PORT-01–02 | 10 |
| COHER-01–03 | 11 |

**All 11 v2 requirements covered across 5 phases ✓**

---

# Milestone 3.0 — gsd-orchestrator Feature Expansion

**Goal:** Extend gsd-orchestrator from a single-repo issue-to-PR automator into a multi-repo, triage-aware, test-generating autonomous engineering platform.

**Repo:** Coding-Autopilot-System/gsd-orchestrator (C#/.NET 10)

---

## Phase 12 — Robustness Foundation

**Goal:** Structured logging, unit test scaffold, and circuit breaker. This phase unlocks all subsequent phases by ensuring the codebase is instrumented, partially tested, and resilient before new states are added.

**Requirements:** ROB-01, ROB-02, ROB-03

**Plans:** 3 plans

Plans:
- [x] 12-01-PLAN.md — Serilog structured logging: add packages to csproj, register AddSerilog in Program.cs, instrument GsdStateMachine (ROB-01) [Wave 1] — 2026-05-29
- [x] 12-02-PLAN.md — Polly circuit breaker: extend AddResiliencePipeline with AddCircuitBreaker, catch BrokenCircuitException in McpToolDispatcher (ROB-03) [Wave 1] — 2026-05-30
- [x] 12-03-PLAN.md — xUnit test project: create GsdOrchestrator.Tests, write 7 GsdStateMachine unit tests with NSubstitute mocks (ROB-02) [Wave 2] — 2026-06-01

**Success Criteria:**
- `dotnet build` still green
- Serilog emits JSON-structured logs for every state transition
- xUnit project exists with >= 20% coverage on state machine and MCP client
- Circuit breaker prevents cascading MCP failures

**Depends on:** Nothing (brownfield — codebase exists)
**Estimated effort:** Medium

---

## Phase 13 — Smarter Issue Triage

**Goal:** Issues are classified before the orchestrator commits to full planning and editing.

**Requirements:** TRIAGE-01, TRIAGE-02, TRIAGE-03, TRIAGE-04

**Plans:** 2 plans

Plans:
- [x] 13-01-PLAN.md — Merge Phase 12 test infrastructure, extend WorkflowModels with triage types, write 7 TriagingStateTests (Wave 0 RED) (TRIAGE-01, TRIAGE-02, TRIAGE-03, TRIAGE-04) [Wave 1] — 2026-06-02
- [x] 13-02-PLAN.md — Create TriagingState.cs, wire into IdleState + Program.cs + GsdStateMachine, all 14 tests GREEN (TRIAGE-01, TRIAGE-02, TRIAGE-03, TRIAGE-04) [Wave 2] — 2026-06-02

**Success Criteria:**
- `TriagingState` inserted between `IdleState` and `AnalyzingState`
- `--triage` mode exits after classification with a comment posted to the issue
- Duplicate issues detected and skipped with a comment
- Out-of-scope issues closed/labelled, workflow exits cleanly

**Depends on:** Phase 12 (logging in place)
**Estimated effort:** Medium

---

## Phase 14 — Autonomous Test Generation

**Goal:** Code changes are paired with generated tests, committed to the same branch.

**Requirements:** TESTGEN-01, TESTGEN-02, TESTGEN-03

**Plans:** 2 plans

Plans:
- [x] 14-01-PLAN.md — Extend WorkflowModels with TestGenerating types; 7 RED test stubs (TESTGEN-01, TESTGEN-02) [Wave 1] — 2026-06-04
- [x] 14-02-PLAN.md — Implement TestGeneratingState + wire EditingState/ValidatingState/Program.cs; all 21 tests GREEN (TESTGEN-01, TESTGEN-02, TESTGEN-03) [Wave 2] — 2026-06-04

**Success Criteria:**
- `TestGeneratingState` executes after `EditingState`
- Tests file(s) committed alongside code changes on the feature branch
- `ValidatingState` checks test compilation succeeds

**Depends on:** Phase 12 (logging), Phase 13 (triage reduces noise before test gen)
**Estimated effort:** Medium-large (Claude prompt engineering for test generation)

---

## Phase 15 — PR Review Loop

**Goal:** Orchestrator can review open PRs and post structured inline review comments.

**Requirements:** REV-01, REV-02, REV-03

**Plans:** 2/2 plans complete

Plans:
- [x] 15-01-PLAN.md — Extend WorkflowModels with PR-review contracts (ReviewComment, ReviewResult, PrReviewContext); 7 RED test stubs (REV-01, REV-02) [Wave 1]
- [x] 15-02-PLAN.md — Implement PR-review-loop ReviewingState + --pr flag in Program.cs; all 28 tests GREEN (REV-01, REV-02, REV-03) [Wave 2]

**Success Criteria:**
- `--pr <N>` mode reads diff, invokes Claude, posts inline comments with severity
- Approve or request-changes submitted based on Claude's assessment
- Existing `--issue` flow unaffected

**Depends on:** Phase 12 (logging)
**Estimated effort:** Medium (new operating mode, GitHub PR review API)

---

## Phase 16 — Multi-Repo Support

**Goal:** Watch mode and issue processing work across multiple repos without reconfiguration.

**Requirements:** MULTI-01, MULTI-02, MULTI-03, MULTI-04

**Plans:** 1/2 plans executed

Plans:
- [x] 16-01-PLAN.md — Add RepoConfig record + RepoConfigLoader stub to WorkflowModels.cs; namespace FileCheckpointStore.StatePath to {owner}_{repo}_{workflowId}.json; 7 RED/GREEN test stubs (MULTI-01, MULTI-03) [Wave 1]
- [ ] 16-02-PLAN.md — Implement RepoConfigLoader.Load() (GSD_REPOS JSON + legacy fallback); remove IConfiguration from IdleState; multi-repo watch loop in Program.cs; all 30 tests GREEN (MULTI-01, MULTI-02, MULTI-03, MULTI-04) [Wave 2]

**Success Criteria:**
- `GSD_REPOS` JSON array replaces single owner/repo env vars (backwards compatible)
- `--watch` processes all configured repos in sequence with delay
- Checkpoints scoped per repo — no cross-contamination
- Rate limit delay configurable

**Depends on:** Phase 12 (logging), Phase 13 (triage needed at scale)
**Estimated effort:** Medium

---

## Coverage Check (Milestone 3.0)

| Requirement | Phase |
|-------------|-------|
| ROB-01–03 | 12 |
| TRIAGE-01–04 | 13 |
| TESTGEN-01–03 | 14 |
| REV-01–03 | 15 |
| MULTI-01–04 | 16 |

**13 v3 requirements across 5 phases**
