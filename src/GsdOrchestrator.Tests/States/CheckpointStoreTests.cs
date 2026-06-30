using System.Text.Json;
using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class CheckpointStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileCheckpointStore _store;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CheckpointStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gsd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new FileCheckpointStore(_tempDir, NullLogger<FileCheckpointStore>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GsdWorkflowContext BuildContext(string workflowId, string schemaVersion = "1.0") =>
        new()
        {
            WorkflowId = workflowId,
            SchemaVersion = schemaVersion,
            Issue = new IssueContext(1, "Test", "body", [], "owner", "repo", "main"),
            CurrentState = WorkflowState.Analyzing
        };

    /// <summary>
    /// Writes a checkpoint file directly — bypasses SaveAsync so we can inject any schemaVersion.
    /// </summary>
    private void WriteRawCheckpoint(GsdWorkflowContext ctx)
    {
        var stateDir = Path.Combine(_tempDir, ".gsd", "state");
        Directory.CreateDirectory(stateDir);
        var path = Path.Combine(stateDir, $"{ctx.WorkflowId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(ctx, JsonOpts));
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    // CHECKPOINT-01: compatible schema version — context is returned
    [Fact]
    public async Task LoadAsync_CompatibleSchemaVersion_ReturnsContext()
    {
        var ctx = BuildContext("wf-schema-compat", schemaVersion: "1.0");
        WriteRawCheckpoint(ctx);

        var loaded = await _store.LoadAsync("wf-schema-compat");

        Assert.NotNull(loaded);
        Assert.Equal("wf-schema-compat", loaded!.WorkflowId);
        Assert.Equal(WorkflowState.Analyzing, loaded.CurrentState);
    }

    // CHECKPOINT-02: incompatible schema version — fails clearly
    [Fact]
    public async Task LoadAsync_IncompatibleSchemaVersion_ThrowsInvalidDataException()
    {
        var ctx = BuildContext("wf-schema-incompat", schemaVersion: "2.0");
        WriteRawCheckpoint(ctx);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => _store.LoadAsync("wf-schema-incompat"));
        Assert.Contains("2.0", error.Message);
    }

    // CHECKPOINT-01: save then load round-trip preserves state
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsContext()
    {
        var ctx = new GsdWorkflowContext
        {
            WorkflowId = "wf-roundtrip",
            Issue = new IssueContext(7, "Round trip", "body", [], "owner", "repo", "main"),
            CurrentState = WorkflowState.Branching
        };
        await _store.SaveAsync(ctx);

        var loaded = await _store.LoadAsync("wf-roundtrip");

        Assert.NotNull(loaded);
        Assert.Equal(WorkflowState.Branching, loaded!.CurrentState);
        Assert.Equal(7, loaded.Issue!.Number);
    }

    // CHECKPOINT-02: missing checkpoint returns null
    [Fact]
    public async Task LoadAsync_MissingWorkflow_ReturnsNull()
    {
        var loaded = await _store.LoadAsync("does-not-exist");
        Assert.Null(loaded);
    }
}
