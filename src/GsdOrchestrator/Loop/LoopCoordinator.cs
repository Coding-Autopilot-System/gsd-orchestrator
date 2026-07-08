using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Scheduling;
using GsdOrchestrator.Verification;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Loop;

public sealed record LoopWorkRequest(string GoalId, string WorkItemId, int Attempt, bool IsRepair, string CorrelationId);
public sealed record LoopWorkResult(bool Succeeded, IReadOnlyList<string> EvidenceUris, string Summary, int PeakConcurrency = 1);
public sealed record TerminalLoopOutcome(string GoalId, string CorrelationId, GoalStatus Status, string Summary, IReadOnlyList<string> EvidenceUris);
public sealed record LoopRunResult(GoalAggregate Aggregate, int WorkerAttempts, int VerificationRuns, bool RepairCreated, int PeakConcurrency);

public interface ILoopWorker
{
    Task<LoopWorkResult> ExecuteAsync(LoopWorkRequest request, CancellationToken cancellationToken);
}

public interface ILoopVerifier
{
    Task<VerificationRunResult> VerifyAsync(string goalId, LoopWorkResult work, CancellationToken cancellationToken);
}

public interface ITerminalOutcomePublisher
{
    Task PublishAsync(TerminalLoopOutcome outcome, CancellationToken cancellationToken);
}

public sealed class MafProcessLoopWorker(string pythonExecutable, string mafRoot) : ILoopWorker
{
    public async Task<LoopWorkResult> ExecuteAsync(LoopWorkRequest request, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                WorkingDirectory = mafRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.ArgumentList.Add("-m");
        process.StartInfo.ArgumentList.Add("maf_starter.loop_worker_cli");
        process.Start();
        await process.StandardInput.WriteAsync(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)).AsMemory(),
            cancellationToken);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"MAF worker failed with exit code {process.ExitCode}: {error.Trim()}");
        var envelope = JsonSerializer.Deserialize<MafWorkerEnvelope>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("MAF worker returned no result.");
        return new(envelope.Succeeded, envelope.EvidenceUris, envelope.Summary, envelope.PeakConcurrency);
    }

    private sealed record MafWorkerEnvelope(bool Succeeded, int PeakConcurrency, string[] Roles, string[] EvidenceUris, string Summary);
}

public sealed class McpTerminalOutcomePublisher(McpToolDispatcher dispatcher) : ITerminalOutcomePublisher
{
    public async Task PublishAsync(TerminalLoopOutcome outcome, CancellationToken cancellationToken)
    {
        var status = outcome.Status switch
        {
            GoalStatus.Completed => "completed",
            GoalStatus.Cancelled => "cancelled",
            GoalStatus.Blocked => "blocked",
            GoalStatus.BudgetExhausted => "budget_exhausted",
            _ => "failed",
        };
        var evidence = new JsonArray(outcome.EvidenceUris.Select(uri => JsonValue.Create(uri)).ToArray());
        await dispatcher.CallAsync("record_terminal_outcome", new JsonObject
        {
            ["goal_id"] = outcome.GoalId,
            ["status"] = status,
            ["evidence"] = evidence,
            ["summary"] = outcome.Summary,
        }, cancellationToken);
    }
}

