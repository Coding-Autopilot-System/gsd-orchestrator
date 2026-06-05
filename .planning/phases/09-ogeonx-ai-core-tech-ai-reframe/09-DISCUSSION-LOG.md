# Phase 9: OgeonX-Ai Core Tech AI Reframe + Level A - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-27
**Phase:** 9-OgeonX-Ai Core Tech AI Reframe + Level A
**Areas discussed:** enterprise-ai-gateway identity, android framing angle, android CI strategy, cross-link ecosystem story

---

## enterprise-ai-gateway identity

| Option | Description | Selected |
|--------|-------------|----------|
| LLM API proxy/router | Routes requests to multiple AI providers, handles auth/rate limiting/fallback | |
| AI middleware/service layer | Sits between apps and AI backends, prompt transformation, context injection | |
| Enterprise integration gateway | Bridges enterprise systems with AI services | |
| Let the codebase drive it | Executor scans repo, derives framing entirely from what code actually does | ✓ |

**User's choice:** "You research what it does through the codebase"
**Notes:** Same scan-first approach as android (TECH-02 already required a codebase scan). No pre-committed angle.

**Follow-up — framing direction after scan:**

| Option | Description | Selected |
|--------|-------------|----------|
| AI infrastructure angle | Frame as AI engineering infrastructure regardless of what code does | |
| Let the codebase drive it | Executor derives angle entirely from actual code | ✓ |

---

## android framing angle

| Option | Description | Selected |
|--------|-------------|----------|
| Codebase drives it | Executor scans, writes accurate README from what code actually does | |
| Android AI client | Frame as Android client that interfaces with AI backends | |
| AI-powered Android app | Frame as Android app with embedded AI features — leads with AI capabilities | ✓ |

**User's choice:** AI-powered Android app
**Notes:** Even if AI integration is lightweight, lead with AI capabilities as the headline angle.

**Follow-up — wiki page names:**

| Option | Description | Selected |
|--------|-------------|----------|
| Keep standard names | Home, Setup-Guide, Architecture, Configuration-Reference | ✓ |
| App-specific names | e.g., Home, Installation, Architecture, Features | |

---

## android CI strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Lightweight: lint + syntax only | XML/Kotlin syntax check, no APK compilation. Fast, consistent with prior repos | |
| Gradle build, no emulator | Full APK compilation with Gradle, no tests/emulator | |
| Full Android CI | Gradle build + unit tests + emulator for instrumented tests | ✓ (refined below) |

**User's choice:** Full Android CI (then refined)

**Follow-up — definition of "full":**

| Option | Description | Selected |
|--------|-------------|----------|
| Gradle build + unit tests, no emulator | setup-java + gradlew test (JVM unit tests). ~5 min, no emulator. | ✓ |
| Gradle build + instrumented tests (emulator) | Uses emulator runner, 15-30 min, fragile | |

**Final decision:** `gradlew test` (JVM unit tests), setup-java, ubuntu-latest, no emulator.

---

## cross-link ecosystem story

| Option | Description | Selected |
|--------|-------------|----------|
| CAS org badge + ecosystem line | shields.io org badge + "Part of CAS ecosystem: [gsd-orchestrator] \| [Promptimprover] \| [autogen]" | ✓ |
| CAS org link only | Just a link to CAS org, no individual repo links | |

**User's choice:** CAS org badge + ecosystem line (same pattern as Phases 4-5)

**Follow-up — intra-OgeonX-Ai linking:**

| Option | Description | Selected |
|--------|-------------|----------|
| No — link to CAS only | No linking between OgeonX-Ai repos | |
| Yes — link to each other too | enterprise-ai-gateway ↔ android "See also" links | ✓ |

**Final decision:** Both repos link to CAS ecosystem AND to each other via "See also" markdown link.

---

## Claude's Discretion

- enterprise-ai-gateway architecture diagram: executor designs flowchart LR based on scanned codebase structure
- android CI fallback: if no JVM unit tests exist in codebase, fall back to `gradlew assembleDebug` (build only) rather than creating empty test stubs

## Deferred Ideas

None — discussion stayed within phase scope.
