using System.Text.Json;
using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests.States;

/// <summary>
/// Additional coverage for FileCheckpointStore branches not hit by CheckpointStoreTests.
/// </summary>
public class CheckpointStoreAdditionalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileCheckpointStore _store;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CheckpointStoreAdditionalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gsd-extra-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new FileCheckpointStore(_tempDir, NullLogger<FileCheckpointStore>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private GsdWorkflowContext BuildContext(string workflowId, string owner = "owner", string repo = "repo") =>
        new()
        {
            WorkflowId = workflowId,
            Issue = new IssueContext(1, "Test", "body", [], owner, repo, "main"),
            CurrentState = WorkflowState.Analyzing
        };

    // CR-01: ListActiveWorkflowsAsync returns saved workflow IDs
    [Fact]
    public async Task ListActiveWorkflowsAsync_AfterSave_ContainsWorkflowId()
    {
        var ctx = BuildContext("list-test-wf");
        await _store.SaveAsync(ctx);

        var ids = await _store.ListActiveWorkflowsAsync();
        Assert.Contains(ids, id => id.Contains("list-test-wf"));
    }

    // CR-01: ListActiveWorkflowsAsync returns empty when no checkpoints
    [Fact]
    public async Task ListActiveWorkflowsAsync_EmptyStore_ReturnsEmptyList()
    {
        var ids = await _store.ListActiveWorkflowsAsync();
        Assert.Empty(ids);
    }

    // CR-03: ArchiveAsync moves file to archive directory
    [Fact]
    public async Task ArchiveAsync_ExistingCheckpoint_MovesToArchive()
    {
        var ctx = BuildContext("archive-test-wf");
        await _store.SaveAsync(ctx);

        await _store.ArchiveAsync("archive-test-wf");

        // Should no longer be listed as active
        var ids = await _store.ListActiveWorkflowsAsync();
        Assert.DoesNotContain(ids, id => id.Contains("archive-test-wf"));
    }

    // CR-03: ArchiveAsync with missing workflow ID is a no-op (does not throw)
    [Fact]
    public async Task ArchiveAsync_NonExistentWorkflow_DoesNotThrow()
    {
        await _store.ArchiveAsync("does-not-exist-wf"); // should not throw
    }

    // MULTI-03: SaveAsync with owner+repo produces namespaced file, LoadAsync finds it
    [Fact]
    public async Task SaveAsync_WithOwnerAndRepo_LoadAsync_ByWorkflowId_ReturnsContext()
    {
        var ctx = BuildContext("namespaced-wf", owner: "myorg", repo: "myrepo");
        await _store.SaveAsync(ctx);

        // Load by just the workflowId — should find via namespaced scan
        var loaded = await _store.LoadAsync("namespaced-wf");

        Assert.NotNull(loaded);
        Assert.Equal("namespaced-wf", loaded!.WorkflowId);
    }

    // CR-01: path traversal in workflowId is sanitized
    [Fact]
    public async Task SaveAsync_WorkflowIdWithPathTraversal_SanitizesPath()
    {
        var ctx = new GsdWorkflowContext
        {
            WorkflowId = "../../../etc/passwd",
            Issue = new IssueContext(1, "T", "B", [], "o", "r", "main"),
            CurrentState = WorkflowState.Idle
        };

        // Should not throw; file is saved with sanitized name
        await _store.SaveAsync(ctx);

        // Verify we can load it back (the sanitized path is consistent)
        var stateDir = Path.Combine(_tempDir, ".gsd", "state");
        var files = Directory.GetFiles(stateDir);
        // None of them should point outside _stateDir
        foreach (var f in files)
        {
            Assert.StartsWith(Path.GetFullPath(stateDir), Path.GetFullPath(f));
        }
    }

    // CR-03: ArchiveAsync for namespaced file moves it correctly
    [Fact]
    public async Task ArchiveAsync_NamespacedCheckpoint_MovesToArchive()
    {
        var ctx = BuildContext("ns-archive-wf", owner: "org", repo: "rep");
        await _store.SaveAsync(ctx);

        await _store.ArchiveAsync("ns-archive-wf");

        var stateDir = Path.Combine(_tempDir, ".gsd", "state");
        var remaining = Directory.GetFiles(stateDir);
        Assert.DoesNotContain(remaining, f => f.Contains("ns-archive-wf"));
    }
}
