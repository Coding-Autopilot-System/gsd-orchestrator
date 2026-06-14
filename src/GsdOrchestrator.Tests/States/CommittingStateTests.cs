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

public class CommittingStateTests
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
            Issue = new IssueContext(42, "Fix Foo", "body", [], "testowner", "testrepo", "main"),
            Branch = new BranchContext("fix/issue-42-foo", "abc123"),
            Edits = new EditContext([
                new FileEdit("src/Foo.cs", "oldsha", "newsha999", "fix(#42): update Foo")
            ]),
            CurrentState = WorkflowState.Committing
        };

    private static IMcpClient BuildMcpClient(string sha = "finalsha999abc")
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("get_branch"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               $"{{\"commit\":{{\"sha\":\"{sha}\"}}}}",
               false)));
        return mcp;
    }

    private static CommittingState BuildSut(IMcpClient mcpClient) =>
        new(BuildDispatcher(mcpClient), NullLogger<CommittingState>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    // COMMITTING-01: happy path transitions to PrCreating
    [Fact]
    public async Task ExecuteAsync_GetBranchSucceeds_TransitionsToPrCreating()
    {
        var sut = BuildSut(BuildMcpClient());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.PrCreating, result.CurrentState);
    }

    // COMMITTING-01: sha from get_branch is recorded in CommitContext
    [Fact]
    public async Task ExecuteAsync_GetBranchSucceeds_RecordsShaInCommitContext()
    {
        var sut = BuildSut(BuildMcpClient("finalsha999abc"));

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.NotNull(result.Commit);
        Assert.Equal("finalsha999abc", result.Commit!.FinalCommitSha);
    }

    // COMMITTING-01: commit URL is formed correctly
    [Fact]
    public async Task ExecuteAsync_GetBranchSucceeds_BuildsCorrectCommitUrl()
    {
        var sut = BuildSut(BuildMcpClient("finalsha999abc"));

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Contains("testowner/testrepo/commit/finalsha999abc", result.Commit!.CommitUrl);
    }

    // COMMITTING-01: get_branch is called with the branch name from context
    [Fact]
    public async Task ExecuteAsync_CallsGetBranchWithCorrectBranchName()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp);

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.Received().CallToolAsync(
            Arg.Is<string>("get_branch"),
            Arg.Is<JsonObject>(j => j["branch"]!.GetValue<string>() == "fix/issue-42-foo"),
            Arg.Any<CancellationToken>());
    }

    // COMMITTING-01: MCP failure propagates
    [Fact]
    public async Task ExecuteAsync_GetBranchThrows_PropagatesMcpException()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("get_branch"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("network error"));

        var sut = BuildSut(mcp);

        await Assert.ThrowsAsync<McpException>(
            () => sut.ExecuteAsync(BuildContext(), CancellationToken.None));
    }

    // COMMITTING-01: cancellation is propagated through MCP call
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
