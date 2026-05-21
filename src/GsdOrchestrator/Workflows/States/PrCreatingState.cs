using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class PrCreatingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<PrCreatingState> _logger;

    public WorkflowState State => WorkflowState.PrCreating;

    public PrCreatingState(McpToolDispatcher mcp, IChatClient llm, ILogger<PrCreatingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        var plan = ctx.Plan!;
        var branch = ctx.Branch!;

        // Idempotency: check for existing PR from this branch
        var existingPrsResult = await _mcp.CallAsync("list_pull_requests", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["state"] = "open",
            ["head"] = $"{issue.RepoOwner}:{branch.BranchName}"
        }, ct);

        var existingPrs = existingPrsResult.ParseInnerJson()?.AsArray();
        if (existingPrs?.Count > 0)
        {
            var existing = existingPrs[0]!;
            var prCtx = new PullRequestContext(
                PrNumber: existing["number"]!.GetValue<int>(),
                PrUrl: existing["html_url"]!.GetValue<string>(),
                Title: existing["title"]!.GetValue<string>(),
                Body: existing["body"]?.GetValue<string>() ?? "");

            _logger.LogInformation("PR already exists: #{Number} — resuming", prCtx.PrNumber);
            return (ctx with { PullRequest = prCtx }).Transition(WorkflowState.Reviewing);
        }

        // Generate PR title and body via LLM (structured output)
        var (title, body) = await GeneratePrDraftAsync(issue, plan, ctx.Edits!, ct);

        var createResult = await _mcp.CallAsync("create_pull_request", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["title"] = title,
            ["body"] = body,
            ["head"] = branch.BranchName,
            ["base"] = issue.DefaultBranch
        }, ct);

        var prJson = createResult.ParseInnerJson();
        var pr = new PullRequestContext(
            PrNumber: prJson?["number"]?.GetValue<int>() ?? 0,
            PrUrl: prJson?["html_url"]?.GetValue<string>() ?? "",
            Title: title,
            Body: body);

        _logger.LogInformation("PR created: #{Number} — {Url}", pr.PrNumber, pr.PrUrl);
        return (ctx with { PullRequest = pr }).Transition(WorkflowState.Reviewing);
    }

    private async Task<(string Title, string Body)> GeneratePrDraftAsync(
        IssueContext issue, AnalysisPlan plan, EditContext edits, CancellationToken ct)
    {
        var filesChanged = string.Join(", ", edits.Edits.Select(e => $"`{e.Path}`"));
        var prompt = $"""
            Generate a GitHub pull request title and body for the following change.
            Return ONLY a JSON object with "title" and "body" fields — no markdown fences.

            Issue #{issue.Number}: {issue.Title}
            Summary: {plan.Summary}
            Files changed: {filesChanged}

            Requirements:
            - title: concise, ≤72 chars, imperative mood
            - body: include "## What", "## Why", a checklist of files changed, and end with "Closes #{issue.Number}"
            """;

        var response = await _llm.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { Temperature = 0.2f },
            ct);

        var text = (response.Text ?? "").Trim().TrimStart('`').TrimEnd('`');
        if (text.StartsWith("json\n")) text = text[5..];

        try
        {
            var node = JsonNode.Parse(text);
            var title = node?["title"]?.GetValue<string>() ?? $"fix(#{issue.Number}): {plan.Summary}";
            var body = node?["body"]?.GetValue<string>() ?? $"Closes #{issue.Number}";
            return (title, body);
        }
        catch
        {
            return ($"fix(#{issue.Number}): {plan.Summary}", $"Closes #{issue.Number}");
        }
    }
}
