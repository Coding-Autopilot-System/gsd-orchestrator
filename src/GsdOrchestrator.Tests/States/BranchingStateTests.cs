using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class BranchingStateTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    private static GsdWorkflowContext BuildContext() =>
        new()
        {
            Issue = new IssueContext(42, "Test issue", "body", [], "testowner", "testrepo", "main"),
            Plan = new AnalysisPlan(
                BranchName: "fix/issue-42-test",
                FilesToModify: [new PlannedFile("src/Foo.cs", "broken")],
                Summary: "Fix foo",
                RequiresTests: false),
            CurrentState = WorkflowState.Branching
        };

    /// <summary>
    /// Happy-path MCP client: list_branches returns empty, get_branch returns a sha,
    /// create_branch succeeds.
    /// </summary>
    private static IMcpClient BuildMcpClient(bool branchExists = false)
    {
        var mcp = Substitute.For<IMcpClient>();

        // list_branches: if branchExists include the planned branch name
        var branchListJson = branchExists
            ? """[{"name":"fix/issue-42-test"}]"""
            : "[]";
        mcp.CallToolAsync(
            Arg.Is<string>("list_branches"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(branchListJson, false)));

        // get_branch: returns sha for main OR for the existing feature branch
        mcp.CallToolAsync(
            Arg.Is<string>("get_branch"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               """{"commit":{"sha":"abc123def456"}}""", false)));

        // create_branch: succeeds silently
        mcp.CallToolAsync(
            Arg.Is<string>("create_branch"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));

        return mcp;
    }

    private static BranchingState BuildSut(IMcpClient mcpClient) =>
        new(BuildDispatcher(mcpClient), NullLogger<BranchingState>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    // BRANCHING-01: new branch → transitions to Editing
    [Fact]
    public async Task ExecuteAsync_NewBranch_TransitionsToEditing()
    {
        var mcp = BuildMcpClient(branchExists: false);
        var sut = BuildSut(mcp);

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Editing, result.CurrentState);
        Assert.NotNull(result.Branch);
        Assert.Equal("fix/issue-42-test", result.Branch.BranchName);
        Assert.False(result.Branch.WasResumed);
    }

    // BRANCHING-02: branch already exists → resumed, no create_branch call
    [Fact]
    public async Task ExecuteAsync_BranchAlreadyExists_ResumesWithoutCreating()
    {
        var mcp = BuildMcpClient(branchExists: true);
        var sut = BuildSut(mcp);

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Editing, result.CurrentState);
        Assert.True(result.Branch!.WasResumed);

        // create_branch must NOT have been called
        await mcp.DidNotReceive().CallToolAsync(
            Arg.Is<string>("create_branch"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // BRANCHING-01: sha from default branch captured in BranchContext
    [Fact]
    public async Task ExecuteAsync_NewBranch_CapturesBaseSha()
    {
        var mcp = BuildMcpClient(branchExists: false);
        var sut = BuildSut(mcp);

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal("abc123def456", result.Branch!.BaseSha);
    }

    // BRANCHING-01: MCP failure on get_branch throws McpException
    [Fact]
    public async Task ExecuteAsync_GetBranchThrows_PropagatesMcpException()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("list_branches"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("[]", false)));
        mcp.CallToolAsync(
            Arg.Is<string>("get_branch"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("connection refused"));

        var sut = BuildSut(mcp);

        await Assert.ThrowsAsync<McpException>(
            () => sut.ExecuteAsync(BuildContext(), CancellationToken.None));
    }

    // BRANCHING-01: cancellation is propagated through MCP call
    [Fact]
    public async Task ExecuteAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Any<string>(),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(ci =>
           {
               cts.Cancel();
               ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
               return Task.FromResult(new McpToolResult("", false));
           });

        var sut = BuildSut(mcp);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(BuildContext(), cts.Token));
    }
}
