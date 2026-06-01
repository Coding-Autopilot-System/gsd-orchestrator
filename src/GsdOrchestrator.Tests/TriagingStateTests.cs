using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

public class TriagingStateTests
{
    // Helper: build McpToolDispatcher with mock IMcpClient (same pattern as GsdStateMachineTests.BuildSut)
    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    // Helper: build GsdWorkflowContext with a test issue
    private static GsdWorkflowContext BuildContext(bool triageModeOnly = false) =>
        new()
        {
            Issue = new IssueContext(42, "Test issue title", "Some body text", [], "testowner", "testrepo", "main"),
            CurrentState = WorkflowState.Triaging,
            TriageModeOnly = triageModeOnly
        };

    // Helper: build mock IChatClient that returns a fixed JSON string
    private static IChatClient BuildLlm(string jsonResponse)
    {
        var llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse))));
        return llm;
    }

    // Helper: build mock IMcpClient that responds to list_issues and add_issue_comment
    private static IMcpClient BuildMcpClient()
    {
        var mcp = Substitute.For<IMcpClient>();
        // list_issues returns an empty array (no open issues for duplicate context)
        mcp.CallToolAsync(
            Arg.Is<string>("list_issues"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("[]", false)));
        // add_issue_comment succeeds
        mcp.CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));
        // update_issue succeeds
        mcp.CallToolAsync(
            Arg.Is<string>("update_issue"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));
        return mcp;
    }

    // Helper: construct TriagingState SUT
    private static TriagingState BuildSut(IMcpClient mcpClient, IChatClient llm) =>
        new(BuildDispatcher(mcpClient), llm, NullLogger<TriagingState>.Instance);

    // ── Test 1: TRIAGE-01 — actionable transitions to Analyzing ───────────
    [Fact]
    public async Task ExecuteAsync_ActionableClassification_TransitionsToAnalyzing()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("""{"classification":"actionable","reason":"Clear bug report.","duplicateNumber":null}""");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext();

        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(WorkflowState.Analyzing, result.CurrentState);
        Assert.Equal("actionable", result.Triage?.Classification);
    }

    // ── Test 2: TRIAGE-01 — needs-info transitions to Done ────────────────
    [Fact]
    public async Task ExecuteAsync_NeedsInfoClassification_TransitionsToDone()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("""{"classification":"needs-info","reason":"Missing reproduction steps.","duplicateNumber":null}""");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext();

        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(WorkflowState.Done, result.CurrentState);
        Assert.Equal("needs-info", result.Triage?.Classification);
    }

    // ── Test 3: TRIAGE-01 — out-of-scope transitions to Done ──────────────
    [Fact]
    public async Task ExecuteAsync_OutOfScopeClassification_TransitionsToDone()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("""{"classification":"out-of-scope","reason":"Not a project goal.","duplicateNumber":null}""");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext();

        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(WorkflowState.Done, result.CurrentState);
        Assert.Equal("out-of-scope", result.Triage?.Classification);
    }

    // ── Test 4: TRIAGE-02/04 — duplicate transitions to Done + calls update_issue ─
    [Fact]
    public async Task ExecuteAsync_DuplicateClassification_TransitionsToDoneAndCallsUpdateIssue()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("""{"classification":"duplicate","reason":"Same as #10.","duplicateNumber":10}""");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext();

        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(WorkflowState.Done, result.CurrentState);
        Assert.Equal("duplicate", result.Triage?.Classification);
        Assert.Equal(10, result.Triage?.DuplicateNumber);
        // Verify update_issue was called to close the issue
        await mcpClient.Received().CallToolAsync(
            Arg.Is<string>("update_issue"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // ── Test 5: TRIAGE-03 — TriageModeOnly=true, actionable still exits to Done ─
    [Fact]
    public async Task ExecuteAsync_TriageModeOnlyTrue_ActionableStillTransitionsToDone()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("""{"classification":"actionable","reason":"Clear bug report.","duplicateNumber":null}""");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext(triageModeOnly: true);

        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(WorkflowState.Done, result.CurrentState);
        Assert.Equal("actionable", result.Triage?.Classification);
    }

    // ── Test 6: TRIAGE-01 — LLM parse failure after 3 attempts throws ─────
    [Fact]
    public async Task ExecuteAsync_LlmParseFailureAllAttempts_ThrowsInvalidOperationException()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("this is not valid json at all");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(ctx, CancellationToken.None));
    }

    // ── Test 7: TRIAGE-04 — any classification posts comment via add_issue_comment ─
    [Fact]
    public async Task ExecuteAsync_AnyClassification_PostsCommentViaAddIssueComment()
    {
        var mcpClient = BuildMcpClient();
        var llm = BuildLlm("""{"classification":"actionable","reason":"Clear bug report.","duplicateNumber":null}""");
        var sut = BuildSut(mcpClient, llm);
        var ctx = BuildContext();

        await sut.ExecuteAsync(ctx, CancellationToken.None);

        // Verify add_issue_comment was called
        await mcpClient.Received().CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }
}
