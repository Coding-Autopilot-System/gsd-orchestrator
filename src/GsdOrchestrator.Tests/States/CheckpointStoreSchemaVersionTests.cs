using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class CheckpointStoreSchemaVersionTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..12]);
    private FileCheckpointStore Sut() => new(_tmp, NullLogger<FileCheckpointStore>.Instance);

    // LoadAsync exact path, schema mismatch => returns null (line 79)
    [Fact]
    public async Task Load_ExactPath_WrongSchema_ReturnsNull()
    {
        var sut = Sut();
        var ctx = new GsdWorkflowContext { WorkflowId = "wf1",
            Issue = new IssueContext(1,"t","b",[],"o","r","main"), CurrentState = WorkflowState.Idle };
        await sut.SaveAsync(ctx);
        var stateDir = Path.Combine(_tmp, ".gsd", "state");
        var file = Directory.GetFiles(stateDir, "*wf1*.json").First();
        var json = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, json.Replace("\"1.0\"", "\"0.9\""));
        var loaded = await sut.LoadAsync("wf1");
        Assert.Null(loaded);
    }

    // LoadAsync namespaced scan single candidate loads correctly (line 89)
    [Fact]
    public async Task Load_NamespacedPath_SingleCandidate_Loads()
    {
        var sut = Sut();
        var ctx = new GsdWorkflowContext { WorkflowId = "wfns",
            Issue = new IssueContext(2,"t","b",[],"owner","repo","main"), CurrentState = WorkflowState.Branching };
        await sut.SaveAsync(ctx);
        var loaded = await sut.LoadAsync("wfns");
        Assert.NotNull(loaded);
        Assert.Equal(WorkflowState.Branching, loaded!.CurrentState);
    }

    // LoadAsync exact legacy path (no owner/repo prefix) loads correctly (line 76)
    [Fact]
    public async Task Load_LegacyExactPath_Loads()
    {
        var sut = Sut();
        var stateDir = Path.Combine(_tmp, ".gsd", "state");
        Directory.CreateDirectory(stateDir);
        var ctx = new GsdWorkflowContext { WorkflowId = "legacywf",
            Issue = new IssueContext(3,"t","b",[],"o","r","main"), CurrentState = WorkflowState.Analyzing };
        var opts = new JsonSerializerOptions { WriteIndented=true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(Path.Combine(stateDir,"legacywf.json"), JsonSerializer.Serialize(ctx, opts));
        var loaded = await sut.LoadAsync("legacywf");
        Assert.NotNull(loaded);
        Assert.Equal(WorkflowState.Analyzing, loaded!.CurrentState);
    }

    // ArchiveAsync namespaced file moves to archive dir
    [Fact]
    public async Task Archive_NamespacedFile_MovesToArchive()
    {
        var sut = Sut();
        var ctx = new GsdWorkflowContext { WorkflowId = "wfarch",
            Issue = new IssueContext(4,"t","b",[],"ao","ar","main"), CurrentState = WorkflowState.Done };
        await sut.SaveAsync(ctx);
        await sut.ArchiveAsync("wfarch");
        var stateDir = Path.Combine(_tmp, ".gsd", "state");
        Assert.Empty(Directory.GetFiles(stateDir,"*wfarch*.json"));
        var archDir = Path.Combine(_tmp, ".gsd", "archive");
        Assert.Single(Directory.GetFiles(archDir,"*wfarch*.json"));
    }

    // ArchiveAsync non-existent workflowId is a no-op
    [Fact]
    public async Task Archive_NonExistent_NoOp()
    {
        var sut = Sut();
        await sut.ArchiveAsync("does-not-exist");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmp)) Directory.Delete(_tmp, recursive:true);
    }
}
