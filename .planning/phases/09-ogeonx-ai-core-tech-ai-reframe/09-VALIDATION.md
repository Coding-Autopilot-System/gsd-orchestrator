---
phase: 9
slug: ogeonx-ai-core-tech-ai-reframe
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-27
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | GitHub API verification (no local test runner — docs-only phase) |
| **Config file** | none |
| **Quick run command** | `gh api repos/OgeonX-Ai/{repo} --jq '.description'` |
| **Full suite command** | `gh api repos/OgeonX-Ai/{repo}/contents/README.md` + wiki page reads |
| **Estimated runtime** | ~10 seconds (API calls) |

---

## Sampling Rate

- **After every task commit:** Verify the specific artifact written (README section, wiki page, workflow file)
- **After every plan wave:** Full GitHub API check — README, wiki pages, CI run status, badge URL resolves
- **Before `/gsd-verify-work`:** All 4 wiki pages created, CI green on correct branch, badge renders in README
- **Max feedback latency:** 30 seconds (GitHub API + CI queue)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 9-01-01 | 01 | 1 | TECH-01 | — | N/A | manual | `gh api repos/OgeonX-Ai/enterprise-ai-gateway/contents/README.md` returns 200 | ✅ remote | ⬜ pending |
| 9-01-02 | 01 | 1 | TECH-01 | — | N/A | manual | Wiki pages: `gh api repos/OgeonX-Ai/enterprise-ai-gateway/git/refs` confirms wiki.git exists | ✅ remote | ⬜ pending |
| 9-01-03 | 01 | 1 | TECH-01 | — | N/A | manual | `gh api repos/OgeonX-Ai/enterprise-ai-gateway/actions/workflows/ci.yml/badge.svg` returns badge | ✅ remote | ⬜ pending |
| 9-02-00 | 02 | 0 | TECH-02 | — | N/A | manual | `gh api repos/OgeonX-Ai/android` confirms wiki initialized | ✅ remote | ⬜ pending |
| 9-02-01 | 02 | 1 | TECH-02 | — | N/A | manual | `gh api repos/OgeonX-Ai/android/contents/README.md` returns 200 with AI framing | ✅ remote | ⬜ pending |
| 9-02-02 | 02 | 1 | TECH-02 | — | N/A | manual | Wiki pages: Home, Setup-Guide, Architecture, Configuration-Reference all readable via wiki.git | ✅ remote | ⬜ pending |
| 9-02-03 | 02 | 1 | TECH-02 | — | N/A | manual | `gh api repos/OgeonX-Ai/android/actions/workflows/ci.yml/badge.svg?branch=master` returns badge | ✅ remote | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `OgeonX-Ai/android` wiki initialized — wiki.git remote must be initialized before wiki pages can be pushed (GitHub requires a manual step to create wiki.git; executor uses `gh api --method POST repos/OgeonX-Ai/android` or creates a stub page via the API to bootstrap the wiki remote)

*Note: enterprise-ai-gateway has no Wave 0 requirements — its wiki.git is already initialized.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| README hero line accurately reflects code (not hallucinated) | TECH-01, TECH-02 | Requires human judgment to assess accuracy vs. codebase | Read README hero line, compare to scanned source files in RESEARCH.md |
| CAS ecosystem cross-link badge renders in GitHub README | TECH-01, TECH-02 | shields.io badge rendering requires browser | Open GitHub repo page, verify badge displays |
| OgeonX-Ai intra-linking ("See also") present in both READMEs | TECH-01 ↔ TECH-02 | Cross-repo link correctness | Open each README, verify "See also" links to the sibling repo |
| CI badge on `master` branch for android (not `main`) | TECH-02 | Badge URL branch param must be `?branch=master` | Verify badge SVG URL includes `branch=master` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers android wiki initialization
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
