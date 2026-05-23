---
phase: 3
slug: gsd-orchestrator-wiki-release
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-23
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | gh CLI + git commands (no test runner — documentation phase) |
| **Config file** | none |
| **Quick run command** | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/git/trees/HEAD` |
| **Full suite command** | `gh release view v1.0.0 --repo Coding-Autopilot-System/gsd-orchestrator` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run quick verification (gh api or git ls-remote)
- **After every plan wave:** Check all wiki pages exist and release is live
- **Before `/gsd-verify-work`:** Full manual review of all 4 wiki pages + release page
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 3-00-01 | 00 | 0 | GSD-04–07 | — | wiki init creates .wiki.git | manual | `git ls-remote https://github.com/Coding-Autopilot-System/gsd-orchestrator.wiki.git HEAD` | ✅ / ❌ W0 | ⬜ pending |
| 3-01-01 | 01 | 1 | GSD-04 | — | Home page content pushed | automated | `git -C /tmp/wiki-clone ls-files \| grep -q Home` | ✅ / ❌ W0 | ⬜ pending |
| 3-02-01 | 02 | 1 | GSD-05 | — | Setup Guide accurate | manual | read cloned Setup-Guide.md, verify each step | ❌ W0 | ⬜ pending |
| 3-03-01 | 03 | 1 | GSD-06 | — | Architecture page has diagrams | automated | `grep -q 'stateDiagram' /tmp/wiki-clone/Architecture.md` | ❌ W0 | ⬜ pending |
| 3-04-01 | 04 | 1 | GSD-07 | — | Config Reference covers all 7 env vars | automated | `grep -c 'GITHUB_PERSONAL_ACCESS_TOKEN\|ANTHROPIC_API_KEY\|GSD_GITHUB_OWNER\|GSD_GITHUB_REPO\|GSD_REVIEWERS\|GSD_AUTO_MERGE\|GSD_MCP_BINARY' /tmp/wiki-clone/Configuration-Reference.md` | ❌ W0 | ⬜ pending |
| 3-05-01 | 05 | 2 | GSD-08 | — | Release v1.0.0 published | automated | `gh release view v1.0.0 --repo Coding-Autopilot-System/gsd-orchestrator` | ✅ / ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Manual step: Create initial wiki page via GitHub web UI at `https://github.com/Coding-Autopilot-System/gsd-orchestrator/wiki` (creates .wiki.git repo)
- [ ] Verify `git ls-remote https://github.com/Coding-Autopilot-System/gsd-orchestrator.wiki.git HEAD` returns a commit SHA

*This is the critical Wave 0 blocker — no automated wiki push is possible until .wiki.git exists.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Setup Guide accuracy | GSD-05 | Must verify .env steps and `dotnet run` produce expected output | Follow guide on a clean checkout, verify terminal output matches "What a successful run looks like" section |
| Wiki renders Mermaid | GSD-06 | GitHub wiki Mermaid render can only be verified in browser | Open Architecture page in browser, confirm diagrams render |
| Release appears on repo page | GSD-08 | GitHub UI verification | Visit https://github.com/Coding-Autopilot-System/gsd-orchestrator/releases |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
