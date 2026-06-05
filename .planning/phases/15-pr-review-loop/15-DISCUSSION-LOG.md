# Phase 15: PR Review Loop - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-05
**Phase:** 15-pr-review-loop
**Areas discussed:** State machine entry, Inline comment format

---

## State machine entry

| Option | Description | Selected |
|--------|-------------|----------|
| New RunPrReviewAsync() method | New method on GsdStateMachine. Program.cs calls it directly when --pr detected. Keeps state machine interface consistent with RunAsync/ResumeAsync. | ✓ |
| PrReviewModeOnly flag on context | Add bool PrReviewModeOnly to GsdWorkflowContext, reuse RunAsync, IdleState short-circuits. Overloads IdleState. | |

**User's choice:** New RunPrReviewAsync() method

---

| Option | Description | Selected |
|--------|-------------|----------|
| New WorkflowState.PrReviewing | New enum value and new PrReviewingState.cs. One state, one responsibility. | ✓ |
| WorkflowState.Reviewing | Reuse existing Reviewing enum. ReviewingState branches on flag. Mixes concerns. | |

**User's choice:** New WorkflowState.PrReviewing

---

| Option | Description | Selected |
|--------|-------------|----------|
| No checkpointing | PR review is fast/stateless. Re-run is recovery. Simpler. | ✓ |
| Yes, checkpoint like --issue | Adds --resume support for PR review. More resilient but complex. | |

**User's choice:** No checkpointing

---

## Inline comment format

| Option | Description | Selected |
|--------|-------------|----------|
| True inline: path+line per finding | Claude reads diff, produces [{file, line, severity, message}]. Each finding posted inline via pull_request_review_write anchored to file+line. | ✓ |
| Review body summary | All findings in one review body block. Simpler, one MCP call. | |
| Hybrid | Body summary + inline where line is known. | |

**User's choice:** True inline: path+line per finding

---

| Option | Description | Selected |
|--------|-------------|----------|
| 3-level: blocking/warning/info | Consistent with ValidationStatus vocabulary. Blocking → REQUEST_CHANGES. | ✓ |
| 4-level: critical/major/minor/suggestion | More granular, new vocabulary. | |
| 2-level: blocking/non-blocking | Simplest, unambiguous. | |

**User's choice:** 3-level: blocking/warning/info

---

| Option | Description | Selected |
|--------|-------------|----------|
| COMMENT only — never autonomous APPROVE | REQUEST_CHANGES if blocking findings; COMMENT otherwise. Bot never approves. | ✓ |
| APPROVE when no blocking findings | Full autonomous approval when Claude finds no blockers. | |

**User's choice:** COMMENT only — never autonomous APPROVE

---

## Claude's Discretion

- Exact MCP tool parameter names for `pull_request_read` — verify at runtime
- Whether `pull_request_review_write` supports batched inline comments (preferred if available)
- LLM prompt phrasing for the code review
- `PrintResult` branch for `WorkflowState.PrReviewing`

## Deferred Ideas

- Auto-approve when zero findings — rejected by design; never autonomous APPROVE
- `--watch` mode for PR review polling — future milestone
- Re-review idempotency (clear old bot comments before reposting) — future phase
