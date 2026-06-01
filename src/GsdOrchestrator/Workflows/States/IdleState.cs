using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class IdleState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly ILogger<IdleState> _logger;
    private readonly string _owner;
    private readonly string _repo;

    public WorkflowState State => WorkflowState.Idle;

    public IdleState(McpToolDispatcher mcp, IConfiguration config, ILogger<IdleState> logger)
    {
        _mcp = mcp;
        _logger = logger;
        _owner = config["GSD_GITHUB_OWNER"] ?? throw new InvalidOperationException("GSD_GITHUB_OWNER not set");
        _repo = config["GSD_GITHUB_REPO"] ?? throw new InvalidOperationException("GSD_GITHUB_REPO not set");
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("Fetching issue #{Number} from {Owner}/{Repo}", ctx.Issue?.Number, _owner, _repo);

        // get_repository to confirm repo exists and get default branch
        var repoResult = await _mcp.CallAsync("get_repository", new JsonObject
        {
            ["owner"] = _owner,
            ["repo"] = _repo
        }, ct);

        var repoJson = repoResult.ParseInnerJson();
        var defaultBranch = repoJson?["default_branch"]?.GetValue<string>() ?? "main";

        // get_issue for the main issue content
        var issueResult = await _mcp.CallAsync("get_issue", new JsonObject
        {
            ["owner"] = _owner,
            ["repo"] = _repo,
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
            RepoOwner: _owner,
            RepoName: _repo,
            DefaultBranch: defaultBranch);

        _logger.LogInformation("Issue fetched: \"{Title}\"", issue.Title);
        return (ctx with { Issue = issue }).Transition(WorkflowState.Triaging);
    }
}
