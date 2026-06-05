---
phase: 5
slug: autogen-polish
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-24
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | pytest (running stdlib unittest.TestCase tests) |
| **Config file** | None — no pytest.ini, pyproject.toml, or conftest.py |
| **Quick run command** | `python -m pytest tests/test_phase5_ui_contract.py tests/test_phase5_operator_views.py -v` |
| **Full suite command** | Same — only these two test files are CI-runnable without framework pip packages |
| **Install required** | `pip install pytest` |
| **Estimated runtime** | ~5 seconds |

> **Why only 2 test files:** All other tests import `agent_framework`, `autogen_starter.*` (removed from repo), or `fastapi`/`pydantic` — none declared in any requirements file. Full `discover` would fail with `ModuleNotFoundError`. These two files use only stdlib and verify real behavior.

---

## Sampling Rate

- **After every task commit:** Run quick command above (locally or check CI run)
- **After every plan wave:** Run full suite + check CI badge status
- **Before `/gsd-verify-work`:** Full suite green + CI badge green + wiki pages accessible
- **Max feedback latency:** ~30 seconds (CI run time + badge update)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Automated Command | Manual Check | Status |
|---------|------|------|-------------|-------------------|--------------|--------|
| 05-00-01 | 00 | 0 | AG-03 blocker | — | Navigate to https://github.com/Coding-Autopilot-System/autogen/wiki and create first page | ⬜ pending |
| 05-01-01 | 01 | 1 | AG-02 | `gh run list -R Coding-Autopilot-System/autogen --limit 1` | Check CI badge on main branch | ⬜ pending |
| 05-01-02 | 01 | 1 | AG-01, AG-04, AG-05 | `gh api repos/Coding-Autopilot-System/autogen/contents/README.md --jq '.content' \| base64 -d` | Verify hero line, no "starter kit", badges render, cross-repo links present | ⬜ pending |
| 05-02-01 | 02 | 2 | AG-03 | `git ls-remote https://github.com/Coding-Autopilot-System/autogen.wiki.git` | Verify 4 pages at https://github.com/Coding-Autopilot-System/autogen/wiki | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `https://github.com/Coding-Autopilot-System/autogen/wiki` — human must click "Create the first page" and save any stub to initialize wiki.git before Wave 2 automation can push pages

*Existing pytest infrastructure: `pip install pytest` in CI is the only install step needed — no requirements.txt exists in the repo.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| README hero line present, no "starter kit" language | AG-01 | Content review — no automated string assertion in PLAN | `gh api repos/Coding-Autopilot-System/autogen/contents/README.md --jq '.content' \| base64 -d \| head -20` |
| Badges render in README on GitHub | AG-04 | Badge rendering requires browser render | Navigate to https://github.com/Coding-Autopilot-System/autogen and verify badges load |
| Wiki pages have substantive content | AG-03 | Content quality is subjective | Navigate to each wiki page URL |
| CI badge shows green on main | AG-02 | Badge update has CDN lag | Navigate to repo and check badge after first CI run completes |
| Cross-repo links work | AG-05 | Link resolution requires browser | Click each link in README |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers wiki initialization dependency
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s (CI run + badge propagation)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
