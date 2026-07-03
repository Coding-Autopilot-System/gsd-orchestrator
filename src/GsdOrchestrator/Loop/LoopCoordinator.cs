using GsdOrchestrator.Scheduling;
using GsdOrchestrator.Verification;
using GsdOrchestrator.Mcp;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GsdOrchestrator.Loop;

public sealed record LoopToolScope(bool MutationAllowed, string MutationOwner, IReadOnlyList<string> AllowedTools);
public sealed record LoopStepInput(string Name, string Value);
public sealed record LoopCompletionCriterion(string Id, string Description, bool Mandatory);
public sealed record LoopFanOutBranch(string Role, IReadOnlyList<string> DependsOnRoles, IReadOnlyList<string> ExpectedArtifacts, bool RepositoryMutationAllowed = false, string? MergeStrategy = null);
public sealed record LoopFanInRule(string RuleSet, IReadOnlyList<string> RequiredRoles, bool RequireAllRequiredSucceeded);
public sealed record LoopFanOutPlan(int MaxConcurrency, IReadOnlyList<LoopFanOutBranch> Branches, LoopFanInRule Aggregation, string AggregatorRole)
{
    public IReadOnlyList<string> RequiredRoles => Branches.Select(branch => branch.Role).ToArray();
}
public sealed record LoopStepContract(
    string ContractId,
    string ContextBundleId,
    string Objective,
    string DownstreamConsumer,
    string OutputSchema,
    LoopToolScope ToolScope,
    LoopFanOutPlan FanOut,
    IReadOnlyList<LoopStepInput> Inputs,
    IReadOnlyList<LoopCompletionCriterion> CompletionCriteria);
public sealed record LoopWorkRequest(string GoalId, string WorkItemId, int Attempt, bool IsRepair, string CorrelationId, LoopStepContract Contract);
public sealed record LoopWorkResult(
    bool Succeeded,
    IReadOnlyList<string> EvidenceUris,
    string Summary,
    string ContractId,
    string ContextBundleId,
    string OutputSchema,
    IReadOnlyList<string> Roles,
    IReadOnlyList<LoopBranchResult> BranchResults,
    LoopFanInState FanIn,
    int PeakConcurrency = 1);
public sealed record LoopBranchResult(string Role, string Status, IReadOnlyList<string> EvidenceUris, bool Required);
public sealed record LoopFanInState(string AggregatorRole, IReadOnlyList<LoopBranchResult> Branches, bool AllRequiredTerminal, bool AllRequiredSucceeded);
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
        if (!string.Equals(envelope.ContractId, request.Contract.ContractId, StringComparison.Ordinal))
            throw new InvalidOperationException($"MAF worker contract mismatch. Expected {request.Contract.ContractId}, received {envelope.ContractId}.");
        if (!string.Equals(envelope.ContextBundleId, request.Contract.ContextBundleId, StringComparison.Ordinal))
            throw new InvalidOperationException($"MAF worker context bundle mismatch. Expected {request.Contract.ContextBundleId}, received {envelope.ContextBundleId}.");
        if (!string.Equals(envelope.OutputSchema, request.Contract.OutputSchema, StringComparison.Ordinal))
            throw new InvalidOperationException($"MAF worker output schema mismatch. Expected {request.Contract.OutputSchema}, received {envelope.OutputSchema}.");
        return new(envelope.Succeeded, envelope.EvidenceUris, envelope.Summary, envelope.ContractId, envelope.ContextBundleId, envelope.OutputSchema, envelope.Roles, envelope.BranchResults, envelope.FanIn, envelope.PeakConcurrency);
    }

    private sealed record MafWorkerEnvelope(bool Succeeded, int PeakConcurrency, string[] Roles, string[] EvidenceUris, string Summary, string ContractId, string ContextBundleId, string OutputSchema, LoopBranchResult[] BranchResults, LoopFanInState FanIn);
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

