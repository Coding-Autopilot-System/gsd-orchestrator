using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows;

public sealed class GsdStateMachine
{
    private readonly ICheckpointStore _checkpoints;
    private readonly McpToolDispatcher _mcp;
    private readonly ILogger<GsdStateMachine> _logger;
    private readonly Dictionary<WorkflowState, IWorkflowState> _states;

    public GsdStateMachine(
        ICheckpointStore checkpoints,
        McpToolDispatcher mcp,
        IEnumerable<IWorkflowState> states,
        ILogger<GsdStateMachine> logger)
    {
        _checkpoints = checkpoints;
        _mcp = mcp;
        _logger = logger;
        _states = states.ToDictionary(s => s.State);
    }

    /// <summary>Starts a new workflow for the given issue number.</summary>
    public Task<GsdWorkflowContext> RunAsync(string owner, string repo, int issueNumber, CancellationToken ct)
    {
        var ctx = new GsdWorkflowContext
        {
            Issue = new IssueContext(
                Number: issueNumber,
                Title: $"Issue #{issueNumber}",  // will be filled by IdleState
                Body: "",
                Labels: [],
                RepoOwner: owner,
                RepoName: repo,
                DefaultBranch: "main"),
            CurrentState = WorkflowState.Idle
        };
        return ExecuteLoopAsync(ctx, ct);
    }

    /// <summary>Resumes an interrupted workflow from its last checkpoint.</summary>
    public async Task<GsdWorkflowContext> ResumeAsync(string workflowId, CancellationToken ct)
    {
        var ctx = await _checkpoints.LoadAsync(workflowId, ct)
            ?? throw new InvalidOperationException($"No checkpoint found for workflow '{workflowId}'");

        _logger.LogInformation("Resuming workflow {Id} from state {State}", workflowId, ctx.CurrentState);
        return await ExecuteLoopAsync(ctx, ct);
    }

    private async Task<GsdWorkflowContext> ExecuteLoopAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("Workflow {Id} starting at state {State}", ctx.WorkflowId, ctx.CurrentState);

        while (ctx.CurrentState is not WorkflowState.Done and not WorkflowState.Failed)
        {
            ct.ThrowIfCancellationRequested();

            if (!_states.TryGetValue(ctx.CurrentState, out var stateHandler))
                throw new InvalidOperationException($"No handler registered for state {ctx.CurrentState}");

            try
            {
                // Checkpoint BEFORE executing (so we can resume from this state)
                await _checkpoints.SaveAsync(ctx, ct);

                ctx = await stateHandler.ExecuteAsync(ctx, ct);

                _logger.LogInformation("[{Id}] → {State}", ctx.WorkflowId, ctx.CurrentState);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Workflow {Id} cancelled at state {State}", ctx.WorkflowId, ctx.CurrentState);
                await _checkpoints.SaveAsync(ctx, ct);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow {Id} failed at state {State}", ctx.WorkflowId, ctx.CurrentState);
                ctx = (ctx with { FailureReason = ex.Message }).Transition(WorkflowState.Failed);
            }
        }

        // Final checkpoint
        await _checkpoints.SaveAsync(ctx, ct);

        if (ctx.CurrentState == WorkflowState.Failed)
        {
            await PostFailureCommentAsync(ctx, ct);
        }
        else
        {
            await _checkpoints.ArchiveAsync(ctx.WorkflowId, ct);
        }

        return ctx;
    }

    private async Task PostFailureCommentAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        if (ctx.Issue is null) return;
        try
        {
            var body = $"""
                🤖 **GSD Orchestrator failed**

                Last state: `{ctx.History.LastOrDefault()?.From}`
                Reason: {ctx.FailureReason ?? "Unknown error"}

                The workflow checkpoint is saved for debugging. Resume with:
                ```
                dotnet run -- --resume {ctx.WorkflowId}
                ```
                """;

            await _mcp.CallAsync("add_issue_comment", new System.Text.Json.Nodes.JsonObject
            {
                ["owner"] = ctx.Issue.RepoOwner,
                ["repo"] = ctx.Issue.RepoName,
                ["issue_number"] = ctx.Issue.Number,
                ["body"] = body
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post failure comment on issue #{Number}", ctx.Issue?.Number);
        }
    }
}
