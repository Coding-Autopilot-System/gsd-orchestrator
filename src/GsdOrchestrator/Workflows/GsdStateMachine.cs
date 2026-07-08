using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows;

public sealed class GsdStateMachine
{
    private const int MaxRecoveryAttempts = 1;
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

    /// <summary>Starts a new workflow with optional triage-only mode.</summary>
    public Task<GsdWorkflowContext> RunAsync(string owner, string repo, int issueNumber, bool triageModeOnly, CancellationToken ct)
    {
        var ctx = new GsdWorkflowContext
        {
            Issue = new IssueContext(
                Number: issueNumber,
                Title: $"Issue #{issueNumber}",
                Body: "",
                Labels: [],
                RepoOwner: owner,
                RepoName: repo,
                DefaultBranch: "main"),
            CurrentState = WorkflowState.Idle,
            TriageModeOnly = triageModeOnly
        };
        ctx = ctx.WithSdlcRun($"Issue #{issueNumber} for {owner}/{repo}");
        return ExecuteLoopAsync(ctx, ct);
    }

    /// <summary>Returns the registered handler for the given state (used by --pr entry point).</summary>
    public IWorkflowState GetState(WorkflowState state) =>
        _states.TryGetValue(state, out var s)
            ? s
            : throw new InvalidOperationException($"No handler registered for state {state}");

    /// <summary>Resumes an interrupted workflow from its last checkpoint.</summary>
    public async Task<GsdWorkflowContext> ResumeAsync(string workflowId, CancellationToken ct)
    {
        var ctx = await _checkpoints.LoadAsync(workflowId, ct)
            ?? throw new InvalidOperationException($"No checkpoint found for workflow '{workflowId}'");

        if (ctx.CurrentState == WorkflowState.Failed)
        {
            if (ctx.FailedState is null)
                throw new InvalidOperationException(
                    $"Workflow '{workflowId}' failed without a recoverable state. Start a new workflow after reviewing its checkpoint.");

            if (ctx.RetryCount >= MaxRecoveryAttempts)
                throw new InvalidOperationException(
                    $"Workflow '{workflowId}' reached the recovery retry limit of {MaxRecoveryAttempts}.");

            var failedState = ctx.FailedState.Value;
            ctx = ctx with
            {
                CurrentState = failedState,
                FailedState = null,
                FailureReason = null,
                RetryCount = ctx.RetryCount + 1,
                History =
                [
                    .. ctx.History,
                    new StateTransitionEvent(
                        WorkflowState.Failed,
                        failedState,
                        DateTimeOffset.UtcNow,
                        "Explicit recovery retry")
                ]
            };
        }

        _logger.LogInformation("Resuming workflow {WorkflowId} from state {StateName}", workflowId, ctx.CurrentState);
        return await ExecuteLoopAsync(ctx, ct);
    }

    private async Task<GsdWorkflowContext> ExecuteLoopAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        _logger.LogInformation(
            "Workflow {WorkflowId} starting at state {StateName}. IssueNumber={IssueNumber}",
            ctx.WorkflowId, ctx.CurrentState, ctx.Issue?.Number);

        if (ctx.SdlcRun is null)
        {
            ctx = ctx.WithSdlcRun(ctx.Issue is null
                ? $"Workflow {ctx.WorkflowId}"
                : $"{ctx.Issue.Title} for {ctx.Issue.RepoOwner}/{ctx.Issue.RepoName}");
        }