public sealed class LoopCoordinator(IGoalStore store, ILoopWorker worker, ILoopVerifier verifier, ITerminalOutcomePublisher learning)
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
        LoopPolicyGuard.ValidateFanOutPlan(aggregate.Goal.Limits, BuildContract(aggregate, workItem, 1, false).FanOut);
        var now = DateTimeOffset.UtcNow;
        var lease = await store.TryAcquireLeaseAsync(new(
            goalId, workItem.Id, "loop-coordinator", now, now.AddMinutes(5),
            aggregate.Goal.Limits.MaxFanOut, aggregate.Goal.Limits.MaxFanOut, aggregate.Goal.Limits.MaxFanOut), cancellationToken)
            ?? throw new InvalidOperationException($"Work item '{workItem.Id}' could not be leased.");

        aggregate = (await store.LoadAsync(goalId, cancellationToken))!;
        var first = await ExecuteAttemptAsync(aggregate, workItem, 1, false, cancellationToken);
        aggregate = first.Aggregate;
        var workerAttempts = 1;
        var peakConcurrency = first.Work.PeakConcurrency;
        var verificationRuns = 0;
        var repairCreated = false;

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
                    aggregate = AddDecisionEvent(aggregate, "repair.created", "create_repair", new { repair.AttemptNumber, repair.EvidenceUris });
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
                else
                {
                    aggregate = AddDecisionEvent(aggregate, "verification.decision", "stop", new { reason = "repair_not_permitted" });
                }
            }
            else if (verification.Outcome == VerificationOutcome.Inconclusive)
            {
                aggregate = AddDecisionEvent(aggregate, "verification.decision", "request_evidence", new { reason = "mandatory_checks_inconclusive" });
            }
            else
            {
                aggregate = AddDecisionEvent(aggregate, "verification.decision", "advance", new { target = "goal_completion" });
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

    private async Task<(GoalAggregate Aggregate, LoopWorkResult Work)> ExecuteAttemptAsync(
        GoalAggregate aggregate,
        WorkItemRecord workItem,
        int attemptNumber,
        bool isRepair,
        CancellationToken cancellationToken)
    {
        var contract = BuildContract(aggregate, workItem, attemptNumber, isRepair);
        LoopPolicyGuard.ValidateFanOutPlan(aggregate.Goal.Limits, contract.FanOut);
        var request = new LoopWorkRequest(aggregate.Goal.Id, workItem.Id, attemptNumber, isRepair, aggregate.Goal.CorrelationId, contract);
        var result = await worker.ExecuteAsync(request, cancellationToken);
        ValidateWorkResult(contract, result);
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
            Events =
            [
                .. aggregate.Events,
                NewEvent(aggregate, "step.contract.declared", "declared", new
                {
                    contract.ContractId,
                    contract.ContextBundleId,
                    contract.Objective,
                    contract.DownstreamConsumer,
                    contract.OutputSchema,
                    contract.FanOut.RequiredRoles,
                    Branches = contract.FanOut.Branches,
                    Aggregation = contract.FanOut.Aggregation,
                    contract.FanOut.MaxConcurrency,
                    contract.ToolScope.MutationAllowed,
                }),
                NewEvent(aggregate, $"worker.{(result.Succeeded ? "succeeded" : "failed")}", result.Succeeded ? "succeeded" : "failed", new
                {
                    result.ContractId,
                    result.ContextBundleId,
                    result.Roles,
                    result.FanIn,
                    result.PeakConcurrency,
                    result.Summary,
                })
            ]
        };
        await store.SaveAsync(updated, cancellationToken);
        return (updated, result);
    }

    private static void ValidateWorkResult(LoopStepContract contract, LoopWorkResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Summary))
            throw new InvalidOperationException("Loop worker returned an empty summary.");
        if (result.EvidenceUris.Count < contract.FanOut.RequiredRoles.Count)
            throw new InvalidOperationException("Loop worker returned fewer evidence URIs than required specialist roles.");
        var required = new HashSet<string>(contract.FanOut.RequiredRoles, StringComparer.Ordinal);
        var actual = new HashSet<string>(result.Roles, StringComparer.Ordinal);
        if (!required.SetEquals(actual))
            throw new InvalidOperationException("Loop worker returned a role set that does not match the declared step contract.");
        if (!string.Equals(result.FanIn.AggregatorRole, contract.FanOut.AggregatorRole, StringComparison.Ordinal))
            throw new InvalidOperationException("Loop worker returned a fan-in aggregator that does not match the declared step contract.");
        if (!result.FanIn.AllRequiredTerminal || !result.FanIn.AllRequiredSucceeded)
            throw new InvalidOperationException("Loop worker returned a non-terminal or degraded required fan-in state.");
        var branchRoles = result.BranchResults.Select(branch => branch.Role).ToHashSet(StringComparer.Ordinal);
        if (!required.SetEquals(branchRoles))
            throw new InvalidOperationException("Loop worker returned branch results that do not match the declared specialist set.");
        if (result.BranchResults.Any(branch => !branch.Required))
            throw new InvalidOperationException("Loop worker returned optional branches outside the declared required fan-out.");
        if (result.BranchResults.Any(branch => branch.EvidenceUris.Count == 0))
            throw new InvalidOperationException("Loop worker returned a branch result without evidence.");
        if (result.BranchResults.Any(branch => branch.Status is not ("succeeded" or "failed" or "cancelled" or "timed_out")))
            throw new InvalidOperationException("Loop worker returned a non-terminal branch result.");
    }

    private static LoopStepContract BuildContract(GoalAggregate aggregate, WorkItemRecord workItem, int attemptNumber, bool isRepair)
    {
        var phase = isRepair ? "repair" : "feature";
        var branches = new[]
        {
            new LoopFanOutBranch("research", [], [$"cas://artifact/{workItem.Id}/research/summary"]),
            new LoopFanOutBranch("architecture", ["research"], [$"cas://artifact/{workItem.Id}/architecture/plan"]),
            new LoopFanOutBranch("security", ["research", "architecture"], [$"cas://artifact/{workItem.Id}/security/review"]),
            new LoopFanOutBranch("test", ["architecture"], [$"cas://artifact/{workItem.Id}/test/coverage"])
        };
        return new LoopStepContract(
            ContractId: $"{aggregate.Goal.Id}:{workItem.Id}:attempt-{attemptNumber}",
            ContextBundleId: $"{aggregate.Goal.CorrelationId}:{workItem.Id}:{phase}:{attemptNumber}",
            Objective: isRepair
                ? $"Repair verifier failures for work item {workItem.Id} in {workItem.Repository}."
                : $"Produce bounded specialist analysis for work item {workItem.Id} in {workItem.Repository}.",
            DownstreamConsumer: "loop_verifier",
            OutputSchema: "cas.loop.step-result.v1",
            ToolScope: new LoopToolScope(false, "implementation-owner", ["read_repo", "search_repo"]),
            FanOut: new LoopFanOutPlan(
                aggregate.Goal.Limits.MaxFanOut,
                branches,
                new LoopFanInRule("all_required_terminal", branches.Select(branch => branch.Role).ToArray(), true),
                "loop_verifier"),
            Inputs:
            [
                new("goalId", aggregate.Goal.Id),
                new("workItemId", workItem.Id),
                new("repository", workItem.Repository),
                new("provider", workItem.Provider),
                new("attempt", attemptNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("mode", phase),
            ],
            CompletionCriteria:
            [
                new("all-roles-terminal", "All required specialist branches must reach a terminal result.", true),
                new("all-results-schema-valid", "Each specialist result and the final aggregate must validate against the declared schema.", true),
                new("evidence-emitted", "Each required branch must emit at least one evidence URI.", true),
            ]);
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
            Events = [.. aggregate.Events, NewEvent(aggregate, "verification.completed", result.Outcome.ToString().ToLowerInvariant(), new
            {
                checkCount = result.Checks.Count,
                mandatoryFailures = result.Checks.Count(check => check.Mandatory && check.Outcome == VerificationOutcome.Failed),
                mandatoryInconclusive = result.Checks.Count(check => check.Mandatory && check.Outcome == VerificationOutcome.Inconclusive),
            })]
        };
    }

    private static GoalAggregate AddDecisionEvent(GoalAggregate aggregate, string type, string outcome, object payload) =>
        aggregate with { Events = [.. aggregate.Events, NewEvent(aggregate, type, outcome, payload)] };

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

    private static GoalEventRecord NewEvent(GoalAggregate aggregate, string type, string outcome, object? payload = null) =>
        new(Guid.NewGuid().ToString("N"), aggregate.Goal.Id,
            aggregate.Events.Count == 0 ? 1 : aggregate.Events.Max(item => item.Sequence) + 1,
            type, System.Text.Json.JsonSerializer.Serialize(payload is null ? new { outcome } : MergePayload(outcome, payload)), DateTimeOffset.UtcNow);

    private static object MergePayload(string outcome, object payload)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outcome"] = outcome,
        };
        foreach (var property in payload.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            values[property.Name] = property.GetValue(payload);
        return values;
    }
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

    public static void ValidateFanOutPlan(ExecutionLimits limits, LoopFanOutPlan plan)
    {
        if (plan.MaxConcurrency < 1)
            throw new InvalidOperationException("Loop fan-out plan must declare a positive max concurrency.");
        if (plan.MaxConcurrency > limits.MaxFanOut)
            throw new InvalidOperationException("Loop fan-out plan exceeds the goal concurrency policy.");
        if (!string.Equals(plan.AggregatorRole, "loop_verifier", StringComparison.Ordinal))
            throw new InvalidOperationException("Loop fan-out plan must route through loop_verifier.");
        if (plan.Branches.Count == 0)
            throw new InvalidOperationException("Loop fan-out plan requires at least one branch.");

        var roles = plan.Branches.Select(branch => branch.Role).ToArray();
        if (roles.Length != roles.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Loop fan-out plan cannot declare duplicate branch roles.");
        if (!roles.SequenceEqual(["research", "architecture", "security", "test"], StringComparer.Ordinal))
            throw new InvalidOperationException("Loop fan-out plan must declare the canonical specialist role set in order.");

        var roleSet = new HashSet<string>(roles, StringComparer.Ordinal);
        foreach (var branch in plan.Branches)
        {
            if (branch.ExpectedArtifacts.Count == 0)
                throw new InvalidOperationException($"Loop fan-out branch '{branch.Role}' must declare at least one expected artifact.");
            foreach (var dependency in branch.DependsOnRoles)
            {
                if (!roleSet.Contains(dependency))
                    throw new InvalidOperationException($"Loop fan-out branch '{branch.Role}' references unknown dependency '{dependency}'.");
                if (string.Equals(branch.Role, dependency, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Loop fan-out branch '{branch.Role}' cannot depend on itself.");
            }
        }

        var mutatingBranches = plan.Branches.Where(branch => branch.RepositoryMutationAllowed).ToArray();
        if (mutatingBranches.Length > 1 && mutatingBranches.Any(branch => string.IsNullOrWhiteSpace(branch.MergeStrategy)))
            throw new InvalidOperationException("Loop fan-out plan cannot assign multiple mutation owners without an explicit merge strategy.");

        var aggregatedRoles = plan.Aggregation.RequiredRoles.ToArray();
        if (!aggregatedRoles.SequenceEqual(roles, StringComparer.Ordinal))
            throw new InvalidOperationException("Loop fan-out aggregation must require the declared branch roles in canonical order.");
        if (!string.Equals(plan.Aggregation.RuleSet, "all_required_terminal", StringComparison.Ordinal) || !plan.Aggregation.RequireAllRequiredSucceeded)
            throw new InvalidOperationException("Loop fan-out aggregation must require all required roles to terminate successfully.");
    }
}
