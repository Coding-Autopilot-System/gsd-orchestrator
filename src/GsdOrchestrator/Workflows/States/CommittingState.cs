using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

/// <summary>
/// Verifies the final commit on the branch. In the GitHub API model, each
/// create_or_update_file call already commits — this state confirms the last
/// commit SHA is present and records it.
/// </summary>
public sealed class CommittingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly ILogger<CommittingState> _logger;

    public WorkflowState State => WorkflowState.Committing;

    public CommittingState(McpToolDispatcher mcp, ILogger<CommittingState> logger)
    {
        _mcp = mcp;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        var branch = ctx.Branch!;
        var edits = ctx.Edits!;

        // Get the latest commit on the branch to confirm everything is there
        var branchResult = await _mcp.CallAsync("get_branch", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["branch"] = branch.BranchName
        }, ct);

        var sha = branchResult.ParseInnerJson()?["commit"]?["sha"]?.GetValue<string>()
            ?? edits.Edits.LastOrDefault()?.NewSha
            ?? branch.BaseSha;

        var commitUrl = $"https://github.com/{issue.RepoOwner}/{issue.RepoName}/commit/{sha}";
        _logger.LogInformation("Final commit: {Sha}", sha[..Math.Min(8, sha.Length)]);

        var commit = new CommitContext(sha, commitUrl);
        return (ctx with { Commit = commit }).Transition(WorkflowState.PrCreating);
    }
}
