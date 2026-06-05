---
phase: 4
slug: promptimprover-polish
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-23
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.4 (universal-refiner/) |
| **Config file** | none — Vitest auto-discovers `tests/` directory |
| **Quick run command** | `cd universal-refiner && npm test` |
| **Full suite command** | `cd universal-refiner && npm test` |
| **Estimated runtime** | ~10 seconds (14 test files) |

---

## Sampling Rate

- **After every task commit:** Run `cd universal-refiner && npm test`
- **After every plan wave:** Run `cd universal-refiner && npm test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 4-00-01 | 00 | 0 | PI-03 | — | N/A | manual | `git ls-remote https://github.com/Coding-Autopilot-System/Promptimprover.wiki.git` | ❌ W0 | ⬜ pending |
| 4-01-01 | 01 | 1 | PI-02 | — | N/A | smoke | `gh run list -R Coding-Autopilot-System/Promptimprover --limit 1` | ❌ W0 | ⬜ pending |
| 4-02-01 | 02 | 1 | PI-01, PI-04, PI-05 | — | N/A | manual | `gh api repos/Coding-Autopilot-System/Promptimprover/contents/README.md --jq .content` | ❌ W0 | ⬜ pending |
| 4-03-01 | 03 | 2 | PI-03 | — | N/A | manual | `git ls-remote https://github.com/Coding-Autopilot-System/Promptimprover.wiki.git` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Manual: Initialize wiki via GitHub web UI at `https://github.com/Coding-Autopilot-System/Promptimprover/wiki` — creates `Promptimprover.wiki.git`
- [ ] Confirm `universal-refiner/package.json` `test` script runs with `npm test` (no env vars required)

*Wave 0 is a manual human action; no automated test stubs required for this documentation phase.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| README hero line, no emoji, Mermaid diagram renders | PI-01 | Visual/rendered content check | Browse `https://github.com/Coding-Autopilot-System/Promptimprover` after push |
| CI badge shows green | PI-02 | Badge requires first CI run to complete | Check README badge after first push triggers CI |
| Wiki 4 pages exist with correct content | PI-03 | Wiki content is human-readable prose | Browse `https://github.com/Coding-Autopilot-System/Promptimprover/wiki` |
| Badges render (CI, Node 22, MIT) | PI-04 | shields.io badge rendering | Browse README after push; check badge URLs resolve |
| Cross-repo links work | PI-05 | Link correctness requires navigation test | Click org badge and sibling links in rendered README |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
