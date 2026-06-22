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

public class IdleStateTests
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
            Issue = new IssueContext(42, "Placeholder title", "", [], "testowner", "testrepo", "main"),
            CurrentState = WorkflowState.Idle
        };

    private static IMcpClient BuildMcpClient(
        string defaultBranch = "main",
        string issueTitle = "Real issue title",
        string[]? labels = null)
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(
            Arg.Is<string>("get_repository"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               "{\"name\":\"testrepo\",\"default_branch\":\"" + defaultBranch + "\"}", false)));

        var labelsJson = "[]";
        if (labels != null && labels.Length > 0)
        {
            var labelItems = string.Join(",", System.Linq.Enumerable.Select(labels, l => "{\"name\":\"" + l + "\"}"));
            labelsJson = "[" + labelItems + "]";
        }

        mcp.CallToolAsync(
            Arg.Is<string>("get_issue"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult(
               "{\"number\":42,\"title\":\"" + issueTitle + "\",\"body\":\"body\",\"labels\":" + labelsJson + "}", false)));
        return mcp;
    }

    private static IdleState BuildSut(IMcpClient mcpClient) =>
        new(BuildDispatcher(mcpClient), NullLogger<IdleState>.Instance);

    [Fact]
    public async Task ExecuteAsync_HappyPath_TransitionsToTriaging()
    {
        var sut = BuildSut(BuildMcpClient());
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal(WorkflowState.Triaging, result.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_UpdatesIssueTitleFromApi()
    {
        var sut = BuildSut(BuildMcpClient(issueTitle: "Real fetched title"));
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal("Real fetched title", result.Issue!.Title);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_CapturesDefaultBranchFromRepo()
    {
        var sut = BuildSut(BuildMcpClient(defaultBranch: "develop"));
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal("develop", result.Issue!.DefaultBranch);
    }

    [Fact]
    public async Task ExecuteAsync_WithLabels_CapturesLabels()
    {
        var sut = BuildSut(BuildMcpClient(labels: new[] { "bug", "priority:high" }));
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Contains("bug", result.Issue!.Labels);
        Assert.Contains("priority:high", result.Issue!.Labels);
    }

    [Fact]
    public async Task ExecuteAsync_CallsGetRepositoryWithCorrectOwnerAndRepo()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp);
        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("get_repository"),
            Arg.Is<JsonObject>(j =>
                j["owner"]!.GetValue<string>() == "testowner" &&
                j["repo"]!.GetValue<string>() == "testrepo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CallsGetIssueWithCorrectArgs()
    {
        var mcp = BuildMcpClient();
        var sut = BuildSut(mcp);
        await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("get_issue"),
            Arg.Is<JsonObject>(j =>
                j["owner"]!.GetValue<string>() == "testowner" &&
                j["repo"]!.GetValue<string>() == "testrepo" &&
                j["issue_number"]!.GetValue<int>() == 42),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingDefaultBranch_FallsBackToMain()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(Arg.Is<string>("get_repository"), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("{}", false)));
        mcp.CallToolAsync(Arg.Is<string>("get_issue"), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("{\"number\":42,\"title\":\"T\",\"body\":\"B\",\"labels\":[]}", false)));
        var sut = BuildSut(mcp);
        var result = await sut.ExecuteAsync(BuildContext(), CancellationToken.None);
        Assert.Equal("main", result.Issue!.DefaultBranch);
    }

    [Fact]
    public async Task ExecuteAsync_GetRepositoryThrows_PropagatesMcpException()
    {
        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(Arg.Is<string>("get_repository"), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
           .ThrowsAsync(new McpException("timeout"));
        var sut = BuildSut(mcp);
        await Assert.ThrowsAsync<McpException>(
            () => sut.ExecuteAsync(BuildContext(), CancellationToken.None));
    }
}
