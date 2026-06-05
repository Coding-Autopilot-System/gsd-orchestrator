---
phase: 16-multi-repo-support
plan: 02
subsystem: configuration
tags: [csharp, dotnet, xunit, tdd, multi-repo, configuration, checkpointing, security]

# Dependency graph
requires:
  - phase: 16-01
    provides: RepoConfig record + RepoConfigLoader stub (NotImplementedException); FileCheckpointStore.StatePath namespaced overload; 7 RED/GREEN test stubs in MultiRepoConfigTests.cs
  - phase: 15-pr-review-loop
    provides: ReviewingState + ReviewResult models; GsdStateMachine 5-param RunAsync; all 28 prior tests GREEN
provides:
  - RepoConfigLoader.Load() full implementation — parses GSD_REPOS JSON array or falls back to GSD_GITHUB_OWNER+GSD_GITHUB_REPO
  - IdleState with IConfiguration dependency removed — owner/repo read from ctx.Issue!.RepoOwner/RepoName
  - Program.cs multi-repo watch loop — foreach over RepoConfigLoader.Load() result with per-repo rateLimitDelaySeconds
  - FileCheckpointStore.StatePath sanitization — T-16-05 path traversal mitigation
  - All 35 tests GREEN (28 prior + 7 MultiRepoConfigTests)
affects: [any future phase extending watch mode, any phase adding new state that reads env config directly]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RepoConfigLoader pattern: GSD_REPOS JSON array (multi) → GSD_GITHUB_OWNER+GSD_GITHUB_REPO (legacy single) → InvalidOperationException (neither)"
    - "IConfiguration removed from DI-injected states — config-reading confined to Program.cs startup"
    - "Path sanitization: replace '/', '\\', '..' segments with underscores before building checkpoint filename (T-16-05)"

key-files:
  created: []
  modified:
    - src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs
    - src/GsdOrchestrator/Workflows/States/IdleState.cs
    - src/GsdOrchestrator/Program.cs
    - src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs

key-decisions:
  - "D-16-04: RepoConfigLoader.Load() uses JsonSerializerOptions(PropertyNameCaseInsensitive=true) to handle mixed-case JSON field names in GSD_REPOS"
  - "D-16-05: Program.cs PR review mode uses repos[0] as primary repo — consistent with single-issue mode; backwards-compatible"
  - "D-16-06: T-16-05 security mitigation applied in same task as multi-repo implementation — sanitize owner/repo in FileCheckpointStore.StatePath with Sanitize() helper replacing path-traversal chars with underscores"

patterns-established:
  - "Pattern 3: Config-at-startup only — IConfiguration consumed by RepoConfigLoader at Program.cs startup; states receive structured data (RepoConfig, ctx.Issue) rather than raw config"
  - "Pattern 4: Multi-repo iteration — foreach (var repoConfig in repos) with CancellationToken.IsCancellationRequested guard and per-repo rate limit delay"

requirements-completed: [MULTI-01, MULTI-02, MULTI-03, MULTI-04]

# Metrics
duration: 20min
completed: 2026-06-05
---

# Phase 16 Plan 02: Multi-Repo Support Implementation Summary

**RepoConfigLoader.Load() implemented with GSD_REPOS JSON array and legacy env var fallback; IdleState decoupled from IConfiguration; Program.cs loops watch mode over all repos with per-repo rate limit delay; all 35 tests GREEN**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-06-05T00:00:00Z
- **Completed:** 2026-06-05T00:20:00Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- RepoConfigLoader.Load() fully implemented: parses GSD_REPOS JSON array into IReadOnlyList<RepoConfig> with case-insensitive deserialization; falls back to GSD_GITHUB_OWNER+GSD_GITHUB_REPO for single-repo backwards compat; throws InvalidOperationException when neither source is present — MULTI-01 satisfied
- IdleState: IConfiguration field and constructor parameter removed; ExecuteAsync reads owner/repo from ctx.Issue!.RepoOwner and ctx.Issue!.RepoName — DI hygiene and MULTI-03 context flow complete
- Program.cs: RepoConfigLoader.Load(config) replaces legacy owner/repo reads; watch mode loops over all repos with per-repo rateLimitDelaySeconds; single-issue and PR review modes use repos[0] as primary — MULTI-02 and MULTI-04 satisfied
- FileCheckpointStore.StatePath: T-16-05 path traversal mitigation applied — Sanitize() helper replaces '/', '\', '..' with underscores in owner/repo segments before building checkpoint filename
- All 35 tests GREEN (7 GsdStateMachineTests + 7 TriagingStateTests + 14 TestGeneratingStateTests + 7 MultiRepoConfigTests)

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement RepoConfigLoader.Load() and fix IdleState IConfiguration dependency** - `1657f05` (feat)
2. **Task 2: Update Program.cs multi-repo watch loop; all 35 tests GREEN** - `e3e9607` (feat)

