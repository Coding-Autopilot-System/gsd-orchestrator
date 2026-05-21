using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class BranchingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly ILogger<BranchingState> _logger;

    public WorkflowState State => WorkflowState.Branching;

    public BranchingState(McpToolDispatcher mcp, ILogger<BranchingState> logger)
    {
        _mcp = mcp;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        var plan = ctx.Plan!;

        _logger.LogInformation("Creating branch: {Branch}", plan.BranchName);

        // Idempotency: check if branch already exists (resume scenario)
        var branchesResult = await _mcp.CallAsync("list_branches", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName
        }, ct);

        var branchesJson = branchesResult.ParseInnerJson();
        var existingBranch = branchesJson?.AsArray()
            .Any(b => b?["name"]?.GetValue<string>() == plan.BranchName) ?? false;

        string baseSha;
        bool wasResumed = false;

        if (existingBranch)
        {
            _logger.LogInformation("Branch already exists — resuming from existing branch");
            wasResumed = true;

            // Get current SHA of the existing branch
            var branchResult = await _mcp.CallAsync("get_branch", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["branch"] = plan.BranchName
            }, ct);
            baseSha = branchResult.ParseInnerJson()?["commit"]?["sha"]?.GetValue<string>()
                ?? throw new McpException("Could not determine SHA of existing branch");
        }
        else
        {
            // Get the SHA of the default branch to branch from
            var refResult = await _mcp.CallAsync("get_branch", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["branch"] = issue.DefaultBranch
            }, ct);

            baseSha = refResult.ParseInnerJson()?["commit"]?["sha"]?.GetValue<string>()
                ?? throw new McpException("Could not get SHA for default branch");

            await _mcp.CallAsync("create_branch", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["branch"] = plan.BranchName,
                ["from_branch"] = issue.DefaultBranch
            }, ct);

            _logger.LogInformation("Branch created: {Branch} from {Default}@{Sha}",
                plan.BranchName, issue.DefaultBranch, baseSha[..8]);
        }

        var branch = new BranchContext(plan.BranchName, baseSha, wasResumed);
        return (ctx with { Branch = branch }).Transition(WorkflowState.Editing);
    }
}
