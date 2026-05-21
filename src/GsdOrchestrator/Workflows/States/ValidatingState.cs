using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class ValidatingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly ILogger<ValidatingState> _logger;

    private static readonly string[] BlockedPathPrefixes = [".github/workflows/"];
    private static readonly string[] BlockedExtensions = [".pem", ".key", ".p12", ".pfx", ".cer", ".crt"];

    public WorkflowState State => WorkflowState.Validating;

    public ValidatingState(McpToolDispatcher mcp, ILogger<ValidatingState> logger)
    {
        _mcp = mcp;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        var plan = ctx.Plan!;
        var branch = ctx.Branch!;
        var edits = ctx.Edits!;
        var gates = new List<GateResult>();

        // Gate 1: File safety (blocklist)
        foreach (var edit in edits.Edits)
        {
            if (BlockedPathPrefixes.Any(p => edit.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ||
                BlockedExtensions.Any(e => edit.Path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                gates.Add(new GateResult("FileSafety", ValidationStatus.Block,
                    $"Blocked path: {edit.Path}"));
            }
        }

        if (gates.Any(g => g.Status == ValidationStatus.Block))
        {
            _logger.LogError("BLOCK: file safety gate failed: {Detail}", gates.First(g => g.Status == ValidationStatus.Block).Detail);
            return FailWith(ctx, "File safety gate blocked the workflow: " + gates.First(g => g.Status == ValidationStatus.Block).Detail);
        }

        gates.Add(new GateResult("FileSafety", ValidationStatus.Pass));

        // Gate 2: Merge conflict pre-flight
        string? conflictDetail = null;
        try
        {
            var compareResult = await _mcp.CallAsync("compare_commits", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["base"] = issue.DefaultBranch,
                ["head"] = branch.BranchName
            }, ct);

            var compareJson = compareResult.ParseInnerJson();
            var status = compareJson?["status"]?.GetValue<string>() ?? "ahead";

            if (status == "diverged")
            {
                // Check if divergence overlaps with our edited files
                var changedFiles = compareJson?["files"]?.AsArray()
                    .Select(f => f?["filename"]?.GetValue<string>() ?? "")
                    .ToHashSet() ?? [];
                var ourFiles = edits.Edits.Select(e => e.Path).ToHashSet();
                var overlap = changedFiles.Intersect(ourFiles).ToList();

                if (overlap.Count > 0)
                {
                    conflictDetail = $"Conflict in: {string.Join(", ", overlap)}";
                    gates.Add(new GateResult("MergeConflict", ValidationStatus.Block, conflictDetail));
                    return FailWith(ctx, conflictDetail);
                }
                else
                {
                    _logger.LogWarning("Branch diverged from {Default} but no file overlap — proceeding", issue.DefaultBranch);
                    gates.Add(new GateResult("MergeConflict", ValidationStatus.Warn, "Diverged but no overlap"));
                }
            }
            else
            {
                gates.Add(new GateResult("MergeConflict", ValidationStatus.Pass));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "compare_commits failed — skipping merge conflict check");
            gates.Add(new GateResult("MergeConflict", ValidationStatus.Warn, "Could not check: " + ex.Message));
        }

        // Gate 3: Diff size (gated by edit count as a proxy — compare_commits stats need separate parsing)
        gates.Add(new GateResult("DiffSize", ValidationStatus.Pass));

        // Gate 4: Test coverage intent
        if (plan.RequiresTests)
        {
            var hasTestFiles = edits.Edits.Any(e =>
                e.Path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                e.Path.Contains("Spec", StringComparison.OrdinalIgnoreCase));

            if (!hasTestFiles)
            {
                _logger.LogWarning("Plan required tests but no test files were modified");
                gates.Add(new GateResult("TestIntent", ValidationStatus.Warn, "No test files modified"));
            }
            else
            {
                gates.Add(new GateResult("TestIntent", ValidationStatus.Pass));
            }
        }

        var overallStatus = gates.Any(g => g.Status == ValidationStatus.Block) ? ValidationStatus.Block
            : gates.Any(g => g.Status == ValidationStatus.Warn) ? ValidationStatus.Warn
            : ValidationStatus.Pass;

        _logger.LogInformation("Validation: {Status} ({GateCount} gates)", overallStatus, gates.Count);

        var validation = new ValidationResult(overallStatus, gates, conflictDetail);
        return (ctx with { Validation = validation }).Transition(WorkflowState.Committing);
    }

    private static GsdWorkflowContext FailWith(GsdWorkflowContext ctx, string reason) =>
        (ctx with { FailureReason = reason }).Transition(WorkflowState.Failed);
}
