using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class ReviewingState : IWorkflowState
{
    private const int MaxLlmAttempts = 3;

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
        // Route to PR-review mode when context carries a PrReviewContext
        if (ctx.PrReview is not null)
            return await ExecutePrReviewAsync(ctx, ctx.PrReview, ct);

        // --issue mode: post comment and request reviewers (legacy behaviour — REV-03)
        return await ExecuteIssueModeAsync(ctx, ct);
    }

    // ── MODE A: --pr review loop ─────────────────────────────────────────────

    private async Task<GsdWorkflowContext> ExecutePrReviewAsync(
        GsdWorkflowContext ctx,
        PrReviewContext prCtx,
        CancellationToken ct)
    {
        _logger.LogInformation("PR review starting for PR #{PrNumber} in {Owner}/{Repo}",
            prCtx.PrNumber, prCtx.Owner, prCtx.Repo);

        // Fetch PR metadata (title + body for context)
        var prMeta = await FetchPrMetaAsync(prCtx, ct);

        // Build prompt and invoke LLM
        var reviewResult = await InvokeLlmReviewAsync(prCtx, prMeta, ct);

        // Submit review via GitHub MCP
        await SubmitGitHubReviewAsync(prCtx, reviewResult, ct);

        _logger.LogInformation(
            "PR #{PrNumber} review submitted: verdict={Verdict}, comments={Count}",
            prCtx.PrNumber, reviewResult.Verdict, reviewResult.Comments.Count);

        return (ctx with { Review = reviewResult }).Transition(WorkflowState.Done);
    }

    private async Task<(string title, string body)> FetchPrMetaAsync(
        PrReviewContext prCtx, CancellationToken ct)
    {
        try
        {
            var result = await _mcp.CallAsync("get_pull_request", new JsonObject
            {
                ["owner"] = prCtx.Owner,
                ["repo"] = prCtx.Repo,
                ["pullNumber"] = prCtx.PrNumber
            }, ct);

            var json = result.ParseInnerJson();
            var title = json?["title"]?.GetValue<string>() ?? $"PR #{prCtx.PrNumber}";
            var body = json?["body"]?.GetValue<string>() ?? "";
            return (title, body);
        }
        catch (McpException ex)
        {
            _logger.LogWarning(ex, "Could not fetch PR metadata — proceeding with diff only");
            return ($"PR #{prCtx.PrNumber}", "");
        }
    }

    private async Task<ReviewResult> InvokeLlmReviewAsync(
        PrReviewContext prCtx,
        (string title, string body) prMeta,
        CancellationToken ct)
    {
        var systemPrompt = """
            You are a senior software engineer performing a thorough code review.
            Analyse the provided git diff and respond with a JSON object (no markdown fences) containing:
            {
              "verdict": "APPROVE" or "REQUEST_CHANGES",
              "summary": "1-3 sentence overall assessment",
              "comments": [
                {
                  "path": "relative/file/path.cs",
                  "line": <line number in the NEW file>,
                  "side": "RIGHT",
                  "severity": "error" | "warning" | "info",
                  "body": "Specific, actionable feedback"
                }
              ]
            }
            Rules:
            - verdict = APPROVE when the change is correct and safe to merge
            - verdict = REQUEST_CHANGES when there are bugs, security issues, or major style violations
            - comments array may be empty for APPROVE
            - severity "error" = must fix before merge, "warning" = should fix, "info" = suggestion
            - Only include comments for lines present in the diff (side RIGHT = new file lines)
            - Respond ONLY with the JSON object — no prose before or after
            """;

        var userPrompt = $$"""
            PR #{{prCtx.PrNumber}}: {{prMeta.title}}
            {{(prMeta.body.Length > 0 ? $"\nDescription:\n{prMeta.body}\n" : "")}}
            Diff:
            ```diff
            {{prCtx.Diff}}
            ```
            """;

        ReviewResult? reviewResult = null;
        Exception? lastException = null;
        string? lastRaw = null;

        for (int attempt = 1; attempt <= MaxLlmAttempts && reviewResult is null; attempt++)
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, systemPrompt),
                    new(ChatRole.User, userPrompt)
                };

                if (attempt > 1 && lastRaw is not null)
                    messages.Add(new ChatMessage(ChatRole.User,
                        $"Your previous response could not be parsed as JSON. Respond ONLY with valid JSON. Previous response was:\n{lastRaw}"));

                var response = await _llm.GetResponseAsync(
                    messages,
                    new ChatOptions { Temperature = 0.1f },
                    ct);

                lastRaw = response.Text?.Trim() ?? "";
                reviewResult = ParseReviewResult(lastRaw);

                if (reviewResult is null)
                    _logger.LogWarning("LLM attempt {Attempt}/{Max}: parse failed", attempt, MaxLlmAttempts);
                else
                    _logger.LogInformation("LLM attempt {Attempt}/{Max}: parsed verdict={Verdict}",
                        attempt, MaxLlmAttempts, reviewResult.Verdict);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "LLM attempt {Attempt}/{Max} threw", attempt, MaxLlmAttempts);
            }
        }

        if (reviewResult is null)
            throw new InvalidOperationException(
                $"LLM failed to produce a valid review JSON after {MaxLlmAttempts} attempts. " +
                $"Last raw response: {lastRaw ?? "(null)"}",
                lastException);

        return reviewResult;
    }

    private static ReviewResult? ParseReviewResult(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            // Strip markdown fences if LLM added them despite instructions
            var text = raw;
            if (text.StartsWith("```")) text = text[(text.IndexOf('\n') + 1)..];
            if (text.TrimEnd().EndsWith("```")) text = text[..text.LastIndexOf("```")];
            text = text.Trim();

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var verdict = root.GetProperty("verdict").GetString();
            if (verdict is not ("APPROVE" or "REQUEST_CHANGES")) return null;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            var comments = new List<ReviewComment>();

            if (root.TryGetProperty("comments", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in arr.EnumerateArray())
                {
                    var path = c.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                    var line = c.TryGetProperty("line", out var l) ? l.GetInt32() : 1;
                    var side = c.TryGetProperty("side", out var sd) ? sd.GetString() ?? "RIGHT" : "RIGHT";
                    var severity = c.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "info" : "info";
                    var body = c.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(body))
                        comments.Add(new ReviewComment(path, line, side, severity, body));
                }
            }

            return new ReviewResult(verdict!, summary, comments);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task SubmitGitHubReviewAsync(
        PrReviewContext prCtx,
        ReviewResult reviewResult,
        CancellationToken ct)
    {
        var commentsArray = new JsonArray();
        foreach (var c in reviewResult.Comments)
        {
            commentsArray.Add(new JsonObject
            {
                ["path"] = c.Path,
                ["line"] = c.Line,
                ["side"] = c.Side,
                ["body"] = $"[{c.Severity.ToUpperInvariant()}] {c.Body}"
            });
        }

        var reviewBody = $"**GSD Orchestrator automated review**\n\n{reviewResult.Summary}";

        await _mcp.CallAsync("create_pull_request_review", new JsonObject
        {
            ["owner"] = prCtx.Owner,
            ["repo"] = prCtx.Repo,
            ["pullNumber"] = prCtx.PrNumber,
            ["body"] = reviewBody,
            ["event"] = reviewResult.Verdict,
            ["comments"] = commentsArray
        }, ct);
    }

    // ── MODE B: --issue post-PR comment (legacy — REV-03) ────────────────────

    private async Task<GsdWorkflowContext> ExecuteIssueModeAsync(
        GsdWorkflowContext ctx, CancellationToken ct)
    {
        if (ctx.Issue is null || ctx.PullRequest is null || ctx.Plan is null || ctx.Edits is null)
            throw new InvalidOperationException(
                "ReviewingState (issue mode) requires Issue, PullRequest, Plan, and Edits " +
                "to all be set in the context. Current state may have been reached incorrectly.");

        var issue = ctx.Issue;
        var pr = ctx.PullRequest;
        var plan = ctx.Plan;
        var edits = ctx.Edits;

        var comment = await GenerateReviewCommentAsync(issue, plan, edits, pr, ct);

        await _mcp.CallAsync("add_issue_comment", new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["issue_number"] = pr.PrNumber,
            ["body"] = comment
        }, ct);

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
        var filesList = string.Join("\n", edits.Edits.Select(e =>
            $"- `{e.Path}`: {plan.FilesToModify.FirstOrDefault(f => f.Path == e.Path)?.Rationale ?? "updated"}"));

        var prompt = $$"""
            Write a short, friendly GitHub PR comment (2-4 sentences) from a bot explaining:
            - What was automatically changed and why
            - Which files were modified
            - A note asking the reviewer to verify the changes

            Issue: #{{issue.Number}} — {{issue.Title}}
            Plan: {{plan.Summary}}
            Files modified:
            {{filesList}}
            """;

        var response = await _llm.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { Temperature = 0.4f },
            ct);

        var text = response.Text ?? "Automated changes applied. Please review.";
        return $"🤖 **GSD Orchestrator**\n\n{text}";
    }
}
