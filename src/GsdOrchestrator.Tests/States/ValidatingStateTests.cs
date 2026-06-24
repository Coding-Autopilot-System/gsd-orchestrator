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

public class ValidatingStateTests
{
    private static McpToolDispatcher BuildDispatcher(IMcpClient mcpClient)
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(mcpClient, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    private static IMcpClient BuildMcpClientAhead()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("compare_commits"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(@"{""status"":""ahead"",""files"":[]}", false)));
        return mcp;
    }

    private static IMcpClient BuildMcpClientDivergedNoOverlap()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("compare_commits"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               @"{""status"":""diverged"",""files"":[{""filename"":""other/File.cs""}]}", false)));
        return mcp;
    }

    private static IMcpClient BuildMcpClientDivergedWithOverlap()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("compare_commits"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               @"{""status"":""diverged"",""files"":[{""filename"":""src/Foo.cs""}]}", false)));
        return mcp;
    }

    private static ValidatingState BuildSut(IMcpClient mcpClient) =>
        new(BuildDispatcher(mcpClient), NullLogger<ValidatingState>.Instance);

    private static GsdWorkflowContext BuildContext(
        IReadOnlyList<FileEdit>? edits = null,
        bool requiresTests = false,
        TestGenerationContext? testGeneration = null) =>
        new()
        {
            Issue = new IssueContext(42, "Fix Foo", "body", [], "testowner", "testrepo", "main"),
            Branch = new BranchContext("fix/issue-42", "abc123"),
            Plan = new AnalysisPlan("fix/issue-42", [new PlannedFile("src/Foo.cs", "fix")], "Fix foo", requiresTests),
            Edits = new EditContext(edits ?? [new FileEdit("src/Foo.cs", "oldsha", "newsha", "fix: foo")]),
            TestGeneration = testGeneration,
            CurrentState = WorkflowState.Validating
        };

    [Fact]
    public async Task ExecuteAsync_CleanEditsNoDivergence_TransitionsToCommitting()
    {
        var sut = BuildSut(BuildMcpClientAhead());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_CleanEdits_StoresValidationResultInContext()
    {
        var sut = BuildSut(BuildMcpClientAhead());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.NotNull(result.Validation);
        Assert.Equal(ValidationStatus.Pass, result.Validation!.Status);
    }

    [Fact]
    public async Task ExecuteAsync_BlockedWorkflowPath_TransitionsToFailed()
    {
        var mcp = Substitute.For<IMcpClient>();
        var sut = BuildSut(mcp);
        var ctx = BuildContext(edits: [new FileEdit(".github/workflows/ci.yml", "old", "new", "chore: ci")]);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Failed, result.CurrentState);
        Assert.Contains("File safety", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_BlockedPemExtension_TransitionsToFailed()
    {
        var mcp = Substitute.For<IMcpClient>();
        var sut = BuildSut(mcp);
        var ctx = BuildContext(edits: [new FileEdit("secrets/cert.pem", "old", "new", "chore: cert")]);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Failed, result.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_DivergenceWithFileOverlap_TransitionsToFailed()
    {
        var sut = BuildSut(BuildMcpClientDivergedWithOverlap());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Failed, result.CurrentState);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_DivergenceNoOverlap_TransitionsToCommitting()
    {
        var sut = BuildSut(BuildMcpClientDivergedNoOverlap());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_CompareCommitsThrows_WarnsAndContinuesToCommitting()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("compare_commits"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("network error"));
        var sut = BuildSut(mcp);
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "MergeConflict" && g.Status == ValidationStatus.Warn);
    }

    [Fact]
    public async Task ExecuteAsync_PlanRequiresTestsButNoTestFiles_AddsWarnGate()
    {
        var sut = BuildSut(BuildMcpClientAhead());
        var ctx = BuildContext(requiresTests: true);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestIntent" && g.Status == ValidationStatus.Warn);
    }

    [Fact]
    public async Task ExecuteAsync_PlanRequiresTestsAndTestFilePresent_PassesTestIntentGate()
    {
        var sut = BuildSut(BuildMcpClientAhead());
        var ctx = BuildContext(
            requiresTests: true,
            edits: [
                new FileEdit("src/Foo.cs", "old", "new", "fix: foo"),
                new FileEdit("src/FooTests.cs", "old2", "new2", "test: foo")
            ]);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestIntent" && g.Status == ValidationStatus.Pass);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedTestsPresent_PassesTestIntentGate()
    {
        // Use a client that also mocks get_file_contents (Gate 5 reads the generated test file)
        var mcp = BuildMcpClientWithFileContent("[Fact] public void Test() {}");
        var sut = BuildSut(mcp);
        var testGen = new TestGenerationContext([
            new GeneratedTest("src/Foo.cs", "src/FooTests.cs", "sha", WasSkipped: false, SkipReason: null)
        ]);
        var ctx = BuildContext(requiresTests: true, testGeneration: testGen);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestIntent" && g.Status == ValidationStatus.Pass);
    }

    private static IMcpClient BuildMcpClientWithFileContent(string fileContent)
    {
        var mcp = BuildMcpClientAhead();
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));
        var json = string.Format("{{\"content\":\"{0}\"}}", base64);
        var result = new McpToolResult(json, false);
        mcp.CallToolAsync(
            Arg.Is<string>("get_file_contents"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(_ => Task.FromResult(result));
        return mcp;
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedTestFileHasFactAttribute_PassesTestCompilationGate()
    {
        var mcp = BuildMcpClientWithFileContent("[Fact] public void Test() {}");
        var sut = BuildSut(mcp);
        var testGen = new TestGenerationContext([
            new GeneratedTest("src/Foo.cs", "src/FooTests.cs", "sha", WasSkipped: false, SkipReason: null)
        ]);
        var result = await sut.ExecuteAsync(BuildContext(testGeneration: testGen), CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestCompilation" && g.Status == ValidationStatus.Pass);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedTestFileMissingFactAttribute_AddsWarnCompilationGate()
    {
        var mcp = BuildMcpClientWithFileContent("public class NoAttributes {}");
        var sut = BuildSut(mcp);
        var testGen = new TestGenerationContext([
            new GeneratedTest("src/Foo.cs", "src/FooTests.cs", "sha", WasSkipped: false, SkipReason: null)
        ]);
        var result = await sut.ExecuteAsync(BuildContext(testGeneration: testGen), CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestCompilation" && g.Status == ValidationStatus.Warn);
    }

    [Fact]
    public async Task ExecuteAsync_TestFileNotFoundOnBranch_AddsWarnCompilationGate()
    {
        var mcp = BuildMcpClientAhead();
        mcp.CallToolAsync(
            Arg.Is<string>("get_file_contents"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("Not found", isTransient: false));
        var sut = BuildSut(mcp);
        var testGen = new TestGenerationContext([
            new GeneratedTest("src/Foo.cs", "src/FooTests.cs", "sha", WasSkipped: false, SkipReason: null)
        ]);
        var result = await sut.ExecuteAsync(BuildContext(testGeneration: testGen), CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestCompilation" && g.Status == ValidationStatus.Warn);
    }

    [Fact]
    public async Task ExecuteAsync_AllGeneratedTestsSkipped_PassesCompilationGateSilently()
    {
        var sut = BuildSut(BuildMcpClientAhead());
        var testGen = new TestGenerationContext([
            new GeneratedTest("src/Foo.cs", "src/FooTests.cs", "", WasSkipped: true, SkipReason: "no tests needed")
        ]);
        var ctx = BuildContext(testGeneration: testGen);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Committing, result.CurrentState);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestCompilation" && g.Status == ValidationStatus.Pass);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedTestFileHasTheoryAttribute_PassesTestCompilationGate()
    {
        var mcp = BuildMcpClientWithFileContent("[Theory][InlineData(1)] public void Test(int x) {}");
        var sut = BuildSut(mcp);
        var testGen = new TestGenerationContext([
            new GeneratedTest("src/Foo.cs", "src/FooTests.cs", "sha", WasSkipped: false, SkipReason: null)
        ]);
        var result = await sut.ExecuteAsync(BuildContext(testGeneration: testGen), CancellationToken.None);
        Assert.Contains(result.Validation!.Gates, g => g.Gate == "TestCompilation" && g.Status == ValidationStatus.Pass);
    }

    [Theory]
    [InlineData("secrets/private.key")]
    [InlineData("certs/cert.p12")]
    [InlineData("certs/cert.pfx")]
    [InlineData("certs/root.cer")]
    [InlineData("certs/chain.crt")]
    public async Task ExecuteAsync_BlockedCertificateExtension_TransitionsToFailed(string blockedPath)
    {
        var mcp = Substitute.For<IMcpClient>();
        var sut = BuildSut(mcp);
        var ctx = BuildContext(edits: [new FileEdit(blockedPath, "old", "new", "chore: cert")]);
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);
        Assert.Equal(WorkflowState.Failed, result.CurrentState);
    }
}