        while (ctx.CurrentState is not WorkflowState.Done and not WorkflowState.Failed)
        {
            ct.ThrowIfCancellationRequested();
            var previousState = ctx.CurrentState;
            var phaseId = SdlcWorkflowMap.PhaseIdForState(previousState);
            var phase = ctx.SdlcRun?.Profile.Phases.FirstOrDefault(p => p.Id == phaseId);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (ctx.SdlcRun is not null)
                {
                    ctx = ctx with { SdlcRun = ctx.SdlcRun.AdvanceIteration() };
                    if (ctx.SdlcRun.IterationCount > ctx.SdlcRun.Profile.Iterations)
                    {
                        ctx = ctx with { StopReason = TerminalStopReason.BudgetExhausted };
                        throw new InvalidOperationException(
                            $"Iteration budget exceeded for workflow {ctx.WorkflowId}");
                    }
                    if (ctx.SdlcRun.NoProgressCount > ctx.SdlcRun.Profile.NoProgressLimit)
                    {
                        ctx = ctx with { StopReason = TerminalStopReason.NoProgress };
                        throw new InvalidOperationException(
                            $"No-progress limit exceeded for workflow {ctx.WorkflowId}");
                    }
                    if (ctx.SdlcRun.AttemptCount > ctx.SdlcRun.Profile.GoalAttempts)
                    {
                        ctx = ctx with { StopReason = TerminalStopReason.BudgetExhausted };
                        throw new InvalidOperationException(
                            $"Goal attempt budget exceeded for workflow {ctx.WorkflowId}");
                    }
                    if (ctx.SdlcRun.ModelCallCount > ctx.SdlcRun.Profile.ModelCalls)
                    {
                        ctx = ctx with { StopReason = TerminalStopReason.BudgetExhausted };
                        throw new InvalidOperationException(
                            $"Model-call budget exceeded for workflow {ctx.WorkflowId}");
                    }
                    if ((DateTimeOffset.UtcNow - ctx.SdlcRun.StartedAt).TotalMinutes > ctx.SdlcRun.Profile.RuntimeMinutes)
                    {
                        ctx = ctx with { StopReason = TerminalStopReason.RuntimeExceeded };
                        throw new InvalidOperationException(
                            $"Runtime-minute budget exceeded for workflow {ctx.WorkflowId}");
                    }
                }

                if (!_states.TryGetValue(ctx.CurrentState, out var stateHandler))
                    throw new InvalidOperationException($"No handler registered for state {ctx.CurrentState}");

                if (phase is not null && ctx.Issue is not null)
                {
                    var prompt = SdlcPromptCompiler.BuildPhasePrompt(
                        $"{ctx.Issue.Title}: {ctx.Issue.Body}".Trim(),
                        ctx.SdlcRun!.Profile,
                        phase,
                        validatedInputs: [ctx.Issue.DefaultBranch],
                        priorEvidence: ctx.History.Select(item => $"{item.From}->{item.To}").ToList(),
                        memoryCandidateFields: ["goal", "constraints", "rollback"]);
                    ctx = ctx.WithSdlcPhaseExecution(new SdlcPhaseExecutionRecord(
                        PhaseId: phase.Id,
                        Status: SdlcPhaseStatus.Running,
                        Verifier: phase.Verifier,
                        PromptDigest: SdlcPromptCompiler.Digest(prompt),
                        Prompt: prompt,
                        StartedAt: DateTimeOffset.UtcNow));
                    _logger.LogInformation(
                        "SDLC phase prompt prepared: Phase={PhaseId} Verifier={Verifier} Digest={Digest}",
                        phase.Id,
                        phase.Verifier,
                        SdlcPromptCompiler.Digest(prompt));
                }
                if (ctx.SdlcRun is not null)
                {
                    ctx = ctx with { SdlcRun = ctx.SdlcRun.AdvanceAttempt().AdvanceModelCall() };
                }
                // Checkpoint BEFORE executing (so we can resume from this state)
                await _checkpoints.SaveAsync(ctx, ct);

                ctx = await stateHandler.ExecuteAsync(ctx, ct);
                ctx = SyncSdlcRunToTransition(ctx);
                if (ctx.SdlcRun is not null && ctx.CurrentState is WorkflowState.Documenting)
                {
                    ctx = ctx with
                    {
                        SdlcRun = ctx.SdlcRun.RecordMemoryCandidate(new SdlcMemoryCandidateRecord(
                            "update-memory",
                            $"1.{ctx.SdlcRun.MemoryCandidateRecords?.Count ?? 0}",
                            $"Memory candidate for {ctx.Issue?.Title ?? ctx.WorkflowId}",
                            [ctx.FailureReason ?? ctx.CurrentState.ToString(), ctx.SdlcRun.CurrentPhaseId],
                            DateTimeOffset.UtcNow))
                    };
                }
                sw.Stop();

                _logger.LogInformation(
                    "State {StateName} completed in {DurationMs}ms — WorkflowId={WorkflowId} IssueNumber={IssueNumber} NextState={NextState}",
                    previousState, sw.ElapsedMilliseconds, ctx.WorkflowId, ctx.Issue?.Number, ctx.CurrentState);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning(
                    "Workflow {WorkflowId} cancelled at state {StateName} after {DurationMs}ms. IssueNumber={IssueNumber}",
                    ctx.WorkflowId, previousState, sw.ElapsedMilliseconds, ctx.Issue?.Number);
                // Use CancellationToken.None — the user-facing ct is already cancelled
                await _checkpoints.SaveAsync(ctx, CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Workflow {WorkflowId} failed at state {StateName} after {DurationMs}ms. IssueNumber={IssueNumber}",
                    ctx.WorkflowId, previousState, sw.ElapsedMilliseconds, ctx.Issue?.Number);
                var failureReason = ex.Message.Length <= 1024 ? ex.Message : ex.Message[..1024];
                if (ctx.StopReason == TerminalStopReason.None)
                {
                    ctx = ctx with
                    {
                        StopReason = ex.Message.Contains("budget", StringComparison.OrdinalIgnoreCase)
                            ? TerminalStopReason.BudgetExhausted
                            : TerminalStopReason.Unknown
                    };
                }
                if (phase is not null && ctx.SdlcRun is not null)
                {
                    ctx = ctx.WithSdlcVerification(new SdlcVerificationOutcome(
                        phase.Id,
                        phase.Verifier,
                        Passed: false,
                        InvalidatedPhaseIds: [phase.RollbackTo ?? phase.Id],
                        Reason: failureReason));
                }
                if (ctx.PendingRollback is not null && ex is not McpException)
                {
                    _logger.LogWarning(
                        "Workflow {WorkflowId} rollback requested from {FromPhase} to {ToPhase}: {Reason}",
                        ctx.WorkflowId,
                        ctx.PendingRollback.FromPhaseId,
                        ctx.PendingRollback.ToPhaseId,
                        ctx.PendingRollback.Reason);
                    var rollbackState = previousState switch
                    {
                        WorkflowState.Analyzing => WorkflowState.Idle,
                        WorkflowState.Branching => WorkflowState.Analyzing,
                        WorkflowState.Editing => WorkflowState.Branching,
                        WorkflowState.TestGenerating => WorkflowState.Editing,
                        WorkflowState.Validating => WorkflowState.TestGenerating,
                        WorkflowState.Committing => WorkflowState.Validating,
                        WorkflowState.PrCreating => WorkflowState.Committing,
                        WorkflowState.Reviewing => WorkflowState.PrCreating,
                        WorkflowState.Documenting => WorkflowState.Reviewing,
                        _ => WorkflowState.Failed
                    };
                    if (rollbackState is not WorkflowState.Failed)
                    {
                        ctx = ctx with { CurrentState = rollbackState };
                        continue;
                    }
                }
                ctx = ctx with
                {
                    CurrentState = WorkflowState.Failed,
                    FailedState = previousState,
                    FailureReason = failureReason,
                    History =
                    [
                        .. ctx.History,
                        new StateTransitionEvent(previousState, WorkflowState.Failed, DateTimeOffset.UtcNow, failureReason)
                    ]
                };
            }
        }

        // Final checkpoint — use CancellationToken.None so a racing Ctrl+C
        // does not lose the completed/failed state.
        await _checkpoints.SaveAsync(ctx, CancellationToken.None);

        if (ctx.CurrentState == WorkflowState.Failed)
        {
            if (ctx.SdlcRun is not null)
            {
                ctx = ctx with { SdlcRun = ctx.SdlcRun.RecordTerminalOutcome("failed") };
            }
            await PostFailureCommentAsync(ctx, CancellationToken.None);
        }
        else
        {
            if (ctx.SdlcRun is not null)
            {
                if (ctx.SdlcRun.MemoryCandidateRecords is null || ctx.SdlcRun.MemoryCandidateRecords.Count == 0)
                {
                    ctx = ctx with
                    {
                        SdlcRun = ctx.SdlcRun.RecordMemoryCandidate(new SdlcMemoryCandidateRecord(
                            "update-memory",
                            "1.0",
                            $"Terminal memory candidate for {ctx.Issue?.Title ?? ctx.WorkflowId}",
                            [ctx.SdlcRun.CurrentPhaseId, ctx.SdlcRun.TerminalOutcome ?? "passed"],
                            DateTimeOffset.UtcNow))
                    };
                }
                ctx = ctx with { SdlcRun = ctx.SdlcRun.RecordTerminalOutcome("passed") };
            }
            await _checkpoints.ArchiveAsync(ctx.WorkflowId, CancellationToken.None);
        }

        return ctx;
    }

    private static GsdWorkflowContext SyncSdlcRunToTransition(GsdWorkflowContext ctx)
    {
        if (ctx.SdlcRun is null)
        {
            return ctx;
        }

        var phase = ctx.SdlcRun.Profile.TryGetPhase(ctx.SdlcRun.CurrentPhaseId);
        return ctx with
        {
            SdlcRun = ctx.SdlcRun with
            {
                CurrentPhaseStatus = ctx.CurrentState is WorkflowState.Done
                    ? SdlcPhaseStatus.Terminal
                    : ctx.CurrentState is WorkflowState.Failed
                        ? SdlcPhaseStatus.Failed
                        : SdlcPhaseStatus.Running,
                CurrentVerifier = phase?.Verifier ?? ctx.SdlcRun.CurrentVerifier
            }
        };
    }

    private async Task PostFailureCommentAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        if (ctx.Issue is null) return;
        try
        {
            var body = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"🤖 **GSD Orchestrator failed**\n\n"
                + $"Last state: `{ctx.History.LastOrDefault()?.From}`\n"
                + $"Reason: {ctx.FailureReason ?? "Unknown error"}\n\n"
                + $"The workflow checkpoint is saved for debugging. Resume with:\n"
                + $"```\ndotnet run -- --resume {ctx.WorkflowId}\n```");

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
