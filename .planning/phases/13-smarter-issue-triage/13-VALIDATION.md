---
phase: 13
slug: smarter-issue-triage
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-01
---

# Phase 13 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + NSubstitute 5.3.0 |
| **Config file** | `src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj` |
| **Quick run command** | `dotnet test src/GsdOrchestrator.Tests/ --no-build -x` |
| **Full suite command** | `dotnet test src/GsdOrchestrator.Tests/` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test src/GsdOrchestrator.Tests/ --no-build -x`
- **After every plan wave:** Run `dotnet test src/GsdOrchestrator.Tests/`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 13-01-01 | 01 | 1 | TRIAGE-01 | — | LLM output parsed strictly; classification used only for branching | unit | `dotnet test src/GsdOrchestrator.Tests/ --filter "FullyQualifiedName~Triaging"` | Wave 0 | ⬜ pending |
| 13-01-02 | 01 | 1 | TRIAGE-01 | T-13-01 | No issue body logged at Info level | unit | same | Wave 0 | ⬜ pending |
| 13-01-03 | 01 | 1 | TRIAGE-02 | — | Duplicate detection via LLM + list_issues | unit | same | Wave 0 | ⬜ pending |
| 13-01-04 | 01 | 1 | TRIAGE-03 | — | TriageModeOnly=true always exits to Done | unit | same | Wave 0 | ⬜ pending |
| 13-01-05 | 01 | 1 | TRIAGE-04 | — | out-of-scope/duplicate → update_issue called | unit | same | Wave 0 | ⬜ pending |
| 13-02-01 | 02 | 1 | TRIAGE-03 | — | --triage CLI flag parsed, requires --issue | `dotnet build src/GsdOrchestrator/` | — | existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `src/GsdOrchestrator.Tests/TriagingStateTests.cs` — stubs for TRIAGE-01 through TRIAGE-04

*Existing test infrastructure from Phase 12-03 covers framework setup — no new csproj or solution wiring needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `update_issue` tool name correct | TRIAGE-04 | MCP tool name not verified from live binary | Run `dotnet run --project src/GsdOrchestrator -- --triage --issue <N>` with a test issue and verify it closes |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
