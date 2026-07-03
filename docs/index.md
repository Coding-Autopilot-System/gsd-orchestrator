# GSD Orchestrator

**GSD Orchestrator** is a highly resilient, state-driven workflow engine built on .NET 10. It is designed to autonomously translate GitHub Issues into tested, validated, and documented Pull Requests via an AI-native Control Plane (the Coding Autopilot System).

## Key Capabilities

1. **State Machine Durability:** The entire Issue-to-PR pipeline is governed by an explicit state machine, ensuring tasks can be interrupted and resumed without losing context.
2. **AI Verification Loops:** Uses explicit `SdlcBatch` and `SdlcPhaseStatus` checkpoints to ensure code is validated, tested, and reviewed by autonomous personas before merging.
3. **Multi-Agent Orchestration:** Delegates specific tasks to specialized AI Personas (e.g., `sdet-expert`, `python-expert`, `azure-architect`) using the `GLOBAL_AGENTS.md` router framework.

## Project Structure

- `src/GsdOrchestrator/Workflows`: Contains the core `.NET 10` State Machine and all transition handlers.
- `src/GsdOrchestrator/Mcp`: The Model Context Protocol (MCP) dispatchers that interact with the local filesystem and GitHub API.
- `src/GsdOrchestrator/Checkpointing`: State serialization logic for workflow durability.

To dive deeper into how the states are processed, see the [Architecture Guide](architecture.md).
