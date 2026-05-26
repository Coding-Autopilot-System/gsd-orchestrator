# Requirements — Enterprise GitHub Portfolio

## v1 Requirements

### Foundation (FOUND)

- [ ] **FOUND-01**: All three repos have correct GitHub topics (5-10 per repo)
- [ ] **FOUND-02**: Org `.github` profile README showcases all three projects with system diagram
- [ ] **FOUND-03**: All three repos have LICENSE (MIT) file
- [ ] **FOUND-04**: ci-autopilot excluded from org featured/pinned repos
- [ ] **FOUND-05**: All repos have concise, accurate GitHub description (< 100 chars)

### gsd-orchestrator (GSD)

- [x] **GSD-01**: GitHub Actions CI workflow (.NET 10 build) with passing badge in README — Phase 2 ✓
- [x] **GSD-02**: Mermaid state machine diagram in README (full workflow: Idle→Done) — Phase 2 ✓
- [x] **GSD-03**: Mermaid component diagram in README (orchestrator ↔ MCP server ↔ Claude) — Phase 2 ✓
- [x] **GSD-04**: GitHub Wiki — Home page with overview and navigation — Phase 3 (03-01) ✓
- [x] **GSD-05**: GitHub Wiki — Setup Guide (prerequisites, clone, .env, first run) — Phase 3 (03-01) ✓
- [x] **GSD-06**: GitHub Wiki — Architecture deep-dive (state machine, components, data flow) — Phase 3 (03-01) ✓
- [x] **GSD-07**: GitHub Wiki — Configuration Reference (all env vars) — Phase 3 (03-01) ✓
- [x] **GSD-08**: GitHub Release v1.0.0 with feature-narrative release notes — Phase 3 (03-02) ✓
- [x] **GSD-09**: README badges: CI, .NET 10, License — Phase 2 ✓

### Promptimprover (PI)

- [x] **PI-01**: README rewritten — remove internal language, add hero line, architecture section — Phase 4 ✓
- [x] **PI-02**: GitHub Actions CI workflow (TypeScript/Node build) with passing badge — Phase 4 ✓
- [x] **PI-03**: GitHub Wiki — Home, Setup Guide, Architecture, Configuration Reference — Phase 4 ✓
- [x] **PI-04**: README badges: CI, Node, License — Phase 4 ✓
- [x] **PI-05**: Cross-repo links to org and sibling projects — Phase 4 ✓

### autogen (AG)

- [x] **AG-01**: README rewritten — remove "starter kit" framing, add enterprise positioning — Phase 5 ✓
- [x] **AG-02**: GitHub Actions CI workflow (Python build) with passing badge — Phase 5 ✓
- [x] **AG-03**: GitHub Wiki — Home, Setup Guide, Architecture, Configuration Reference — Phase 5 ✓
- [x] **AG-04**: README badges: CI, Python, License — Phase 5 ✓
- [x] **AG-05**: Cross-repo links to org and sibling projects — Phase 5 ✓

### Portfolio Coherence (COH)

- [ ] **COH-01**: Personal OgeonX-Ai profile README linking to Coding-Autopilot-System org
- [ ] **COH-02**: All three repo READMEs include "Part of Coding-Autopilot-System" badge/link
- [ ] **COH-03**: Org profile updated with system interaction diagram showing all three projects

## v2 Requirements — Milestone 2.0 (Full Org Documentation)

### ci-autopilot Emergency Fix (CIAP)

- [ ] **CIAP-01**: Disable/fix runner-health.yml runaway cron (currently `*/15 * * * *` checking offline self-hosted runner)
- [ ] **CIAP-02**: Bulk-close all 1,964+ open `runner-offline` issues via GitHub API
- [ ] **CIAP-03**: ci-autopilot Level A docs — README rewrite (AI agent automation framing), CI badge, wiki 4 pages, GitHub topics, cross-links to org

### CAS Secondary Repos (ACOR)

- [ ] **ACOR-01**: autopilot-core Level A docs — README rewrite, CI badge, wiki 4 pages, topics, cross-links
- [ ] **ACOR-02**: autopilot-demo Level A docs — README rewrite, CI badge, wiki 4 pages, topics, cross-links
- [ ] **CSEC-01**: cloud-security-service-model documentation — README rewrite (framework/methodology framing), wiki 4 pages, topics

### OgeonX-Ai Core Tech (TECH)

- [ ] **TECH-01**: enterprise-ai-gateway AI engineer reframe — README hero line, architecture diagram, wiki 4 pages, CI badge, cross-links to CAS
- [ ] **TECH-02**: android AI engineer reframe — scan codebase, README (Android + AI integration framing), wiki 4 pages, CI badge

### OgeonX-Ai Portfolio Repos (PORT)

- [ ] **PORT-01**: kim-ai-voice-demo AI engineer reframe — README rewrite (away from ElevenLabs demo framing), wiki 4 pages, topics
- [ ] **PORT-02**: My-CV reframe — README as AI-powered career tool, wiki 4 pages, topics

### Cross-Portfolio Coherence (COHER)

- [ ] **COHER-01**: GitHub topics audit — all repos have 5-10 accurate, discoverable topics
- [ ] **COHER-02**: Org pinned repos — Coding-Autopilot-System pins the 3 flagship repos (gsd-orchestrator, Promptimprover, autogen), ci-autopilot excluded
- [ ] **COHER-03**: Issue templates — standardize `bug_report.md` and `feature_request.md` across CAS repos

## v1 Deferred (still out of scope for v2)

- Test suites for gsd-orchestrator, Promptimprover, autogen
- GitHub Projects board showing roadmap
- Dependabot configuration
- CONTRIBUTING.md and CODE_OF_CONDUCT.md
- GitHub Pages site for the org

## Out of Scope

- Cloud deployment of any service — portfolio polish only, not infra
- New features in existing projects — additive docs/CI only
- Video demos or GIF screenshots — static docs only for now

## Traceability

| REQ | Phase |
|-----|-------|
| FOUND-01–05 | Phase 1 |
| GSD-01–03, GSD-09 | Phase 2 |
| GSD-04–08 | Phase 3 |
| PI-01–05 | Phase 4 |
| AG-01–05 | Phase 5 |
| COH-01–03 | Phase 6 |
| CIAP-01–03 | Phase 7 |
| ACOR-01–02, CSEC-01 | Phase 8 |
| TECH-01–02 | Phase 9 |
| PORT-01–02 | Phase 10 |
| COHER-01–03 | Phase 11 |
