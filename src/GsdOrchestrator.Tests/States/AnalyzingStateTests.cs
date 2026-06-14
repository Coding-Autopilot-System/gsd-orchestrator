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

public class AnalyzingStateTests
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
            Issue = new IssueContext(42, "Fix null reference in state machine", "Some body text", [], "testowner", "testrepo", "main"),
            CurrentState = WorkflowState.Analyzing
        };

    private static IMcpClient BuildMcpClient()
    {
        var mcp = Substitute.For<IMcpClient>();
        // search_code returns best-effort results
        mcp.CallToolAsync(
            Arg.Is<string>("search_code"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("""[{"path":"src/Foo.cs"}]""", false)));
        return mcp;
    }

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

    private static AnalyzingState BuildSut(IMcpClient mcpClient, IChatClient llm) =>
        new(BuildDispatcher(mcpClient), llm, NullLogger<AnalyzingState>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    // ANALYZING-01: valid LLM response transitions to Branching
    [Fact]
    public async Task ExecuteAsync_ValidPlanResponse_TransitionsToBranching()
    {
        var llm = BuildLlm("""
            {
              "branchName": "fix/issue-42-null-reference",
              "filesToModify": [
                { "path": "src/GsdOrchestrator/Workflows/GsdStateMachine.cs", "rationale": "Null deref here" }
              ],
              "summary": "Fix null reference in state machine",
              "requiresTests": true
            }
            """);
        var sut = BuildSut(BuildMcpClient(), llm);

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Branching, result.CurrentState);
        Assert.NotNull(result.Plan);
        Assert.Equal("fix/issue-42-null-reference", result.Plan.BranchName);
        Assert.Single(result.Plan.FilesToModify);
    }

    // ANALYZING-01: plan populated with correct file list
    [Fact]
    public async Task ExecuteAsync_ValidPlanResponse_PopulatesPlanOnContext()
    {
        var llm = BuildLlm("""
            {
              "branchName": "fix/issue-42-state",
              "filesToModify": [
                { "path": "src/Foo.cs", "rationale": "Core logic" },
                { "path": "src/Bar.cs", "rationale": "Helper" }
              ],
              "summary": "Two-file fix",
              "requiresTests": false
            }
            """);
        var sut = BuildSut(BuildMcpClient(), llm);

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(2, result.Plan!.FilesToModify.Count);
        Assert.False(result.Plan.RequiresTests);
    }

    // ANALYZING-01: LLM failure after 3 attempts throws
    [Fact]
    public async Task ExecuteAsync_LlmAlwaysReturnsBadJson_ThrowsInvalidOperationException()
    {
        var llm = BuildLlm("this is not json at all !!!!");
        var sut = BuildSut(BuildMcpClient(), llm);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(BuildContext(), CancellationToken.None));
    }

    // ANALYZING-02: search_code failure is swallowed (best-effort)
    [Fact]
    public async Task ExecuteAsync_SearchCodeThrows_StillProducesPlan()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("search_code"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("rate limited"));

        var llm = BuildLlm("""
            {
              "branchName": "fix/issue-42-fallback",
              "filesToModify": [{ "path": "src/Foo.cs", "rationale": "broken" }],
              "summary": "Fallback fix",
              "requiresTests": false
            }
            """);

        var sut = BuildSut(mcp, llm);

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Branching, result.CurrentState);
    }

    // ANALYZING-01: cancellation is propagated through LLM call
    [Fact]
    public async Task ExecuteAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // search_code is best-effort and swallows exceptions — cancel via LLM instead
        using var cts = new CancellationTokenSource();

        var llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(ci =>
           {
               cts.Cancel();
               ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
               return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));
           });

        var sut = BuildSut(BuildMcpClient(), llm);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(BuildContext(), cts.Token));
    }
}
