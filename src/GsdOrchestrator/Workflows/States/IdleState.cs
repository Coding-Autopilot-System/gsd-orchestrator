using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class IdleState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly ILogger<IdleState> _logger;

    public WorkflowState State => WorkflowState.Idle;

    public IdleState(McpToolDispatcher mcp, ILogger<IdleState> logger)
    {
        _mcp = mcp;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var owner = ctx.Issue!.RepoOwner;
        var repo = ctx.Issue!.RepoName;
        _logger.LogInformation("Fetching issue #{Number} from {Owner}/{Repo}", ctx.Issue.Number, owner, repo);

        // get_repository to confirm repo exists and get default branch
        var repoResult = await _mcp.CallAsync("get_repository", new JsonObject
        {
            ["owner"] = owner,
            ["repo"] = repo
        }, ct);

        var repoJson = repoResult.ParseInnerJson();
        var defaultBranch = repoJson?["default_branch"]?.GetValue<string>() ?? "main";

        // get_issue for the main issue content
        var issueResult = await _mcp.CallAsync("get_issue", new JsonObject
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["issue_number"] = ctx.Issue!.Number
        }, ct);

        var issueJson = issueResult.ParseInnerJson();
        var labels = issueJson?["labels"]?.AsArray()
            .Select(l => l?["name"]?.GetValue<string>() ?? "")
            .Where(l => l.Length > 0)
            .ToList() ?? [];

        var issue = new IssueContext(
            Number: ctx.Issue.Number,
            Title: issueJson?["title"]?.GetValue<string>() ?? ctx.Issue.Title,
            Body: issueJson?["body"]?.GetValue<string>() ?? "",
            Labels: labels,
            RepoOwner: owner,
            RepoName: repo,
            DefaultBranch: defaultBranch);

        _logger.LogInformation("Issue fetched: \"{Title}\"", issue.Title);
        return (ctx with { Issue = issue }).Transition(WorkflowState.Triaging);
    }
}
