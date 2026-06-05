---
phase: 16-multi-repo-support
reviewed: 2026-06-05T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - src/GsdOrchestrator.Tests/MultiRepoConfigTests.cs
  - src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs
  - src/GsdOrchestrator/Program.cs
  - src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs
  - src/GsdOrchestrator/Workflows/States/IdleState.cs
findings:
  critical: 3
  warning: 4
  info: 2
  total: 9
status: issues_found
---

# Phase 16: Code Review Report

**Reviewed:** 2026-06-05T00:00:00Z
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Phase 16 adds multi-repo support via `RepoConfig`/`RepoConfigLoader`, namespaced checkpoint filenames, and watch-mode iteration over multiple repos. The model and config loader logic is sound. However there are three blockers: `workflowId` is never sanitized in the single-arg `StatePath` overload used by `LoadAsync` and `ArchiveAsync`, enabling path-traversal attacks; the `Sanitize` helper's `Replace("..", "__")` is insufficient and trivially bypassed; and `ListActiveWorkflowsAsync` now returns namespaced filenames as workflow IDs that cannot be resolved by `LoadAsync`. Four warnings cover test assertion weakness, mutable history exposure, fragile archive path construction, and a layering violation.

---

## Critical Issues

### CR-01: workflowId path-traversal — LoadAsync and ArchiveAsync accept unsanitized input

**File:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs:52,72`

**Issue:** `LoadAsync` and `ArchiveAsync` both call the single-argument `StatePath(workflowId)` overload (lines 52 and 72) which does a raw `Path.Combine(_stateDir, $"{workflowId}.json")`. The `workflowId` string is never sanitized. A caller (or a value persisted in a checkpoint file) that passes `workflowId = "../../etc/passwd"` will resolve outside `_stateDir`. The two-argument `StatePath(owner, repo, workflowId)` added for MULTI-03 does call `Sanitize` on owner and repo, but never on `workflowId` either.

**Fix:**
```csharp
// Add sanitization to the single-arg overload and to the three-arg workflowId component
private string StatePath(string workflowId) =>
    Path.Combine(_stateDir, $"{Sanitize(workflowId)}.json");

