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
- [x] 03-00-PLAN.md — Manual checkpoint: initialize wiki.git via GitHub web UI (GSD-04, GSD-05, GSD-06, GSD-07 blocker)
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

**Plans:**
1. Rewrite README — remove emoji overload, add hero line, architecture section, proper badges
2. Create `.github/workflows/ci.yml` — Node 22 / TypeScript build
3. Create Wiki (4 pages: Home, Setup, Architecture, Config Reference)
4. Add cross-repo links (org badge, sibling project links)

**Success Criteria:**
- README no longer uses "starter kit" or internal language
- CI badge is green
- Wiki has 4 pages
- README links to gsd-orchestrator and autogen

**Depends on:** Phase 1
**Estimated effort:** Medium

---

## Phase 5 — autogen Polish

**Goal:** autogen has an enterprise README, passing CI, and Wiki.

**Requirements:** AG-01, AG-02, AG-03, AG-04, AG-05

**Plans:**
1. Rewrite README — remove "starter kit" framing, add proper architecture description
2. Create `.github/workflows/ci.yml` — Python 3.12 with pip install
3. Create Wiki (4 pages: Home, Setup, Architecture, Config Reference)
4. Add cross-repo links (org badge, sibling project links)

**Success Criteria:**
- README positions autogen as "enterprise multi-agent system" not "starter"
- CI badge is green
- Wiki has 4 pages
- README links to gsd-orchestrator and Promptimprover

**Depends on:** Phase 1
**Estimated effort:** Medium

---

## Phase 6 — Coherence & Personal Profile

**Goal:** All repos tell one story. Personal profile connects identity to the work.

**Requirements:** COH-01, COH-02, COH-03

**Plans:**
1. Create OgeonX-Ai personal profile README (GitHub profile repo)
2. Add "Part of Coding-Autopilot-System" badge/link to all three READMEs
3. Update org profile README with final system diagram linking all three repos

**Success Criteria:**
- OgeonX-Ai GitHub profile shows portfolio work
- Any repo README leads to the org and the other two repos
- Org profile README is the definitive landing page for the portfolio

**Depends on:** Phases 2-5 (all repos polished before linking them)
**Estimated effort:** Small

---

## Coverage Check

| Requirement | Phase |
|-------------|-------|
| FOUND-01–05 | 1 |
| GSD-01, 02, 03, 09 | 2 |
| GSD-04, 05, 06, 07, 08 | 3 |
| PI-01–05 | 4 |
| AG-01–05 | 5 |
| COH-01–03 | 6 |

**All 23 requirements covered across 6 phases ✓**
