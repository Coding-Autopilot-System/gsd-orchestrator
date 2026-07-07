using System.Text.Json;
using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class CheckpointCorruptionTests
{
    [Fact]
    public async Task LoadAsync_CorruptedFinalCheckpoint_ThrowsJsonException()
    {
        using var fixture = new CheckpointFixture();
        var store = fixture.CreateStore();
        var context = ValidContext("corrupt-final");
        var path = fixture.StatePath(context.WorkflowId);

        await store.SaveAsync(context);
        await File.WriteAllTextAsync(path, "{\"workflowId\":\"corrupt-final\",\"currentSt");

        await Assert.ThrowsAsync<JsonException>(() => store.LoadAsync(context.WorkflowId));
    }

    [Fact]
    public async Task LoadAsync_OrphanedTmpFile_IgnoresGarbageAndReturnsLastGoodCheckpoint()
    {
        using var fixture = new CheckpointFixture();
        var store = fixture.CreateStore();
        var context = ValidContext("orphaned-tmp");
        var path = fixture.StatePath(context.WorkflowId);
        var tmpPath = path + ".tmp";

        await store.SaveAsync(context);
        await File.WriteAllTextAsync(tmpPath, "garbage bytes from interrupted second save");

        var loaded = await store.LoadAsync(context.WorkflowId);

        Assert.NotNull(loaded);
        Assert.Equal(context.WorkflowId, loaded!.WorkflowId);
        Assert.Equal(context.CurrentState, loaded.CurrentState);
        Assert.Equal("1.1", loaded.SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_UnsupportedSchemaVersion_ThrowsInvalidDataException()
    {
        using var fixture = new CheckpointFixture();
        var store = fixture.CreateStore();
        var context = ValidContext("unsupported-version");

        await store.SaveAsync(context);
        await RewriteSchemaVersionAsync(fixture.StatePath(context.WorkflowId), "99.0");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(context.WorkflowId));
        Assert.Contains("unsupported schema version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_LegacySchemaVersion_UpgradesToCurrentVersion()
    {
        using var fixture = new CheckpointFixture();
        var store = fixture.CreateStore();
        var context = ValidContext("legacy-version");

        await store.SaveAsync(context);
        await RewriteSchemaVersionAsync(fixture.StatePath(context.WorkflowId), "1.0");

        var loaded = await store.LoadAsync(context.WorkflowId);

        Assert.NotNull(loaded);
        Assert.Equal("1.1", loaded!.SchemaVersion);
        Assert.Equal(context.WorkflowId, loaded.WorkflowId);
    }

    private static async Task RewriteSchemaVersionAsync(string path, string schemaVersion)
    {
        var checkpoint = await File.ReadAllTextAsync(path);
        var rewritten = checkpoint.Replace("\"schemaVersion\": \"1.1\"", $"\"schemaVersion\": \"{schemaVersion}\"", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, rewritten);
    }

    private static GsdWorkflowContext ValidContext(string workflowId) =>
        new()
        {
            WorkflowId = workflowId,
            SchemaVersion = "1.1",
            CurrentState = WorkflowState.Analyzing,
            Issue = null
        };

    private sealed class CheckpointFixture : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("gsd-checkpoint-tests-").FullName;

        public FileCheckpointStore CreateStore() =>
            new(_root, NullLogger<FileCheckpointStore>.Instance);

        public string StatePath(string workflowId) =>
            Path.Combine(_root, ".gsd", "state", $"{workflowId}.json");

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}