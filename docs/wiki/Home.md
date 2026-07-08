# gsd-orchestrator Wiki

## Role in the CAS portfolio

`gsd-orchestrator` is the **Control plane** of the Coding-Autopilot-System three-plane model
(Control / Execution / Governance — see the root [`docs/VISION.md`](https://github.com/OgeonX-Ai/cas-workstation)
architecture diagram). Its job: admit goals as typed, bounded, budgeted work items and drive
dependency-aware scheduling through a durable state machine — issue in, PR out, with every
step checkpointed so a crashed or interrupted run can be resumed rather than restarted.

| Plane | This repo's responsibility |
|---|---|
| Control | Goal admission, state-machine scheduling, checkpoint/resume, PR lifecycle |
| Execution | *(not this repo — see `autogen`)* |
| Governance | *(not this repo — see `Promptimprover`, `cas-contracts`, `cas-evals`)* |

## Quickstart

- [README.md](../../README.md) — setup, `.env` configuration, running the orchestrator against a real issue
- [docs/portfolio-proof.md](../portfolio-proof.md) — concise reviewer-oriented summary
- [Architecture](./Architecture.md) — state machine, component topology, typed-failure path
- [Operations](./Operations.md) — build, test, and CI commands (verified against the live tree)
- [Decisions](./Decisions.md) — index of phase summaries and the ADR convention

## Ecosystem links

Part of the [Coding-Autopilot-System](https://github.com/Coding-Autopilot-System) org:
[autogen](https://github.com/Coding-Autopilot-System/autogen) (execution plane) ·
[Promptimprover](https://github.com/Coding-Autopilot-System/Promptimprover) (prompt governance) ·
[cas-contracts](https://github.com/Coding-Autopilot-System/cas-contracts) (shared schemas) ·
[cas-evals](https://github.com/Coding-Autopilot-System/cas-evals) (evidence gate)

<!-- docs-verified: a01b130c98cb7833d45cc7406f6002009f33557a 2026-07-08 -->
