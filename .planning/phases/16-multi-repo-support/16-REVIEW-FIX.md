---
phase: 16-multi-repo-support
fixed_at: 2026-06-05T18:56:00Z
review_path: .planning/phases/16-multi-repo-support/16-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 16: Code Review Fix Report

**Fixed at:** 2026-06-05T18:56:00Z
**Source review:** .planning/phases/16-multi-repo-support/16-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7 (3 Critical + 4 Warning; Info excluded per fix_scope=critical_warning)
- Fixed: 7
- Skipped: 0

## Fixed Issues

### CR-01: workflowId path-traversal in LoadAsync and ArchiveAsync

**Files modified:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs`
**Commit:** 1c0cfea
**Applied fix:** Single-arg `StatePath(workflowId)` now calls `Sanitize(workflowId)`. Three-arg overload also sanitizes `workflowId` via `Sanitize(workflowId)` in both branches. Combined with CR-02's allowlist sanitizer this closes the path-traversal vector.

---

### CR-02: Sanitize helper insufficient — Replace("..", "__") bypassable

**Files modified:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs`
**Commit:** 1c0cfea
**Applied fix:** Replaced the character-denylist approach with a compiled allowlist regex `[^a-zA-Z0-9\-_]` that replaces every non-safe character with `_`. Handles `.`, `..`, `/`, `\`, `%2F`, `:`, Unicode separators, and all other bypass vectors in one pass.

---

### CR-03: ListActiveWorkflowsAsync returns namespaced filenames — breaks LoadAsync / resume

**Files modified:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs`
**Commit:** 1c0cfea
**Applied fix:** `LoadAsync` now tries the exact path first (legacy/no-owner-repo), then falls back to `Directory.GetFiles(_stateDir, $"*_{sanitized}.json")` and loads the single candidate. `ArchiveAsync` uses the same two-step scan so archive also works for namespaced files. Resume of `--resume abc123` now correctly locates `myorg_myrepo_abc123.json`.

---

### WR-01: Test assertions too weak — ThrowsAny<Exception> accepts stubs

**Files modified:** `src/GsdOrchestrator.Tests/MultiRepoConfigTests.cs`
**Commit:** 11dc445
**Applied fix:** Test 3 (missing config) changed to `Assert.Throws<InvalidOperationException>`. Test 6 (malformed JSON) changed to `Assert.Throws<System.Text.Json.JsonException>`. Both assertions are now precise enough to catch regressions.

---

### WR-02: GsdWorkflowContext.History exposes mutable List behind init-only property

**Files modified:** `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs`
**Commit:** 614723e
**Applied fix:** `History` type changed from `List<StateTransitionEvent>` to `IReadOnlyList<StateTransitionEvent>`. The `Transition()` spread `[.. History, ...]` works on any `IEnumerable` — no other internal change required.

---

### WR-03: ArchiveAsync constructs archive path via ".." relative segment

**Files modified:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs`
**Commit:** 1c0cfea
**Applied fix:** Added `_archiveDir` field computed once in the constructor as `Path.GetFullPath(Path.Combine(repoRoot, ".gsd", "archive"))`. `ArchiveAsync` now uses `_archiveDir` directly instead of `Path.Combine(_stateDir, "..", "archive")`.

---

### WR-04: RepoConfigLoader placed inside WorkflowModels.cs violates single-responsibility

**Files modified:** `src/GsdOrchestrator/Workflows/Models/RepoConfigLoader.cs` (new), `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs`
**Commit:** a643c3a
**Applied fix:** Extracted `RepoConfigLoader` and its private `RepoConfigDto` record into a new file `src/GsdOrchestrator/Workflows/Models/RepoConfigLoader.cs` in the same namespace. Removed `using Microsoft.Extensions.Configuration` from `WorkflowModels.cs`. `WorkflowModels.cs` now contains only data type records and enums.

---

## Test Results

All 35 tests passed after applying fixes:

```
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35, Duration: 477 ms
```

---

_Fixed: 2026-06-05T18:56:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
