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

public class ReviewingStateTests
{
    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    // Builds a context shaped for --pr mode (PrReviewContext present, no IssueContext)
    private static GsdWorkflowContext BuildPrContext(int prNumber = 7) =>
        new()
        {
            PrReview = new PrReviewContext(
                prNumber,
                "testowner",
                "testrepo",
                "@@ -1,3 +1,4 @@\n public class Foo\n {\n+    // added comment\n }"),
            CurrentState = WorkflowState.Reviewing
        };

    // LLM returns a valid JSON review response
    private static IChatClient BuildLlmApprove()
    {
        var llm = Substitute.For<IChatClient>();
        var json = """
            {
              "verdict": "APPROVE",
              "summary": "Looks good — no issues found.",
              "comments": []
            }
            """;
        llm.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(
               new ChatMessage(ChatRole.Assistant, json))));
        return llm;
    }

    // LLM returns REQUEST_CHANGES with one inline comment
    private static IChatClient BuildLlmRequestChanges()
    {
        var llm = Substitute.For<IChatClient>();
        var json = """
            {
              "verdict": "REQUEST_CHANGES",
              "summary": "Found a potential null-dereference.",
              "comments": [
                {
                  "path": "src/Foo.cs",
                  "line": 3,
                  "side": "RIGHT",
                  "severity": "error",
                  "body": "Possible null dereference here."
                }
              ]
            }
            """;
        llm.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(
               new ChatMessage(ChatRole.Assistant, json))));
        return llm;
    }

    // LLM returns unparseable response (simulates failure)
    private static IChatClient BuildLlmBadJson()
    {
        var llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(
               new ChatMessage(ChatRole.Assistant, "not valid json at all"))));
        return llm;
    }

    // MCP client that stubs get_pull_request and create_pull_request_review
    private static IMcpClient BuildMcpClient()
    {
        var mcp = Substitute.For<IMcpClient>();

        // get_pull_request — returns minimal PR object with diff_url
        mcp.CallToolAsync(
                Arg.Is<string>("get_pull_request"),
                Arg.Any<JsonObject>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               """{"number":7,"title":"fix: update Foo","body":"Closes #5","diff_url":"https://github.com/testowner/testrepo/pull/7.diff"}""",
               false)));

        // create_pull_request_review — simulates successful review submission
        mcp.CallToolAsync(
                Arg.Is<string>("create_pull_request_review"),
                Arg.Any<JsonObject>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               """{"id":1001,"state":"APPROVED"}""",
               false)));

        return mcp;
    }

    private static ReviewingState BuildSut(IMcpClient mcpClient, IChatClient llm)
    {
        var config = Substitute.For<IConfiguration>();
        config["GSD_REVIEWERS"].Returns("");
        return new ReviewingState(
            BuildDispatcher(mcpClient),
            llm,
            config,
            NullLogger<ReviewingState>.Instance);
    }

    // ── Test 1: REV-01 — APPROVE verdict transitions to Done ─────────────────
    [Fact]
    public async Task ExecuteAsync_ApproveVerdict_TransitionsToDone()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmApprove());
        var result = await sut.ExecuteAsync(BuildPrContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Done, result.CurrentState);
    }

    // ── Test 2: REV-02 — REQUEST_CHANGES verdict transitions to Done ─────────
    [Fact]
    public async Task ExecuteAsync_RequestChangesVerdict_TransitionsToDone()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmRequestChanges());
        var result = await sut.ExecuteAsync(BuildPrContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Done, result.CurrentState);
    }

    // ── Test 3: REV-01 — APPROVE calls create_pull_request_review with APPROVE ─
    [Fact]
    public async Task ExecuteAsync_ApproveVerdict_SubmitsApproveReview()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmApprove());
        await sut.ExecuteAsync(BuildPrContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_pull_request_review"),
            Arg.Is<JsonObject>(j => j["event"]!.GetValue<string>() == "APPROVE"),
            Arg.Any<CancellationToken>());
    }

    // ── Test 4: REV-02 — REQUEST_CHANGES calls create_pull_request_review with REQUEST_CHANGES ─
    [Fact]
    public async Task ExecuteAsync_RequestChangesVerdict_SubmitsRequestChangesReview()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmRequestChanges());
        await sut.ExecuteAsync(BuildPrContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_pull_request_review"),
            Arg.Is<JsonObject>(j => j["event"]!.GetValue<string>() == "REQUEST_CHANGES"),
            Arg.Any<CancellationToken>());
    }

    // ── Test 5: REV-02 — inline comment path and body are sent in review ──────
    [Fact]
    public async Task ExecuteAsync_WithInlineComments_SendsCommentsInReview()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmRequestChanges());
        await sut.ExecuteAsync(BuildPrContext(), CancellationToken.None);
        // Verify create_pull_request_review was called with a non-empty comments array
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_pull_request_review"),
            Arg.Is<JsonObject>(j => j["comments"] != null && j["comments"]!.AsArray().Count > 0),
            Arg.Any<CancellationToken>());
    }

    // ── Test 6: REV-01 — Review result stored in ctx.Review ──────────────────
    [Fact]
    public async Task ExecuteAsync_ApproveVerdict_StoresReviewResultInContext()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmApprove());
        var result = await sut.ExecuteAsync(BuildPrContext(), CancellationToken.None);
        Assert.NotNull(result.Review);
        Assert.Equal("APPROVE", result.Review!.Verdict);
    }

    // ── Test 7: REV-01 — LLM parse failure throws InvalidOperationException ──
    [Fact]
    public async Task ExecuteAsync_LlmParseFailure_ThrowsInvalidOperationException()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmBadJson());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(BuildPrContext(), CancellationToken.None));
    }
}
