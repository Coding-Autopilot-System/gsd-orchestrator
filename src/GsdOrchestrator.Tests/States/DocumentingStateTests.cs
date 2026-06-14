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

namespace GsdOrchestrator.Tests.States;

public class DocumentingStateTests
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
            Issue = new IssueContext(42, "Fix Foo bug", "body", [], "testowner", "testrepo", "main"),
            Plan = new AnalysisPlan(
                BranchName: "fix/issue-42-foo",
                FilesToModify: [new PlannedFile("src/Foo.cs", "broken")],
                Summary: "Fix null ref in Foo",
                RequiresTests: false),
            Branch = new BranchContext("fix/issue-42-foo", "abc123"),
            Edits = new EditContext([
                new FileEdit("src/Foo.cs", "oldsha", "newsha", "fix(#42): update Foo")
            ]),
            PullRequest = new PullRequestContext(99, "https://github.com/testowner/testrepo/pull/99", "fix: update Foo", "Closes #42"),
            CurrentState = WorkflowState.Documenting
        };

    private static IChatClient BuildLlm()
    {
        var llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new ChatResponse(
               new ChatMessage(ChatRole.Assistant,
                   "## [Unreleased]\n### Fixed\n- #42: Fix null ref in Foo ([#99](https://github.com/testowner/testrepo/pull/99))"))));
        return llm;
    }

    private static IMcpClient BuildMcpClient(bool changelogExists = false)
    {
        var mcp = Substitute.For<IMcpClient>();

        // tools/list for MCP tool catalog
        mcp.ListToolsAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult<IReadOnlyList<McpTool>>(
               [new McpTool("list_issues", "List issues", new JsonObject())]));

        // get_file_contents for docs/github-mcp-tools.md — file might not exist
        mcp.CallToolAsync(
            Arg.Is<string>("get_file_contents"),
            Arg.Is<JsonObject>(j => j["path"] != null && j["path"]!.GetValue<string>() == "docs/github-mcp-tools.md"),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("{}", false)));

        if (changelogExists)
        {
            var b64 = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("# Changelog\n\n## Previous\n- Old entry\n"));
            mcp.CallToolAsync(
                Arg.Is<string>("get_file_contents"),
                Arg.Is<JsonObject>(j => j["path"] != null && j["path"]!.GetValue<string>() == "CHANGELOG.md"),
                Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult(
                   $"{{\"sha\":\"changelogsha\",\"content\":\"{b64}\"}}",
                   false)));
        }
        else
        {
            // CHANGELOG.md doesn't exist — get_file_contents throws McpException
            mcp.CallToolAsync(
                Arg.Is<string>("get_file_contents"),
                Arg.Is<JsonObject>(j => j["path"] != null && j["path"]!.GetValue<string>() == "CHANGELOG.md"),
                Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(new McpToolResult("", true)));
        }

        // create_or_update_file succeeds for both docs files
        mcp.CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("""{"content":{"sha":"newdocsha"}}""", false)));

        return mcp;
    }

    private static DocumentingState BuildSut(
        IMcpClient mcpClient, IChatClient llm, bool autoMerge = false)
    {
        var config = Substitute.For<IConfiguration>();
        config["GSD_AUTO_MERGE"].Returns(autoMerge ? "true" : "false");
        return new DocumentingState(
            BuildDispatcher(mcpClient),
            llm,
            config,
            NullLogger<DocumentingState>.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    // DOCUMENTING-01: transitions to Done
    [Fact]
    public async Task ExecuteAsync_HappyPath_TransitionsToDone()
    {
        var sut = BuildSut(BuildMcpClient(), BuildLlm());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Done, result.CurrentState);
    }

    // DOCUMENTING-01: creates_or_updates docs/github-mcp-tools.md
    [Fact]
    public async Task ExecuteAsync_HappyPath_CommitsMcpToolCatalog()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm());

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Is<JsonObject>(j => j["path"]!.GetValue<string>() == "docs/github-mcp-tools.md"),
            Arg.Any<CancellationToken>());
    }

    // DOCUMENTING-01: creates_or_updates CHANGELOG.md
    [Fact]
    public async Task ExecuteAsync_HappyPath_CommitsChangelog()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm());

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Is<JsonObject>(j => j["path"]!.GetValue<string>() == "CHANGELOG.md"),
            Arg.Any<CancellationToken>());
    }

    // DOCUMENTING-02: auto-merge is called when GSD_AUTO_MERGE=true
    [Fact]
    public async Task ExecuteAsync_AutoMergeEnabled_CallsMergePullRequest()
    {
        var mcp = BuildMcpClient();
        // Also stub merge_pull_request
        mcp.CallToolAsync(
            Arg.Is<string>("merge_pull_request"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("{}", false)));

        var sut = BuildSut(mcp, BuildLlm(), autoMerge: true);

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.Received().CallToolAsync(
            Arg.Is<string>("merge_pull_request"),
            Arg.Is<JsonObject>(j => j["pullNumber"]!.GetValue<int>() == 99),
            Arg.Any<CancellationToken>());
    }

    // DOCUMENTING-02: when auto-merge is false, merge_pull_request is NOT called
    [Fact]
    public async Task ExecuteAsync_AutoMergeDisabled_DoesNotCallMergePullRequest()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp, BuildLlm(), autoMerge: false);

        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        await mcp.DidNotReceive().CallToolAsync(
            Arg.Is<string>("merge_pull_request"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // DOCUMENTING-01: existing CHANGELOG.md is read and prepended
    [Fact]
    public async Task ExecuteAsync_ExistingChangelog_PrependsNewEntry()
    {
        var mcp = BuildMcpClient(changelogExists: true);
        var sut = BuildSut(mcp, BuildLlm());

        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);

        Assert.Equal(WorkflowState.Done, result.CurrentState);
        // create_or_update_file should have been called with existing sha
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("create_or_update_file"),
            Arg.Is<JsonObject>(j =>
                j["path"]!.GetValue<string>() == "CHANGELOG.md" &&
                j["sha"] != null),
            Arg.Any<CancellationToken>());
    }
}
