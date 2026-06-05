---
phase: 7
phase-slug: emergency-ci-autopilot-fix
date: 2026-05-26
---

# Validation Strategy — Phase 7

## Framework

| Property | Value |
|----------|-------|
| Framework | None (portfolio docs + GitHub API operations; manual verification) |
| Quick run | `gh api repos/Coding-Autopilot-System/ci-autopilot --jq '.open_issues_count'` |
| Full suite | Per-requirement verification commands below |

## Requirement Verification Map

| Req ID | Behavior | Verification Command | Expected |
|--------|----------|---------------------|----------|
| CIAP-01 | runner-health.yml has no schedule trigger | `gh api repos/Coding-Autopilot-System/ci-autopilot/contents/.github/workflows/runner-health.yml --jq '.content' \| base64 -d \| grep -c "schedule"` | 0 |
| CIAP-02 | Zero open runner-offline issues | `gh api repos/Coding-Autopilot-System/ci-autopilot --jq '.open_issues_count'` | ~0 |
| CIAP-03a | CI workflow exists and passes | GitHub Actions tab: ci.yml on main branch shows green | green badge |
| CIAP-03b | README has hero line | `gh api repos/Coding-Autopilot-System/ci-autopilot/contents/README.md --jq '.content' \| base64 -d \| head -3` | "AI-powered CI autopilot" in first 3 lines |
| CIAP-03c | README has ecosystem cross-link | `grep -c 'Coding-Autopilot-System ecosystem' README.md` (from decoded content) | 1 |
| CIAP-03d | 8 GitHub topics set | `gh api repos/Coding-Autopilot-System/ci-autopilot --jq '.topics \| length'` | 8 |
| CIAP-03e | Wiki has 4 pages | Visit https://github.com/Coding-Autopilot-System/ci-autopilot/wiki | Home, Setup Guide, Architecture, Configuration Reference visible |

## Validation Dimensions

All verification is API-based or visual inspection — no automated test suite exists for this phase (portfolio documentation operations). Each plan's `<verify>` and `<acceptance_criteria>` blocks embed the specific commands per task.