public sealed class LoopCoordinator(IGoalStore store, ILoopWorker worker, ILoopVerifier verifier, ITerminalOutcomePublisher learning, ILogger<LoopCoordinator> logger)
{
    public async Task<LoopRunResult> RunAsync(
        string goalId,
        RepairBudget repairBudget,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await store.LoadAsync(goalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal '{goalId}' was not found.");
        if (aggregate.Goal.Status is not GoalStatus.Planned and not GoalStatus.Running)
            throw new InvalidOperationException($"Goal '{goalId}' cannot execute from {aggregate.Goal.Status}.");
        var workItem = aggregate.WorkItems.SingleOrDefault(item => item.Status is WorkItemStatus.Pending or WorkItemStatus.Ready)
            ?? throw new InvalidOperationException($"Goal '{goalId}' has no executable work item.");
        var now = DateTimeOffset.UtcNow;
        var lease = await store.TryAcquireLeaseAsync(new(
            goalId, workItem.Id, "loop-coordinator", now, now.AddMinutes(5),
            aggregate.Goal.Limits.MaxFanOut, aggregate.Goal.Limits.MaxFanOut, aggregate.Goal.Limits.MaxFanOut), cancellationToken)
            ?? throw new InvalidOperationException($"Work item '{workItem.Id}' could not be leased.");

        aggregate = (await store.LoadAsync(goalId, cancellationToken))!;
        var workerAttempts = 0;
        var peakConcurrency = 0;
        var verificationRuns = 0;
        var repairCreated = false;

        try
        {
            var first = await ExecuteAttemptAsync(aggregate, workItem, 1, false, cancellationToken);
            aggregate = first.Aggregate;
            workerAttempts = 1;
            peakConcurrency = first.Work.PeakConcurrency;

            if (first.Work.Succeeded)
            {
                var verification = await verifier.VerifyAsync(goalId, first.Work, cancellationToken);
                verificationRuns++;
                aggregate = AddVerification(aggregate, workItem.Id, verification);
                if (verification.Outcome == VerificationOutcome.Failed)
                {
                    var consumedBudget = repairBudget with
                    {
                        ConsumedAttempts = Math.Max(repairBudget.ConsumedAttempts, 1),
                        ConsumedIterations = Math.Max(repairBudget.ConsumedIterations, 1),
                    };
                    var repair = RepairPolicy.CreateRepair(verification, consumedBudget, workItem.Id);
                    if (repair is not null)
                    {
                        repairCreated = true;
                        aggregate = AddEvent(aggregate, "repair.created", $"attempt-{repair.AttemptNumber}");
                        await store.SaveAsync(aggregate, cancellationToken);
                        var repaired = await ExecuteAttemptAsync(aggregate, workItem, repair.AttemptNumber, true, cancellationToken);
                        aggregate = repaired.Aggregate;
                        workerAttempts++;
                        peakConcurrency = Math.Max(peakConcurrency, repaired.Work.PeakConcurrency);
                        if (repaired.Work.Succeeded)
                        {
                            verification = await verifier.VerifyAsync(goalId, repaired.Work, cancellationToken);
                            verificationRuns++;
                            aggregate = AddVerification(aggregate, workItem.Id, verification);
                        }
                    }
                }
            }

            var outcome = aggregate.Evidence.Any(item => item.Kind == "verification-failed")
                && !aggregate.Evidence.Any(item => item.Kind == "verification-passed" && item.Id.EndsWith($"-{verificationRuns}", StringComparison.Ordinal))
                ? GoalStatus.Failed
                : aggregate.Evidence.Any(item => item.Kind == "verification-passed") ? GoalStatus.Completed : GoalStatus.Failed;
            aggregate = Complete(aggregate, workItem.Id, outcome, lease.Id);
            await store.SaveAsync(aggregate, cancellationToken);

            var evidence = aggregate.Evidence.Select(item => item.Uri).Distinct(StringComparer.Ordinal).ToArray();
            await learning.PublishAsync(new(goalId, aggregate.Goal.CorrelationId, outcome, $"Goal {outcome} after {workerAttempts} worker attempt(s).", evidence), cancellationToken);
            return new(aggregate, workerAttempts, verificationRuns, repairCreated, peakConcurrency);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var failure = FailureClassifier.Classify(new OperationCanceledException("Loop execution cancelled by caller."), "gsd-orchestrator.LoopCoordinator");
            logger.LogError(
                "Loop execution failed for goal {GoalId} with failure class {FailureClass}: {Message}",
                goalId,
                failure.FailureClass,
                failure.Message);
            throw;
        }
        catch (Exception ex)
        {
            var failure = FailureClassifier.Classify(ex, "gsd-orchestrator.LoopCoordinator");
            logger.LogError(
                ex,
                "Loop execution failed for goal {GoalId} with failure class {FailureClass}: {Message}",
                goalId,
                failure.FailureClass,
                failure.Message);

            aggregate = await store.LoadAsync(goalId, cancellationToken) ?? aggregate;
            var status = failure.FailureClass == FailureClass.Cancellation ? GoalStatus.Cancelled : GoalStatus.Failed;
            var evidenceUri = $"cas://evidence/failure/{failure.FailureClass.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}";
            aggregate = CompleteWithFailure(aggregate, workItem.Id, lease.Id, status, failure, evidenceUri);
            await store.SaveAsync(aggregate, cancellationToken);

            var evidence = aggregate.Evidence.Select(item => item.Uri).Distinct(StringComparer.Ordinal).ToArray();
            await learning.PublishAsync(
                new(goalId, aggregate.Goal.CorrelationId, status, failure.Message, evidence),
                cancellationToken);
            return new(aggregate, workerAttempts, verificationRuns, repairCreated, peakConcurrency);
        }
    }

    private async Task<(GoalAggregate Aggregate, LoopWorkResult Work)> ExecuteAttemptAsync(
        GoalAggregate aggregate,
        WorkItemRecord workItem,
        int attemptNumber,
        bool isRepair,
        CancellationToken cancellationToken)
    {
        var request = new LoopWorkRequest(aggregate.Goal.Id, workItem.Id, attemptNumber, isRepair, aggregate.Goal.CorrelationId);
        var result = await worker.ExecuteAsync(request, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var attempt = new AttemptRecord(Guid.NewGuid().ToString("N"), aggregate.Goal.Id, workItem.Id, attemptNumber,
            result.Succeeded ? AttemptStatus.Succeeded : AttemptStatus.Failed, now);
        var evidence = result.EvidenceUris.Select((uri, index) =>
            new EvidenceRecord($"worker-{attemptNumber}-{index}", aggregate.Goal.Id, workItem.Id, "worker", uri));
        var updated = aggregate with
        {
            Goal = aggregate.Goal with { Status = GoalStatus.Running },
            Attempts = [.. aggregate.Attempts, attempt],
            Evidence = [.. aggregate.Evidence, .. evidence],
            Events = [.. aggregate.Events, NewEvent(aggregate, $"worker.{(result.Succeeded ? "succeeded" : "failed")}", $"attempt-{attemptNumber}")]
        };
        await store.SaveAsync(updated, cancellationToken);
        return (updated, result);
    }

    private static GoalAggregate AddVerification(GoalAggregate aggregate, string workItemId, VerificationRunResult result)
    {
        var runNumber = aggregate.Events.Count(item => item.Type == "verification.completed") + 1;
        var kind = result.Outcome == VerificationOutcome.Passed ? "verification-passed" : "verification-failed";
        var evidence = result.Checks.Select((check, index) =>
            new EvidenceRecord($"verification-{index}-{runNumber}", aggregate.Goal.Id, workItemId, kind, check.EvidenceUri));
        return aggregate with
        {
            Evidence = [.. aggregate.Evidence, .. evidence],
            Events = [.. aggregate.Events, NewEvent(aggregate, "verification.completed", result.Outcome.ToString().ToLowerInvariant())]
        };
    }

    private static GoalAggregate AddEvent(GoalAggregate aggregate, string type, string payload) =>
        aggregate with { Events = [.. aggregate.Events, NewEvent(aggregate, type, payload)] };

    private static GoalAggregate Complete(GoalAggregate aggregate, string workItemId, GoalStatus status, string leaseId)
    {
        var now = DateTimeOffset.UtcNow;
        return aggregate with
        {
            Goal = aggregate.Goal with { Status = status },
            WorkItems = aggregate.WorkItems.Select(item => item.Id == workItemId
                ? item with { Status = status == GoalStatus.Completed ? WorkItemStatus.Succeeded : WorkItemStatus.Failed }
                : item).ToArray(),
            Leases = aggregate.Leases.Where(item => item.Id != leaseId).ToArray(),
            BudgetReservations = [],
            Transitions = [.. aggregate.Transitions, new(Guid.NewGuid().ToString("N"), aggregate.Goal.Id, aggregate.Goal.Status.ToString(), status.ToString(), status == GoalStatus.Completed ? GoalStopReason.Passed.ToString() : GoalStopReason.Exhaustion.ToString(), now)],
            Events = [.. aggregate.Events, NewEvent(aggregate, status == GoalStatus.Completed ? "goal.completed" : "goal.failed", status.ToString().ToLowerInvariant())]
        };
    }

    private static GoalAggregate CompleteWithFailure(
        GoalAggregate aggregate,
        string workItemId,
        string leaseId,
        GoalStatus status,
        FailureState failure,
        string evidenceUri)
    {
        var now = DateTimeOffset.UtcNow;
        var eventPayload = JsonSerializer.Serialize(new
        {
            failureClass = failure.FailureClass.ToString(),
            component = failure.Component,
            message = failure.Message,
            retryable = failure.Retryable,
            exceptionType = failure.ExceptionType,
            causeChain = failure.CauseChain,
            retryAfterSeconds = failure.RetryAfterSeconds,
        });

        return aggregate with
        {
            Goal = aggregate.Goal with { Status = status },
            WorkItems = aggregate.WorkItems.Select(item => item.Id == workItemId
                ? item with { Status = status == GoalStatus.Cancelled ? WorkItemStatus.Cancelled : WorkItemStatus.Failed }
                : item).ToArray(),
            Leases = aggregate.Leases.Where(item => item.Id != leaseId).ToArray(),
            BudgetReservations = [],
            Evidence = [.. aggregate.Evidence, new EvidenceRecord(Guid.NewGuid().ToString("N"), aggregate.Goal.Id, workItemId, "failure-state", evidenceUri)],
            Transitions = [.. aggregate.Transitions, new(Guid.NewGuid().ToString("N"), aggregate.Goal.Id, aggregate.Goal.Status.ToString(), status.ToString(), status == GoalStatus.Cancelled ? GoalStopReason.Cancellation.ToString() : GoalStopReason.UnrecoverableFault.ToString(), now)],
            Events =
            [
                .. aggregate.Events,
                new GoalEventRecord(Guid.NewGuid().ToString("N"), aggregate.Goal.Id,
                    aggregate.Events.Count == 0 ? 1 : aggregate.Events.Max(item => item.Sequence) + 1,
                    "goal.failed", eventPayload, now)
            ]
        };
    }

    private static GoalEventRecord NewEvent(GoalAggregate aggregate, string type, string payload) =>
        new(Guid.NewGuid().ToString("N"), aggregate.Goal.Id,
            aggregate.Events.Count == 0 ? 1 : aggregate.Events.Max(item => item.Sequence) + 1,
            type, System.Text.Json.JsonSerializer.Serialize(new { outcome = payload }), DateTimeOffset.UtcNow);
}

public enum ExternalActionDecision { Authorized, WaitingApproval }

public static class LoopPolicyGuard
{
    private static readonly string[] ApprovalActions = ["push", "deploy", "delete", "message", "production_mutation"];

    public static void RequireReadablePath(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part.Equals(".env", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Environment files are denied by policy.");
    }

    public static ExternalActionDecision EvaluateExternalAction(string action, bool approved)
    {
        if (!ApprovalActions.Contains(action, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown external action.");
        return approved ? ExternalActionDecision.Authorized : ExternalActionDecision.WaitingApproval;
    }
}
