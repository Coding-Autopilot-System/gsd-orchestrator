using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class AnalyzingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<AnalyzingState> _logger;

    public WorkflowState State => WorkflowState.Analyzing;

    public AnalyzingState(McpToolDispatcher mcp, IChatClient llm, ILogger<AnalyzingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        _logger.LogInformation("Analyzing issue #{Number}: {Title}", issue.Number, issue.Title);

        // Search for related code to give the LLM more context
        var searchResult = await TrySearchCodeAsync(issue, ct);

        var prompt = BuildPrompt(issue, searchResult);

        AnalysisPlan? plan = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var response = await _llm.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Temperature = 0.1f },
                ct);

            var text = response.Text ?? "";
            plan = TryParseAnalysisPlan(text);

            if (plan is not null) break;

            _logger.LogWarning("AnalysisPlan parse failed on attempt {Attempt}/3", attempt);
            prompt += $"\n\nAttempt {attempt} failed to parse. Return ONLY valid JSON, no markdown fences.";
        }

        if (plan is null)
            throw new InvalidOperationException("LLM failed to produce a valid AnalysisPlan after 3 attempts.");

        _logger.LogInformation("Plan: branch={Branch}, files={Count}", plan.BranchName, plan.FilesToModify.Count);
        return (ctx with { Plan = plan }).Transition(WorkflowState.Branching);
    }

    private async Task<string?> TrySearchCodeAsync(IssueContext issue, CancellationToken ct)
    {
        try
        {
            // Extract a keyword from the title for the search
            var keyword = issue.Title.Split(' ').FirstOrDefault(w => w.Length > 4) ?? issue.Title[..Math.Min(20, issue.Title.Length)];
            var result = await _mcp.CallAsync("search_code", new JsonObject
            {
                ["query"] = $"{keyword} repo:{issue.RepoOwner}/{issue.RepoName}"
            }, ct);
            return result.Text;
        }
        catch
        {
            return null; // search is best-effort
        }
    }

    private static string BuildPrompt(IssueContext issue, string? searchContext) =>
        $$"""
        You are a software engineer. Analyze the following GitHub issue and produce an implementation plan.

        Issue #{{issue.Number}}: {{issue.Title}}

        Body:
        {{issue.Body}}

        {{(searchContext is not null ? $"Relevant code search results:\n{searchContext}" : "")}}

        Return ONLY a JSON object (no markdown, no explanation) with this exact structure:
        {
          "branchName": "fix/issue-{{issue.Number}}-short-description",
          "filesToModify": [
            { "path": "relative/path/to/file.cs", "rationale": "why this file needs changing" }
          ],
          "summary": "One sentence describing what the fix does",
          "requiresTests": true
        }

        Rules:
        - branchName must be lowercase, use hyphens, start with fix/ or feat/ or chore/
        - filesToModify must have 1-10 entries
        - Only include files that must change to resolve the issue
        - requiresTests is true if any logic changes require test coverage
        """;

    private static AnalysisPlan? TryParseAnalysisPlan(string text)
    {
        // Strip markdown code fences if present
        text = text.Trim();
        if (text.StartsWith("```")) text = string.Join('\n', text.Split('\n').Skip(1).SkipLast(1));

        try
        {
            var node = JsonNode.Parse(text.Trim());
            if (node is null) return null;

            var files = node["filesToModify"]?.AsArray()
                .Select(f => new PlannedFile(
                    Path: f!["path"]!.GetValue<string>(),
                    Rationale: f["rationale"]?.GetValue<string>() ?? ""))
                .ToList() ?? [];

            return new AnalysisPlan(
                BranchName: node["branchName"]!.GetValue<string>(),
                FilesToModify: files,
                Summary: node["summary"]?.GetValue<string>() ?? "",
                RequiresTests: node["requiresTests"]?.GetValue<bool>() ?? false);
        }
        catch { return null; }
    }
}
