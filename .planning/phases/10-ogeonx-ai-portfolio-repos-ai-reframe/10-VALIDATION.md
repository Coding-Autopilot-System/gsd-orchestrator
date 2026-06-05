---
phase: 10
slug: ogeonx-ai-portfolio-repos-ai-reframe
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-28
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | GitHub API verification (mcp__github__get_file_contents + gh CLI) |
| **Config file** | none — documentation-only phase |
| **Quick run command** | `gh api repos/OgeonX-Ai/kim-ai-voice-demo/contents/README.md \| base64 -d \| head -5` |
| **Full suite command** | Acceptance criteria grep checks per task (see Per-Task map below) |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Verify remote file via `mcp__github__get_file_contents` or `gh api`
- **After every plan wave:** Check all 4 wiki pages exist + README acceptance criteria pass
- **Before `/gsd-verify-work`:** Full remote state check against must_haves
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 10-01-T1 | 01 | 1 | PORT-01 | — | N/A | manual-api | `gh api repos/OgeonX-Ai/kim-ai-voice-demo/contents/README.md \| python3 -c "import sys,json,base64; print(base64.b64decode(json.load(sys.stdin)['content']).decode()[:200])"` | ✅ | ⬜ pending |
| 10-01-T2 | 01 | 1 | PORT-01 | — | N/A | manual-api | `git ls-remote https://github.com/OgeonX-Ai/kim-ai-voice-demo.wiki.git` | ✅ W0 | ⬜ pending |
| 10-02-T1 | 02 | 1 | PORT-02 | — | N/A | manual-api | `gh api repos/OgeonX-Ai/My-CV/contents/README.md \| python3 -c "import sys,json,base64; print(base64.b64decode(json.load(sys.stdin)['content']).decode()[:200])"` | ✅ | ⬜ pending |
| 10-02-T2 | 02 | 1 | PORT-02 | — | N/A | manual-api | `git ls-remote https://github.com/OgeonX-Ai/My-CV.wiki.git` | ✅ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `OgeonX-Ai/kim-ai-voice-demo.wiki.git` initialized via GitHub web UI
- [ ] `OgeonX-Ai/My-CV.wiki.git` initialized via GitHub web UI
- [ ] Both `git ls-remote` calls return SHAs (not "Repository not found")

*Required before Task 2 in each plan (wiki push tasks).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| kim-ai-voice-demo wiki pages visible in GitHub UI | PORT-01 | GitHub wiki rendering requires browser | Open https://github.com/OgeonX-Ai/kim-ai-voice-demo/wiki and verify 4 pages |
| My-CV wiki pages visible in GitHub UI | PORT-02 | GitHub wiki rendering requires browser | Open https://github.com/OgeonX-Ai/My-CV/wiki and verify 4 pages |
| CI badge renders green on README | PORT-01, PORT-02 | Badge rendering requires browser | View README on GitHub and confirm badge is green |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
