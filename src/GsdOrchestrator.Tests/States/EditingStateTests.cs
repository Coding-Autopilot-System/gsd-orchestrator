using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class EditingStateTests
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
            Issue = new IssueContext(42, "Fix bug in Foo", "body text", [], "testowner", "testrepo", "main"),
            Plan = new AnalysisPlan(
                BranchName: "fix/issue-42-foo",
                FilesToModify: [new PlannedFile("src/GsdOrchestrator/Foo.cs", "bug here")],
                Summary: "Fix null ref in Foo",
                RequiresTests: true),
            Branch = new BranchContext("fix/issue-42-foo", "abc123sha"),
            CurrentState = WorkflowState.Editing
        };

    /// <summary>
    /// Returns IChatClient that simulates the LLM calling write_file (happy path).
    /// </summary>
    private static IChatClient BuildLlmWithWriteFile()
    {
        var llm = Substitute.For<IChatClient>();
        var functionCall = new FunctionCallContent(
            "call_001",
            "write_file",
            new Dictionary<string, object?>
            {
                ["content"] = "public class Foo { public void Bar() {} }",
                ["commitMessage"] = "fix(#42): fix null ref"
            });
        var toolCallMsg = new ChatMessage(ChatRole.Assistant, [functionCall]);
        var toolCallResponse = new ChatResponse(toolCallMsg) { FinishReason = ChatFinishReason.ToolCalls };

        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(toolCallResponse));
        return llm;
    }

    /// <summary>
    /// Returns IChatClient that never calls write_file (stops immediately).
    /// </summary>
    private static IChatClient BuildLlmNoWriteFile()
    {
        var llm = Substitute.For<IChatClient>();
        var stopResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "I cannot make changes to this file"))
        { FinishReason = ChatFinishReason.Stop };
        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(stopResponse));
        return llm;
    }

    private static IMcpClient BuildMcpClient()
    {
        var mcp = Substitute.For<IMcpClient>();
        var b64Content = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("public class Foo { }"));

        // get_file_contents: returns existing file
        mcp.CallToolAsync(
            Arg.Is<string>("get_file_contents"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               $"{{\"sha\":\"oldsha\",\"content\":\"{b64Content}\"}}",
               false)));

        // create_or_update_file: simulates commit
        mcp.CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               """{"content":{"sha":"newsha123"}}""",
               false)));

        return mcp;
    }

    private static EditingState BuildSut(IMcpClient mcpClient, IChatClient llm) =>
        new(BuildDispatcher(mcpClient), llm, NullLogger<EditingState>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    // EDITING-01: LLM calls write_file → transitions to TestGenerating
    [Fact]
    public async Task ExecuteAsync_LlmCallsWriteFile_TransitionsToTestGenerating()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmWithWriteFile());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.TestGenerating, result.CurrentState);
        Assert.NotNull(result.Edits);
    }

    // EDITING-01: file edit captured in EditContext
    [Fact]
    public async Task ExecuteAsync_LlmCallsWriteFile_PopulatesEditsWithNewSha()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmWithWriteFile());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Single(result.Edits!.Edits);
        Assert.Equal("src/GsdOrchestrator/Foo.cs", result.Edits.Edits[0].Path);
        Assert.Equal("newsha123", result.Edits.Edits[0].NewSha);
    }

    // EDITING-02: create_or_update_file is called with correct file path
    [Fact]
    public async Task ExecuteAsync_LlmCallsWriteFile_CommitsFileViaMcp()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmWithWriteFile());

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Is<JsonObject>(j => j["path"]!.GetValue<string>() == "src/GsdOrchestrator/Foo.cs"),
            Arg.Any<CancellationToken>());
    }

    // EDITING-01: LLM never calls write_file → edit skipped, still transitions to TestGenerating
    [Fact]
    public async Task ExecuteAsync_LlmNeverCallsWriteFile_SkipsFileAndTransitions()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmNoWriteFile());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.TestGenerating, result.CurrentState);
        // No commit issued because write_file was never called
        await mcp.DidNotReceive().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // EDITING-01: cancellation is propagated through MCP call
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

        var sut = BuildSut(mcp, BuildLlmWithWriteFile());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(BuildContext(), cts.Token));
    }
}
