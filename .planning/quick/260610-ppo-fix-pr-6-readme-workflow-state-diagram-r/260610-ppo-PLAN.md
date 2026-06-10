---
quick_id: 260610-ppo
status: complete
mode: quick-full
description: Fix PR #6 README workflow state diagram responsibilities to match actual code including Triaging and TestGenerating and render success sample cleanly
---

# Quick Task 260610-ppo Plan

## Goal

Make the README workflow documentation accurately reflect the implemented state machine and render the success sample cleanly.

## Must Haves

- The documented issue workflow includes `Idle`, `Triaging`, and `TestGenerating` in their implemented order.
- The state diagram shows actionable triage continuing to `Analyzing` and non-actionable or triage-only runs exiting to `Done`.
- State responsibilities match the behavior in `src/GsdOrchestrator/Workflows/States`.
- The success sample uses render-safe text matching `Program.PrintResult`.

## Task

### 1. Correct README workflow documentation

**Files:** `README.md`

**Action:** Update the workflow summary, Mermaid state diagram, state responsibilities, and success output sample from the current source implementation.

**Verify:**
- Compare every documented transition with `WorkflowModels.cs` and each state implementation.
- Run `dotnet test GithubMCP.slnx`.
- Run `git diff --check`.

**Done:** README accurately documents the runtime flow, includes `Triaging` and `TestGenerating`, and contains a clean success sample.
