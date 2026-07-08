# Decisions

## ADR convention

`docs/adr/README.md` establishes the convention (sequential numbering, Context/Decision/
Consequences, mandatory for major technical decisions or new dependencies) but **no numbered
ADR files exist in the repo yet** — decisions to date live in phase plans/summaries under
`.planning/phases/` instead. This index points at those until the first formal ADR is filed.

## Phase history (`.planning/phases/`, this repo's own GSD project)

19 phases completed in this repo's local planning history, covering foundation setup through
portfolio polish:

| Phase | Topic |
|---|---|
| 01 | Foundation quick wins |
| 02 | CI diagrams |
| 03 | Wiki release |
| 04–05 | Promptimprover / autogen cross-repo polish |
| 06 | Coherence / personal profile |
| 07 | Emergency ci-autopilot fix |
| 08 | Secondary repos level A |
| 09–11 | Portfolio AI-reframe and cross-portfolio coherence |
| 12 | Robustness foundation |
| 13 | Smarter issue triage |
| 14 | Autonomous test generation |
| 15 | PR review loop |
| 16 | Multi-repo support |
| 17 | CI hardening |
| 18 | State/test coverage |
| 19 | Portfolio polish |

See each `.planning/phases/<NN-topic>/*-SUMMARY.md` for the detailed record of what shipped.

## Open decisions tracked in this Phase 36 refresh

- **PR #16** (`feat/phase-26-coverage-gates`) — ratchets CI to a `branch-rate` coverage gate;
  open, not yet merged.
- **PR #17** (`fix/checkpoint-corruption`) — checkpoint corruption/replay-error hardening;
  open, not yet merged.
- **PR #18** (`ci/sha-pin-and-least-privilege`) — pins third-party GitHub Actions to commit
  SHAs and adds least-privilege permissions; open, not yet merged.
- **Phase 28-01 typed-failure retry/halt path** — designed and implemented locally
  (`feat/phase-28-fault-injection`, unpushed) but not yet a PR; see
  [Architecture](./Architecture.md) for the current-vs-designed failure-handling contrast.

<!-- docs-verified: a01b130c98cb7833d45cc7406f6002009f33557a 2026-07-08 -->
