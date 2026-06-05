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

public class TestGeneratingStateTests
{
    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    private static GsdWorkflowContext BuildContext() =>
        new()
        {
            Issue = new IssueContext(42, "Test issue", "Body text", [], "testowner", "testrepo", "main"),
            Branch = new BranchContext("fix/issue-42", "abc123sha"),
            Edits = new EditContext([
                new FileEdit(
                    "src/GsdOrchestrator/Workflows/States/FooState.cs",
                    "oldsha123", "newsha456",
                    "fix(#42): update FooState")
            ]),
            CurrentState = WorkflowState.TestGenerating
        };

    // Returns IChatClient mock that always simulates a write_file tool call.
    // The ReAct loop exits immediately after write_file content is captured (finalContent != null),
    // so returning toolCallResponse on every call correctly handles both single and multi-file scenarios.
    private static IChatClient BuildLlmWithToolCall()
    {
        var llm = Substitute.For<IChatClient>();
        var functionCall = new FunctionCallContent(
            "call_001",
            "write_file",
            new Dictionary<string, object?>
            {
                ["content"] = "using Xunit;\n[Fact] public void Placeholder() {}",
                ["commitMessage"] = "test: generate"
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

    // Returns IChatClient mock that never calls write_file (always returns Stop).
    private static IChatClient BuildLlmNoToolCall()
    {
        var llm = Substitute.For<IChatClient>();
        var stopResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "no tests needed")) { FinishReason = ChatFinishReason.Stop };
        llm.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(stopResponse));
        return llm;
    }

