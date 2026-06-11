# GSD Orchestrator Portfolio Proof

This repository is a hiring-facing proof point for autonomous engineering workflow design.

## What It Demonstrates

- A .NET 10 state machine driving issue-to-PR automation
- MCP-mediated GitHub operations instead of scattered direct API logic
- Checkpointed workflow durability and resume behavior
- Guardrailed validation before pull request creation
- A local-first execution model that can later be bundled into a workstation or hosted control plane

## Why It Is Credible

- The workflow is decomposed into explicit states rather than hidden inside one prompt loop.
- GitHub operations, AI reasoning, and validation gates are separated by responsibility.
- The local AI CLI path and the orchestrator path are distinct and inspectable.
- The repo exposes the exact code paths where planning, editing, validation, and PR creation happen.

## Fast Review Path

1. `src/GsdOrchestrator/Workflows/GsdStateMachine.cs`
2. `src/GsdOrchestrator/Workflows/States`
3. `src/GsdOrchestrator/Mcp`
4. `install-autostart.ps1` and `start-mcp-server.ps1`