## Files Created/Modified

- `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs` - Added System.Text.Json using; replaced RepoConfigLoader stub with full Load() implementation + private RepoConfigDto sealed record
- `src/GsdOrchestrator/Workflows/States/IdleState.cs` - Removed Microsoft.Extensions.Configuration using and _owner/_repo fields; simplified constructor to (McpToolDispatcher, ILogger); ExecuteAsync reads owner/repo from ctx.Issue!
- `src/GsdOrchestrator/Program.cs` - Replaced owner/repo config reads with RepoConfigLoader.Load(config); updated watch, single-issue, and PR modes; added rateLimitDelaySeconds param to RunWatchModeAsync; added inter-issue rate limit delay
- `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs` - Added Sanitize() helper to StatePath(owner, repo, workflowId) for T-16-05 path traversal mitigation

## Decisions Made

- **D-16-04:** RepoConfigLoader.Load() uses JsonSerializerOptions with PropertyNameCaseInsensitive=true to handle mixed-case JSON field names (e.g., "Owner" vs "owner") without requiring exact casing from operators.
- **D-16-05:** Program.cs PR review mode uses repos[0] as primary repo — no dedicated --pr flag for multi-repo targeting, consistent with how single-issue mode works.
- **D-16-06:** T-16-05 security mitigation (path traversal in StatePath) was applied as part of Task 1 since FileCheckpointStore.cs was already being modified and the threat model designated it `mitigate`. Deviation Rule 2 applied.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Applied T-16-05 path traversal mitigation in FileCheckpointStore.StatePath**
- **Found during:** Task 1 (reading FileCheckpointStore.cs during IdleState changes)
- **Issue:** Plan's threat model marks T-16-05 (path traversal via GSD_REPOS owner/repo → StatePath) as `mitigate` disposition; FileCheckpointStore.StatePath(owner, repo, workflowId) built filename without sanitizing owner/repo segments — a crafted GSD_REPOS value could write checkpoint files outside the state directory
- **Fix:** Added `Sanitize(string segment)` private static method that replaces '/', '\', '..' with '_'; applied to owner and repo segments in the 3-arg StatePath overload
- **Files modified:** src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs
- **Verification:** Build succeeds; test 7 (SaveAsync_WithIssueContext_CreatesNamespacedCheckpointFile) still passes — normal owner/repo values ("myorg", "myrepo") are unchanged by sanitization
- **Committed in:** 1657f05 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 2 — missing critical security mitigation from threat model)
**Impact on plan:** Security hardening as planned in threat model. No scope creep — Sanitize() only affects the 3-arg StatePath overload used for new writes; LoadAsync and ArchiveAsync use the 1-arg overload unchanged.

## Issues Encountered

- dotnet build with `-q` (quiet) mode reported "error" for MSBuild informational messages ("Building target completely", "Creating directory") on first build in a fresh worktree. Using `-v:m` (minimal verbosity) instead shows "Build succeeded. 0 Error(s)" correctly. All 35 tests ran successfully after proper build verification.

## Known Stubs

None — all Wave 1 stubs replaced. RepoConfigLoader.Load() is fully implemented and all 7 MultiRepoConfigTests pass GREEN.

## Next Phase Readiness

- Phase 16 complete: multi-repo support fully implemented
- GSD_REPOS JSON array env var enables watching multiple repos in one deployment
- Per-repo checkpoint namespacing prevents cross-contamination (from Plan 16-01)
- Rate limit delay configurable per repo via JSON (rateLimitDelaySeconds field)
- Single-repo backwards compat via GSD_GITHUB_OWNER+GSD_GITHUB_REPO fallback
- No blockers for next milestone

---
*Phase: 16-multi-repo-support*
*Completed: 2026-06-05*
