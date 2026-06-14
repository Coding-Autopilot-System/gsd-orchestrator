using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class PrCreatingStateTests
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
            Plan = new AnalysisPlan(
                BranchName: "fix/issue-42-foo",
                FilesToModify: [new PlannedFile("src/Foo.cs", "broken")],
                Summary: "Fix null ref in Foo",
                RequiresTests: false),
            Branch = new BranchContext("fix/issue-42-foo", "abc123"),
            Edits = new EditContext([
                new FileEdit("src/Foo.cs", "oldsha", "newsha", "fix(#42): update Foo")
            ]),
            CurrentState = WorkflowState.PrCreating
        };

    private static IChatClient BuildLlm(string title = "fix: Fix null ref in Foo")
    {
        var llm = Substitute.For<IChatClient>();
        var json = $$"""{"title":"{{title}}","body":"## What\nFix applied.\n\n## Why\nNull ref.\n\nCloses #42"}""";
        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(
               new ChatMessage(ChatRole.Assistant, json))));
        return llm;
    }

    /// <summary>
    /// Builds an MCP client where list_pull_requests returns either empty (no existing PR)
    /// or an existing PR.
    /// </summary>
    private static IMcpClient BuildMcpClient(bool existingPrExists = false)
    {
        var mcp = Substitute.For<IMcpClient>();

        if (existingPrExists)
        {
            mcp.CallToolAsync(
                Arg.Is<string>("list_pull_requests"),
                Arg.Any<JsonObject>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult(
                   """[{"number":77,"html_url":"https://github.com/testowner/testrepo/pull/77","title":"fix: existing PR","body":"Closes #42"}]""",
                   false)));
        }
        else
        {
            mcp.CallToolAsync(
                Arg.Is<string>("list_pull_requests"),
                Arg.Any<JsonObject>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult("[]", false)));

            mcp.CallToolAsync(
                Arg.Is<string>("create_pull_request"),
                Arg.Any<JsonObject>(),
                Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult(
                   """{"number":88,"html_url":"https://github.com/testowner/testrepo/pull/88"}""",
                   false)));
        }

        return mcp;
    }

    private static PrCreatingState BuildSut(IMcpClient mcpClient, IChatClient llm) =>
        new(BuildDispatcher(mcpClient), llm, NullLogger<PrCreatingState>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    // PRCREATING-01: new PR created → transitions to Reviewing
    [Fact]
    public async Task ExecuteAsync_NoPrExists_CreatesNewPrAndTransitionsToReviewing()
    {
        var sut = BuildSut(BuildMcpClient(existingPrExists: false), BuildLlm());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Reviewing, result.CurrentState);
        Assert.NotNull(result.PullRequest);
        Assert.Equal(88, result.PullRequest!.PrNumber);
    }

    // PRCREATING-02: PR already exists → resumes without creating new
    [Fact]
    public async Task ExecuteAsync_PrAlreadyExists_ResumesExistingPr()
    {
        var mcp = BuildMcpClient(existingPrExists: true);
        var sut = BuildSut(mcp, BuildLlm());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Reviewing, result.CurrentState);
        Assert.Equal(77, result.PullRequest!.PrNumber);

        // create_pull_request must NOT be called — we reuse the existing one
        await mcp.DidNotReceive().CallToolAsync(
            Arg.Is<string>("create_pull_request"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // PRCREATING-01: create_pull_request called with correct head branch
    [Fact]
    public async Task ExecuteAsync_NoPrExists_CallsCreatePullRequestWithBranchName()
    {
        var mcp = BuildMcpClient(existingPrExists: false);
        var sut = BuildSut(mcp, BuildLlm());

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_pull_request"),
            Arg.Is<JsonObject>(j => j["head"]!.GetValue<string>() == "fix/issue-42-foo"),
            Arg.Any<CancellationToken>());
    }

    // PRCREATING-01: PR URL is stored in context
    [Fact]
    public async Task ExecuteAsync_NoPrExists_StoresPrUrlInContext()
    {
        var sut = BuildSut(BuildMcpClient(existingPrExists: false), BuildLlm());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Contains("pull/88", result.PullRequest!.PrUrl);
    }

    // PRCREATING-01: MCP failure on create_pull_request propagates
    [Fact]
    public async Task ExecuteAsync_CreatePrThrows_PropagatesMcpException()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("list_pull_requests"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("[]", false)));
        mcp.CallToolAsync(
            Arg.Is<string>("create_pull_request"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("permissions denied"));

        var sut = BuildSut(mcp, BuildLlm());

        await Assert.ThrowsAsync<McpException>(
            () => sut.ExecuteAsync(BuildContext(), CancellationToken.None));
    }

    // PRCREATING-01: cancellation is propagated through MCP call
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

        var sut = BuildSut(mcp, BuildLlm());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(BuildContext(), cts.Token));
    }
}
