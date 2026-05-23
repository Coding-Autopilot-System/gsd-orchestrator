# Enterprise GitHub Portfolio

## What This Is

A systematic elevation of the Coding-Autopilot-System GitHub org into an enterprise-grade, job-landing portfolio demonstrating senior AI engineering and .NET architecture skills.

The org currently has three real, working projects:
- **gsd-orchestrator** (C# / .NET 10) — Autonomous GitHub agentic workflow: reads issues, plans code changes via Claude, branches, edits, commits, opens PRs. Enterprise patterns: state machine, DI, Polly resilience, file checkpointing, JSON-RPC MCP stdio client.
- **Promptimprover** (TypeScript) — MCP server middleware for prompt governance: RAG neural snippets, compounding memory, auto-heal middleware, ISO 27001 compliance framing.
- **autogen** (Python) — Microsoft Agent Framework multi-agent automation with Gemini/Claude fallback, AG-UI Command Center, DevUI integration.

These projects are real and impressive. The gap is presentation: no CI badges, no wikis, no architecture diagrams, no org narrative, no topics, no releases, no personal profile. Hiring managers can't see the quality that's already there.

## Core Value

A hiring manager should be able to spend 5 minutes on the GitHub org and immediately understand: this person builds production-grade AI systems at the intersection of .NET, TypeScript/Python, and autonomous agents — and they build them properly.

## Target Audience

- **Primary**: Hiring managers and tech leads at companies building AI-powered developer tooling, enterprise automation, or agentic systems
- **Secondary**: Technical recruiters screening for AI Engineer / Senior .NET / Platform Engineer roles

## Goals

1. Every repo has CI badges, proper README, and GitHub Wiki documentation
2. The org profile tells the story of the three projects as a coherent system
3. gsd-orchestrator has an architecture diagram and v1.0.0 release
4. All repos are discoverable via correct GitHub topics
5. Personal profile (OgeonX-Ai) has a profile README linking to the org

## Tech Stack

| Layer | Tech |
|-------|------|
| Primary language | C# / .NET 10 |
| Secondary | TypeScript, Python |
| CI/CD | GitHub Actions |
| Diagrams | Mermaid (rendered in GitHub) |
| Docs | GitHub Wiki (Markdown) |
| Release management | GitHub Releases + semantic tags |

## Constraints

- Everything must be executable by Claude (no manual commands from user)
- All repos already exist and have code — changes are additive (docs, CI, meta)
- Must not break existing code or workflows
- Enterprise tone throughout — no toy/demo language

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| GitHub Wiki over external docs | Keeps everything in GitHub where hiring managers already are | Decided |
| Mermaid diagrams over image files | Auto-renders in GitHub, version-controlled, no external tools | Decided |
| gsd-orchestrator as crown jewel | Most technically impressive, most complete, best story | Decided |
| Squash merge all portfolio PRs | Clean history for portfolio visibility | Decided |
| Standard granularity | 5-8 phases covers the work without over-slicing | Decided |

## Requirements

### Validated

- ✓ gsd-orchestrator exists with working autonomous workflow — existing
- ✓ Promptimprover exists as MCP server — existing
- ✓ autogen exists as multi-agent framework — existing
- ✓ All repos are public — done this session

### Active

- [ ] All repos have correct GitHub topics for discoverability
- [ ] Org .github profile README showcases all three projects as a system
- [x] gsd-orchestrator has GitHub Actions CI (.NET build) — Validated in Phase 2: CI green, badge live
- [x] gsd-orchestrator has Mermaid architecture diagram in README — Validated in Phase 2: stateDiagram-v2 + flowchart LR
- [ ] gsd-orchestrator has GitHub Wiki (4+ pages)
- [ ] gsd-orchestrator has v1.0.0 release
- [ ] Promptimprover has updated README and GitHub Actions CI
- [ ] Promptimprover has GitHub Wiki
- [ ] autogen has updated README and GitHub Actions CI
- [ ] autogen has GitHub Wiki
- [ ] Personal OgeonX-Ai profile README links to org and highlights top projects
- [x] gsd-orchestrator README has CI / .NET 10 / License badges — Validated in Phase 2
- [ ] Promptimprover and autogen READMEs have CI/version badges

### Out of Scope

- Deploying any service to cloud — focus is documentation and CI, not infra
- Adding new features to existing projects — portfolio polish only
- Test suites — existing projects have no tests; adding them is a separate initiative

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition:**
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active

**After milestone:**
1. Full review of all sections
2. Core Value check — still the right priority?

---
*Last updated: 2026-05-23 — Phase 2 complete (CI, badges, diagrams)*