private string StatePath(string owner, string repo, string workflowId)
{
    var prefix = (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        ? Sanitize(workflowId)
        : $"{Sanitize(owner)}_{Sanitize(repo)}_{Sanitize(workflowId)}";
    return Path.Combine(_stateDir, $"{prefix}.json");
}
```

Also verify the resolved path stays under `_stateDir` after combining:
```csharp
private static void AssertUnderStateDir(string stateDir, string resolved)
{
    var full = Path.GetFullPath(resolved);
    if (!full.StartsWith(Path.GetFullPath(stateDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Path escape detected: {resolved}");
}
```

---

### CR-02: Sanitize helper does not prevent path traversal — Replace("..", "__") is insufficient

**File:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs:98`

**Issue:** `Sanitize` replaces `/`, `\`, and `..` but the replacement of `..` is a simple string substitution (`Replace("..", "__")`). This is bypassed by:
- Input `"..."` → after replace → `"__."` — one remaining dot survives, can be composed at OS level.
- Input `"a/../b"` — after `/` replace becomes `"a_..`_b"` — `..` still present.
- Percent-encoded or Unicode path separators (e.g., `%2F`) are not stripped.
- On Windows, colons (`:`) in a segment name (e.g., `C:`) allow drive-relative paths.

The sanitizer claims in its doc comment (T-16-05) to prevent crafted `GSD_REPOS` values from escaping `_stateDir`, but this guarantee is not met.

**Fix:** Replace the allow-nothing approach with an allow-list: only permit alphanumeric characters, hyphens, and dots (single):
```csharp
private static readonly System.Text.RegularExpressions.Regex SafeSegment =
    new(@"[^a-zA-Z0-9\-]", System.Text.RegularExpressions.RegexOptions.Compiled);

private static string Sanitize(string segment) =>
    SafeSegment.Replace(segment, "_");
```
Then also perform the escape-check in `AssertUnderStateDir` (see CR-01) as defence-in-depth.

---

### CR-03: ListActiveWorkflowsAsync returns namespaced filenames — breaks LoadAsync / resume

**File:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs:59-68`

**Issue:** Before Phase 16 every checkpoint file was named `{workflowId}.json`. Now `SaveAsync` writes `{owner}_{repo}_{workflowId}.json`. `ListActiveWorkflowsAsync` strips the `.json` extension with `Path.GetFileNameWithoutExtension` and returns the result as workflow IDs. Any caller that takes those IDs and passes them to `LoadAsync` (e.g., `GsdStateMachine.ResumeAsync` at `GsdStateMachine.cs:56`) will call `StatePath("myorg_myrepo_abc123")`, looking for `myorg_myrepo_abc123.json` — which exists — but only by coincidence for the three-arg path. However, if a workflow was saved without owner/repo (fallback branch, lines 87-88), the file is named `{workflowId}.json` and is returned correctly. The real problem is that `ListActiveWorkflowsAsync` returns `"myorg_myrepo_abc123"` as the workflow ID when the logical workflow ID is `"abc123"`. This corrupts the external contract: callers printing/storing workflow IDs from `ListActiveWorkflowsAsync` for use with `--resume` will pass the full namespaced string, which then fails the single-arg `StatePath` lookup after CR-01 is fixed (since sanitized `_` are valid but the lookup file is `myorg_myrepo_abc123.json` not `abc123.json`).

More concretely: `GsdStateMachine.ResumeAsync` receives a `workflowId` from the user (e.g., from `--resume abc123`). `LoadAsync("abc123")` builds path `{stateDir}/abc123.json`. But the file on disk is `{stateDir}/myorg_myrepo_abc123.json`. Resume silently returns `null` and fails — existing workflows saved after Phase 16 cannot be resumed.

**Fix:** Either (a) `LoadAsync` must scan for a file matching `*_{workflowId}.json` as a fallback, or (b) maintain a separate index mapping workflowId → filename, or (c) store the filename used in `SaveAsync` inside the checkpoint JSON itself so `LoadAsync` can recover it:
```csharp
// Option A: scan fallback in LoadAsync
public async Task<GsdWorkflowContext?> LoadAsync(string workflowId, CancellationToken ct = default)
{
    // Try exact match first (legacy / fallback path)
    var exactPath = StatePath(workflowId);
    if (File.Exists(exactPath))
    {
        await using var fs = new FileStream(exactPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<GsdWorkflowContext>(fs, JsonOpts, ct);
    }
    // Try namespaced match: *_{workflowId}.json
    var candidates = Directory.GetFiles(_stateDir, $"*_{workflowId}.json");
    if (candidates.Length == 1)
    {
        await using var fs = new FileStream(candidates[0], FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<GsdWorkflowContext>(fs, JsonOpts, ct);
    }
    return null;
}
```
`ArchiveAsync` needs the same scan logic.

---

## Warnings

### WR-01: Test assertions for exception type are too weak — ThrowsAny<Exception> accepts stubs

**File:** `src/GsdOrchestrator.Tests/MultiRepoConfigTests.cs:60,101`

**Issue:** Tests 3 and 6 use `Assert.ThrowsAny<Exception>()`. This passes even when `RepoConfigLoader.Load` throws `NotImplementedException` (a stub). The RED comments in the test file acknowledge this, but the assertions have not been tightened in the GREEN implementation. In the final green state, Test 3 should assert `InvalidOperationException` and Test 6 should assert `System.Text.Json.JsonException` as documented in the comments. As written, a regression that replaces the correct exception with any other exception (including a crash or a stub) will not be caught.

**Fix:**
```csharp
// Test 3 — line 59
Assert.Throws<InvalidOperationException>(() => RepoConfigLoader.Load(config));

// Test 6 — line 101
Assert.Throws<System.Text.Json.JsonException>(() => RepoConfigLoader.Load(config));
```

---

### WR-02: GsdWorkflowContext.History exposes mutable list through init-only property

**File:** `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs:180`

**Issue:** `History` is declared as `List<StateTransitionEvent>` with `init`. The `init` restricts reassignment of the reference but not mutation of the list contents. Any code holding a reference to the context can call `ctx.History.Add(...)`, `ctx.History.Clear()`, or `ctx.History.RemoveAt(...)` directly, bypassing `Transition()`. Since `GsdWorkflowContext` is a `record`, consumers expect value-semantics immutability. The mutable list breaks that contract and can cause audit-trail corruption.

**Fix:**
```csharp
// Change the type to IReadOnlyList<StateTransitionEvent>
public IReadOnlyList<StateTransitionEvent> History { get; init; } = [];
```
The `Transition` method already uses `[.. History, ...]` which works for any `IEnumerable`, so this is a non-breaking internal change.

---

### WR-03: ArchiveAsync constructs archive path via ".." relative segment — fragile

**File:** `src/GsdOrchestrator/Checkpointing/FileCheckpointStore.cs:75`

**Issue:** `Path.Combine(_stateDir, "..", "archive")` relies on OS path normalization to land at `.gsd/archive`. If `_stateDir` ever contains a trailing separator or a symlink, `Path.GetFullPath` resolves differently. More importantly, this pattern deliberately steps outside `_stateDir`, yet the archive target itself has no bounds validation. A crafted `workflowId` that ends in `/` (after sanitize bypass) would make `$"{workflowId}.json"` resolve to a directory path in `Path.Combine`.

**Fix:** Derive the archive directory from `repoRoot` explicitly in the constructor, parallel to `_stateDir`:
```csharp
private readonly string _stateDir;
private readonly string _archiveDir;

public FileCheckpointStore(string repoRoot, ILogger<FileCheckpointStore> logger)
{
    _stateDir  = Path.Combine(repoRoot, ".gsd", "state");
    _archiveDir = Path.Combine(repoRoot, ".gsd", "archive");
    Directory.CreateDirectory(_stateDir);
}
```
Then use `_archiveDir` directly in `ArchiveAsync`.

---

### WR-04: RepoConfigLoader placed inside WorkflowModels.cs violates single-responsibility

**File:** `src/GsdOrchestrator/Workflows/Models/WorkflowModels.cs:128-158`

**Issue:** `WorkflowModels.cs` is the models file for workflow state records. `RepoConfigLoader` is a configuration loading service with a dependency on `IConfiguration`. Placing it here means any consumer that imports only models also pulls in configuration infrastructure. It also makes the file harder to navigate and violates the principle that a models file contains only data types.

**Fix:** Move `RepoConfigLoader` and `RepoConfigDto` to `src/GsdOrchestrator/Workflows/Models/RepoConfigLoader.cs` (same namespace is fine) or to `src/GsdOrchestrator/Configuration/RepoConfigLoader.cs`.

---

## Info

### IN-01: IdleState.ExecuteAsync dereferences ctx.Issue with null-forgiving operator without guard

**File:** `src/GsdOrchestrator/Workflows/States/IdleState.cs:23`

**Issue:** Line 23 uses `ctx.Issue!.RepoOwner` (null-forgiving `!`). If `IdleState.ExecuteAsync` is ever called with a context where `Issue` is null (e.g., a resumed context that never completed the Idle step), this throws a `NullReferenceException` at runtime with no diagnostic message. Line 44 also uses `ctx.Issue!.Number` redundantly after already dereferencing on line 23.

**Fix:** Add an explicit guard at the top of the method:
```csharp
if (ctx.Issue is null)
    throw new InvalidOperationException("IdleState requires a pre-populated IssueContext with at least Number, RepoOwner, and RepoName.");
```
Then remove the `!` operators and use `ctx.Issue.RepoOwner` directly.

---

### IN-02: Program.cs watch mode eviction removes arbitrary items — HashSet<T> has no insertion order

**File:** `src/GsdOrchestrator/Program.cs:231-234`

**Issue:** The eviction logic uses `processedIssues.Take(processedIssuesEvictCount).ToList()` to evict "oldest" entries. `HashSet<int>` does not guarantee any enumeration order. The comment says "Evict oldest entries" but the implementation evicts arbitrary entries. Re-opened issues may be re-processed before the 500-capacity is reached if their numbers happen to be at the front of the enumeration.

**Fix:** Replace `HashSet<int>` with a `Queue<int>` plus a parallel `HashSet<int>` for O(1) lookup, or use a `LinkedList` approach, to preserve insertion order for accurate FIFO eviction:
```csharp
var processedQueue = new Queue<int>(processedIssuesCapacity);
var processedSet = new HashSet<int>();

// When adding:
if (processedSet.Count >= processedIssuesCapacity)
{
    for (int i = 0; i < processedIssuesEvictCount; i++)
    {
        if (processedQueue.TryDequeue(out var old))
            processedSet.Remove(old);
    }
}
processedQueue.Enqueue(num);
processedSet.Add(num);

// When checking:
var pending = openNumbers.Except(processedSet).ToList();
```

---

_Reviewed: 2026-06-05T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
