using GsdOrchestrator.Scheduling;

namespace GsdOrchestrator.Observability;

public sealed record OperatorGoalView(
    string GoalId,
    string CorrelationId,
    GoalStatus Status,
    IReadOnlyList<WorkItemRecord> WorkItems,
    IReadOnlyList<DependencyRecord> Dependencies,
    IReadOnlyList<LeaseRecord> Leases,
    IReadOnlyList<AttemptRecord> Attempts,
    IReadOnlyList<EvidenceRecord> Evidence,
    IReadOnlyList<GoalEventRecord> Events,
    int RemainingModelCalls,
    int RemainingAttempts,
    GoalStopReason? StopReason);

public static class OperatorProjection
{
    public static OperatorGoalView Project(GoalAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var consumedCalls = aggregate.BudgetReservations.Sum(item => item.ModelCalls);
        var remainingCalls = Math.Max(0, aggregate.Goal.Limits.MaxModelCalls - consumedCalls);
        var remainingAttempts = aggregate.WorkItems.Sum(workItem =>
            Math.Max(0, workItem.MaxAttempts - aggregate.Attempts.Count(attempt => attempt.WorkItemId == workItem.Id)));
        var stopReason = ParseStopReason(aggregate.Transitions.LastOrDefault()?.Reason);

        return new OperatorGoalView(
            aggregate.Goal.Id,
            aggregate.Goal.CorrelationId,
            aggregate.Goal.Status,
            aggregate.WorkItems,
            aggregate.Dependencies,
            aggregate.Leases,
            aggregate.Attempts,
            aggregate.Evidence,
            aggregate.Events,
            remainingCalls,
            remainingAttempts,
            stopReason);
    }

    private static GoalStopReason? ParseStopReason(string? reason) =>
        Enum.TryParse<GoalStopReason>(reason, ignoreCase: true, out var parsed) ? parsed : null;
}