    // Returns IMcpClient that stubs get_file_contents and create_or_update_file.
    // When testFileExists=true, the second get_file_contents call (test file) returns existing sha.
    private static IMcpClient BuildMcpClient(bool sourceFileExists = true, bool testFileExists = false)
    {
        var mcp = Substitute.For<IMcpClient>();

        var sourceContent = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("public class FooState {}"));
        var existingTestContent = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("[Fact] public void Existing() {}"));

        if (testFileExists)
        {
            // Source file (non-.Tests path) returns source content
            mcp.CallToolAsync(
                    Arg.Is<string>("get_file_contents"),
                    Arg.Is<JsonObject>(j => j["path"] != null && !j["path"]!.GetValue<string>().Contains(".Tests")),
                    Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult(
                   $"{{\"sha\":\"srcsha123\",\"content\":\"{sourceContent}\"}}",
                   false)));

            // Test file (.Tests path) returns existing sha
            mcp.CallToolAsync(
                    Arg.Is<string>("get_file_contents"),
                    Arg.Is<JsonObject>(j => j["path"] != null && j["path"]!.GetValue<string>().Contains(".Tests")),
                    Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult(
                   $"{{\"sha\":\"existingsha\",\"content\":\"{existingTestContent}\"}}",
                   false)));
        }
        else
        {
            // Any get_file_contents returns source content
            mcp.CallToolAsync(
                    Arg.Is<string>("get_file_contents"),
                    Arg.Any<JsonObject>(),
                    Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult(
                   $"{{\"sha\":\"srcsha123\",\"content\":\"{sourceContent}\"}}",
                   false)));
        }

        // Test file commit
        mcp.CallToolAsync(
                Arg.Is<string>("create_or_update_file"),
                Arg.Any<JsonObject>(),
                Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               """{"content":{"sha":"testsha789"}}""",
               false)));

        return mcp;
    }

    private static TestGeneratingState BuildSut(IMcpClient mcpClient, IChatClient llm) =>
        new(BuildDispatcher(mcpClient), llm, NullLogger<TestGeneratingState>.Instance);

    // ── Test 1: TESTGEN-01 — happy path transitions to Validating ──────────
    [Fact]
    public async Task ExecuteAsync_WithEditableCSharpFile_TransitionsToValidating()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmWithToolCall());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Validating, result.CurrentState);
    }

    // ── Test 2: TESTGEN-02 — create_or_update_file called with derived test path ─
    [Fact]
    public async Task ExecuteAsync_WithEditableCSharpFile_CommitsTestFile()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlmWithToolCall());
        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Is<JsonObject>(j => j["path"]!.GetValue<string>() == "src/GsdOrchestrator.Tests/FooStateTests.cs"),
            Arg.Any<CancellationToken>());
    }

    // ── Test 3: TESTGEN-01 — non-.cs edits produce empty GeneratedTests ────
    [Fact]
    public async Task ExecuteAsync_WithNoTestableFiles_SkipsGracefully()
    {
        var ctx = new GsdWorkflowContext
        {
            Issue = new IssueContext(42, "Test", "Body", [], "testowner", "testrepo", "main"),
            Branch = new BranchContext("fix/issue-42", "abc123sha"),
            Edits = new EditContext([
                new FileEdit("config/settings.json", "old", "new", "chore: update config")
            ]),
            CurrentState = WorkflowState.TestGenerating
        };
        var sut = BuildSut(BuildMcpClient(), BuildLlmNoToolCall());
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Validating, result.CurrentState);
        Assert.Empty(result.TestGeneration!.GeneratedTests);
    }

    // ── Test 4: TESTGEN-01 — .Tests/ path is filtered out ──────────────────
    [Fact]
    public async Task ExecuteAsync_WithTestProjectFile_SkipsFile()
    {
        var ctx = new GsdWorkflowContext
        {
            Issue = new IssueContext(42, "Test", "Body", [], "testowner", "testrepo", "main"),
            Branch = new BranchContext("fix/issue-42", "abc123sha"),
            Edits = new EditContext([
                new FileEdit(
                    "src/GsdOrchestrator.Tests/ExistingTests.cs",
                    "old", "new", "test: update")
            ]),
            CurrentState = WorkflowState.TestGenerating
        };
        var sut = BuildSut(BuildMcpClient(), BuildLlmNoToolCall());
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Validating, result.CurrentState);
        Assert.Empty(result.TestGeneration!.GeneratedTests);
    }

    // ── Test 5: TESTGEN-01 — LLM never calls write_file → WasSkipped=true ─
    [Fact]
    public async Task ExecuteAsync_LlmNeverCallsWriteFile_ProducesSkippedResult()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlmNoToolCall());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Validating, result.CurrentState);
        Assert.Single(result.TestGeneration!.GeneratedTests);
        Assert.True(result.TestGeneration.GeneratedTests[0].WasSkipped);
    }

    // ── Test 6: TESTGEN-02 — existing test file SHA passed to commit ────────
    [Fact]
    public async Task ExecuteAsync_WithExistingTestFile_ReadsExistingSha()
    {
        var mcp = BuildMcpClient(testFileExists: true);
        var sut = BuildSut(mcp, BuildLlmWithToolCall());
        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        // create_or_update_file must include sha field for the existing file
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Is<JsonObject>(j => j["sha"] != null),
            Arg.Any<CancellationToken>());
    }

    // ── Test 7: TESTGEN-01 — multiple .cs edits generate test for each ──────
    [Fact]
    public async Task ExecuteAsync_WithMultipleEditableFiles_GeneratesTestForEach()
    {
        var ctx = new GsdWorkflowContext
        {
            Issue = new IssueContext(42, "Test", "Body", [], "testowner", "testrepo", "main"),
            Branch = new BranchContext("fix/issue-42", "abc123sha"),
            Edits = new EditContext([
                new FileEdit("src/GsdOrchestrator/States/FooState.cs", "o1", "n1", "fix: foo"),
                new FileEdit("src/GsdOrchestrator/States/BarState.cs", "o2", "n2", "fix: bar")
            ]),
            CurrentState = WorkflowState.TestGenerating
        };
        var mcp = BuildMcpClient();
        var llm = BuildLlmWithToolCall();
        var sut = BuildSut(mcp, llm);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(2, result.TestGeneration!.GeneratedTests.Count);
        await mcp.Received(2).CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }
}
