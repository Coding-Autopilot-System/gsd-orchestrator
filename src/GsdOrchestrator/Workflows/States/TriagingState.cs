using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class TriagingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<TriagingState> _logger;

    public WorkflowState State => WorkflowState.Triaging;

    public TriagingState(McpToolDispatcher mcp, IChatClient llm, ILogger<TriagingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        _logger.LogInformation("Triaging issue #{Number}: {Title}", issue.Number, issue.Title);

        // 1. Fetch open issues for duplicate detection context
        string openIssuesSummary = await FetchOpenIssuesSummaryAsync(issue, ct);

        // 2. Build classification prompt
        var prompt = BuildTriagePrompt(issue, openIssuesSummary);

        // 3. LLM classification with retry-on-parse-failure (AnalyzingState pattern)
        TriageResult? triageResult = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var response = await _llm.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Temperature = 0.1f },
                ct);

            var text = response.Text ?? "";
            triageResult = TryParseTriageResult(text);

            if (triageResult is not null) break;

            _logger.LogWarning("TriageResult parse failed on attempt {Attempt}/3", attempt);
            prompt += $"\n\nAttempt {attempt} failed to parse. Return ONLY valid JSON, no markdown fences.";
        }

        if (triageResult is null)
            throw new InvalidOperationException("LLM failed to produce a valid TriageResult after 3 attempts.");

        _logger.LogInformation("Triage result: #{Number} = {Classification}", issue.Number, triageResult.Classification);

        // 4. Post triage comment on the issue
        await PostTriageCommentAsync(issue, triageResult, ct);

        // 5. Handle skip logic for non-actionable classifications
        if (triageResult.Classification is "duplicate" or "out-of-scope" or "needs-info")
        {
            await TryCloseIssueAsync(issue, triageResult, ct);
        }

        // 6. Determine next state
        //    - TriageModeOnly: always Done (triage-only CLI mode)
        //    - actionable (and not triage-only): proceed to Analyzing
        //    - needs-info / duplicate / out-of-scope: Done
        var nextState = !ctx.TriageModeOnly && triageResult.Classification == "actionable"
            ? WorkflowState.Analyzing
            : WorkflowState.Done;

        return (ctx with { Triage = triageResult }).Transition(nextState);
    }

    private async Task<string> FetchOpenIssuesSummaryAsync(IssueContext issue, CancellationToken ct)
    {
        try
        {
            var issuesResult = await _mcp.CallAsync("list_issues", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["state"] = "open",
                ["perPage"] = 50
            }, ct);

            var openIssues = issuesResult.ParseInnerJson()?.AsArray() ?? [];
            var lines = openIssues
                .Where(i => i?["number"]?.GetValue<int>() != issue.Number)
                .Select(i => $"- #{i?["number"]}: {i?["title"]?.GetValue<string>()}")
                .ToList();

            return lines.Count > 0
                ? "Currently open issues:\n" + string.Join("\n", lines)
                : "No other open issues.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch open issues for duplicate context — continuing without");
            return "Could not retrieve open issues list.";
        }
    }

    private static string BuildTriagePrompt(IssueContext issue, string openIssuesSummary) =>
        $$"""
        You are a software issue triage bot. Classify the following GitHub issue.

        Issue #{{issue.Number}}: {{issue.Title}}
        Body:
        {{issue.Body}}
        Labels: {{string.Join(", ", issue.Labels)}}

        {{openIssuesSummary}}

        Return ONLY a JSON object (no markdown, no explanation):
        {
          "classification": "actionable" | "needs-info" | "duplicate" | "out-of-scope",
          "reason": "one sentence explanation",
          "duplicateNumber": null
        }

        Set duplicateNumber to the issue number if classification is "duplicate".

        Definitions:
        - actionable: clear, specific, reproducible — ready for implementation
        - needs-info: too vague, missing steps to reproduce, or requires clarification
        - duplicate: same problem as another open issue (duplicateNumber required)
        - out-of-scope: feature request outside project goals, or spam
        """;

    private static TriageResult? TryParseTriageResult(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```")) text = string.Join('\n', text.Split('\n').Skip(1).SkipLast(1));

        try
        {
            var node = JsonNode.Parse(text.Trim());
            if (node is null) return null;

            var classification = node["classification"]?.GetValue<string>() ?? "";
            if (classification is not ("actionable" or "needs-info" or "duplicate" or "out-of-scope"))
                return null; // treat as parse failure — retry will fire

            return new TriageResult(
                Classification: classification,
                Reason: node["reason"]?.GetValue<string>() ?? "",
                DuplicateNumber: node["duplicateNumber"] is JsonValue dupVal
                    ? dupVal.GetValue<int>()
                    : (int?)null);
        }
        catch { return null; }
    }

    private async Task PostTriageCommentAsync(IssueContext issue, TriageResult triage, CancellationToken ct)
    {
        var duplicateRef = triage.DuplicateNumber.HasValue ? $"\nDuplicate of: #{triage.DuplicateNumber.Value}" : "";
        var body = $"**GSD Triage** — Classification: `{triage.Classification}`\n\n{triage.Reason}{duplicateRef}";

        await _mcp.CallAsync("add_issue_comment", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["issue_number"] = issue.Number,
            ["body"] = body
        }, ct);
    }

    private async Task TryCloseIssueAsync(IssueContext issue, TriageResult triage, CancellationToken ct)
    {
        try
        {
            // Pitfall 2: update_issue tool name is LOW confidence — wrap in try/catch
            await _mcp.CallAsync("update_issue", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["issue_number"] = issue.Number,
                ["state"] = "closed"
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "update_issue call failed for #{Number} — comment was posted, continuing to Done", issue.Number);
        }
    }
}
