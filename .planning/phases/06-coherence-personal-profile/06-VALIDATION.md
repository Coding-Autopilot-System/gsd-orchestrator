---
phase: 6
slug: coherence-personal-profile
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-24
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Manual verification via `gh api` + `base64 -d` (documentation-only phase) |
| **Config file** | None — content verification via GitHub API |
| **Quick run command** | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/contents/README.md --jq '.content' \| base64 -d \| grep 'Coding-Autopilot-System ecosystem'` |
| **Full suite command** | See Per-Task Verification Map below |
| **Estimated runtime** | ~30 seconds (API calls only) |

---

## Sampling Rate

- **After every task commit:** Run quick run command for that task's target file
- **After every plan wave:** Run full suite verification map
- **Before `/gsd-verify-work`:** All manual checks must be complete
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 6-01-01 | 01 | 1 | COH-02 | — | N/A | automated | `gh api repos/Coding-Autopilot-System/gsd-orchestrator/contents/README.md --jq '.content' \| base64 -d \| grep 'Coding-Autopilot-System ecosystem'` | ✅ | ⬜ pending |
| 6-02-01 | 02 | 2 | COH-01 | — | N/A | automated | `gh api repos/OgeonX-Ai/OgeonX-Ai/contents/README.md --jq '.content' \| base64 -d \| grep 'Coding-Autopilot-System'` | ✅ | ⬜ pending |
| 6-02-02 | 02 | 2 | COH-03 | — | N/A | automated | `gh api repos/Coding-Autopilot-System/.github/contents/profile/README.md --jq '.content' \| base64 -d \| grep -c 'autogen\|gsd-orchestrator\|Promptimprover'` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

None — no test files need to be created. This is a documentation-only phase. All verification is via `gh api` content inspection.

*Existing infrastructure covers all phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Profile README renders on OgeonX-Ai GitHub profile page | COH-01 | Visual rendering requires browser | Visit `https://github.com/OgeonX-Ai` — profile README section should appear with org link |
| Org profile renders at Coding-Autopilot-System org page | COH-03 | Visual rendering requires browser | Visit `https://github.com/Coding-Autopilot-System` — org profile README should appear with updated diagram |
| Ecosystem line position in gsd-orchestrator README | COH-02 | Position after badge block requires visual inspection | Inspect full README — ecosystem line must appear after the MIT License badge line, before the first `---` divider |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-05-24
