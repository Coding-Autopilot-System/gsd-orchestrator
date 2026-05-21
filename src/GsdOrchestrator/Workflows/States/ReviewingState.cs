using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class ReviewingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<ReviewingState> _logger;
    private readonly IReadOnlyList<string> _reviewers;

    public WorkflowState State => WorkflowState.Reviewing;

    public ReviewingState(
        McpToolDispatcher mcp,
        IChatClient llm,
        IConfiguration config,
        ILogger<ReviewingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;

        var raw = config["GSD_REVIEWERS"] ?? "";
        _reviewers = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        var pr = ctx.PullRequest!;
        var plan = ctx.Plan!;
        var edits = ctx.Edits!;

        // Post a self-review comment explaining what the automation did
        var comment = await GenerateReviewCommentAsync(issue, plan, edits, pr, ct);

        await _mcp.CallAsync("add_pull_request_review_comment", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["pullNumber"] = pr.PrNumber,
            ["body"] = comment
        }, ct);

        // Request reviewers if configured
        if (_reviewers.Count > 0)
        {
            try
            {
                var reviewersArray = new JsonArray();
                foreach (var r in _reviewers) reviewersArray.Add(r);

                await _mcp.CallAsync("request_reviewers", new JsonObject
                {
                    ["owner"] = issue.RepoOwner,
                    ["repo"] = issue.RepoName,
                    ["pullNumber"] = pr.PrNumber,
                    ["reviewers"] = reviewersArray
                }, ct);

                _logger.LogInformation("Requested review from: {Reviewers}", string.Join(", ", _reviewers));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to request reviewers — continuing");
            }
        }

        _logger.LogInformation("Review comment posted on PR #{Number}", pr.PrNumber);
        return ctx.Transition(WorkflowState.Documenting);
    }

    private async Task<string> GenerateReviewCommentAsync(
        IssueContext issue, AnalysisPlan plan, EditContext edits,
        PullRequestContext pr, CancellationToken ct)
    {
        var filesList = string.Join("\n", edits.Edits.Select(e => $"- `{e.Path}`: {plan.FilesToModify.FirstOrDefault(f => f.Path == e.Path)?.Rationale ?? "updated"}"));

        var prompt = $"""
            Write a short, friendly GitHub PR comment (2-4 sentences) from a bot explaining:
            - What was automatically changed and why
            - Which files were modified
            - A note asking the reviewer to verify the changes

            Issue: #{issue.Number} — {issue.Title}
            Plan: {plan.Summary}
            Files modified:
            {filesList}
            """;

        var response = await _llm.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { Temperature = 0.4f },
            ct);

        var text = response.Text ?? "Automated changes applied. Please review.";
        return $"🤖 **GSD Orchestrator**\n\n{text}";
    }
}
