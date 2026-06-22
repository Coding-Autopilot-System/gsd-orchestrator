using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

/// <summary>
/// Additional ReviewingState tests for issue-mode (ExecuteIssueModeAsync / GenerateReviewCommentAsync).
/// The PR-review mode (ExecutePrReviewAsync) is already covered by ReviewingStateTests.cs.
/// </summary>
public class ReviewingStateIssueModeTests
{
    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    /// <summary>
    /// Context shaped for issue-mode: PrReview is null, Issue/PullRequest/Plan/Edits set.
    /// </summary>
    private static GsdWorkflowContext BuildIssueContext(string[]? reviewers = null) =>
        new()
        {
            Issue = new IssueContext(42, "Fix the bug", "Body text", [], "testowner", "testrepo", "main"),
            PullRequest = new PullRequestContext(99, "https://github.com/testowner/testrepo/pull/99",
                "fix: address the bug", "Fixes #42"),
            Plan = new AnalysisPlan("fix/issue-42", [new PlannedFile("src/Foo.cs", "fix null ref")], "Fix null ref", false),
            Edits = new EditContext([new FileEdit("src/Foo.cs", "oldsha", "newsha", "fix(#42): null ref")]),
            CurrentState = WorkflowState.Reviewing
        };

    private static IChatClient BuildLlm(string reviewComment = "LGTM! Automated changes applied.")
    {
        var llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(
               new ChatMessage(ChatRole.Assistant, reviewComment))));
        return llm;
    }

    private static IMcpClient BuildMcpClient()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));
        mcp.CallToolAsync(
            Arg.Is<string>("request_reviewers"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));
        return mcp;
    }

    private static ReviewingState BuildSut(IMcpClient mcpClient, IChatClient llm, string gsdReviewers = "")
    {
        var config = Substitute.For<IConfiguration>();
        config["GSD_REVIEWERS"].Returns(gsdReviewers);
        return new ReviewingState(
            BuildDispatcher(mcpClient),
            llm,
            config,
            NullLogger<ReviewingState>.Instance);
    }

    // REV-03: issue mode posts comment and transitions to Documenting
    [Fact]
    public async Task ExecuteAsync_IssueMode_TransitionsToDocumenting()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlm());
        var result = await sut.ExecuteAsync(BuildIssueContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Documenting, result.CurrentState);
    }

    // REV-03: issue mode posts add_issue_comment
    [Fact]
    public async Task ExecuteAsync_IssueMode_PostsAddIssueComment()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm());
        await sut.ExecuteAsync(BuildIssueContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // REV-03: when reviewers configured, request_reviewers is called
    [Fact]
    public async Task ExecuteAsync_IssueMode_WithReviewers_CallsRequestReviewers()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm(), gsdReviewers: "alice,bob");
        await sut.ExecuteAsync(BuildIssueContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("request_reviewers"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // REV-03: when no reviewers configured, request_reviewers is NOT called
    [Fact]
    public async Task ExecuteAsync_IssueMode_NoReviewers_DoesNotCallRequestReviewers()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm(), gsdReviewers: "");
        await sut.ExecuteAsync(BuildIssueContext(), CancellationToken.None);
        await mcp.DidNotReceive().CallToolAsync(
            Arg.Is<string>("request_reviewers"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // REV-03: request_reviewers failing is swallowed (logged, not rethrown)
    [Fact]
    public async Task ExecuteAsync_IssueMode_RequestReviewersFails_DoesNotRethrow()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));
        mcp.CallToolAsync(
            Arg.Is<string>("request_reviewers"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns<Task<McpToolResult>>(_ => throw new McpException("forbidden"));

        var sut = BuildSut(mcp, BuildLlm(), gsdReviewers: "reviewer1");
        // Should not throw
        var result = await sut.ExecuteAsync(BuildIssueContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Documenting, result.CurrentState);
    }

    // REV-03: missing Issue/PullRequest/Plan/Edits in issue mode throws InvalidOperationException
    [Fact]
    public async Task ExecuteAsync_IssueMode_MissingRequiredContext_ThrowsInvalidOperationException()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm());

        // No PrReview (so it goes to issue mode) and no Issue/Plan/etc.
        var emptyCtx = new GsdWorkflowContext { CurrentState = WorkflowState.Reviewing };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(emptyCtx, CancellationToken.None));
    }

    // REV-03: comment body includes GSD bot prefix
    [Fact]
    public async Task ExecuteAsync_IssueMode_CommentBodyIncludesGsdPrefix()
    {
        var mcp = Substitute.For<IMcpClient>();
        string? capturedBody = null;
        mcp.CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(ci =>
           {
               capturedBody = ci.Arg<JsonObject>()["body"]?.GetValue<string>();
               return Task.FromResult(new McpToolResult("", false));
           });

        var sut = BuildSut(mcp, BuildLlm("Changes look good."));
        await sut.ExecuteAsync(BuildIssueContext(), CancellationToken.None);

        Assert.NotNull(capturedBody);
        Assert.Contains("GSD Orchestrator", capturedBody);
    }
}
