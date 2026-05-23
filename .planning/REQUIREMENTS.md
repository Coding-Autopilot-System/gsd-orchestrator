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
- [ ] **GSD-04**: GitHub Wiki — Home page with overview and navigation
- [ ] **GSD-05**: GitHub Wiki — Setup Guide (prerequisites, clone, .env, first run)
- [ ] **GSD-06**: GitHub Wiki — Architecture deep-dive (state machine, components, data flow)
- [ ] **GSD-07**: GitHub Wiki — Configuration Reference (all env vars)
- [ ] **GSD-08**: GitHub Release v1.0.0 with changelog
- [x] **GSD-09**: README badges: CI, .NET 10, License — Phase 2 ✓

### Promptimprover (PI)

- [ ] **PI-01**: README rewritten — remove internal language, add hero line, architecture section
- [ ] **PI-02**: GitHub Actions CI workflow (TypeScript/Node build) with passing badge
- [ ] **PI-03**: GitHub Wiki — Home, Setup Guide, Architecture, Configuration Reference
- [ ] **PI-04**: README badges: CI, Node, License
- [ ] **PI-05**: Cross-repo links to org and sibling projects

### autogen (AG)

- [ ] **AG-01**: README rewritten — remove "starter kit" framing, add enterprise positioning
- [ ] **AG-02**: GitHub Actions CI workflow (Python build) with passing badge
- [ ] **AG-03**: GitHub Wiki — Home, Setup Guide, Architecture, Configuration Reference
- [ ] **AG-04**: README badges: CI, Python, License
- [ ] **AG-05**: Cross-repo links to org and sibling projects

### Portfolio Coherence (COH)

- [ ] **COH-01**: Personal OgeonX-Ai profile README linking to Coding-Autopilot-System org
- [ ] **COH-02**: All three repo READMEs include "Part of Coding-Autopilot-System" badge/link
- [ ] **COH-03**: Org profile updated with system interaction diagram showing all three projects

## v2 Requirements (Deferred)

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
