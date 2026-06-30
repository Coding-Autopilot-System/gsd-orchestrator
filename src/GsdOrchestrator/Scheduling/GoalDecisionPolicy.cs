namespace GsdOrchestrator.Scheduling;

public enum DecisionAction { Retry, CreateRepair, Stop }

public sealed record GoalDecision(
    DecisionAction Action,
    FailureClass? FailureClass,
    GoalStopReason? StopReason,
    int ConsumedAttempts,
    string Reason,
    IReadOnlyList<string> EvidenceIds);

public static class GoalDecisionPolicy
{
    public static GoalDecision DecideFailure(
        FailureClass failureClass,
        int consumedAttempts,
        int maxAttempts,
        int noProgressCount,
        int noProgressLimit,
        IReadOnlyList<string> evidenceIds)
    {
        if (consumedAttempts < 0 || maxAttempts < 1 || noProgressCount < 0 || noProgressLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(consumedAttempts), "Decision budgets must be non-negative and limits positive.");
        RequireEvidence(evidenceIds);

        if (noProgressCount >= noProgressLimit)
            return new(DecisionAction.Stop, failureClass, GoalStopReason.NoProgress, consumedAttempts, "Normalized verifier failure reached the no-progress limit.", evidenceIds);
        if (consumedAttempts >= maxAttempts)
            return new(DecisionAction.Stop, failureClass, GoalStopReason.Exhaustion, consumedAttempts, "Work item attempt budget is exhausted.", evidenceIds);

        return failureClass switch
        {
            FailureClass.Transient => new(DecisionAction.Retry, failureClass, null, consumedAttempts, "Transient failure may retry within budget.", evidenceIds),
            FailureClass.Deterministic => new(DecisionAction.CreateRepair, failureClass, null, consumedAttempts, "Deterministic failure requires a new repair item with prior evidence.", evidenceIds),
            FailureClass.Policy => new(DecisionAction.Stop, failureClass, GoalStopReason.Denial, consumedAttempts, "Deterministic policy denied execution.", evidenceIds),
            FailureClass.Cancellation => new(DecisionAction.Stop, failureClass, GoalStopReason.Cancellation, consumedAttempts, "Cancellation prevents further dispatch.", evidenceIds),
            FailureClass.Unrecoverable => new(DecisionAction.Stop, failureClass, GoalStopReason.UnrecoverableFault, consumedAttempts, "Failure is classified as unrecoverable.", evidenceIds),
            _ => throw new ArgumentOutOfRangeException(nameof(failureClass))
        };
    }

    public static GoalDecision Stop(GoalStopReason reason, string explanation, IReadOnlyList<string> evidenceIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        RequireEvidence(evidenceIds);
        return new(DecisionAction.Stop, null, reason, 0, explanation, evidenceIds);
    }

    private static void RequireEvidence(IReadOnlyList<string> evidenceIds)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        if (evidenceIds.Count == 0 || evidenceIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one valid evidence identifier is required.", nameof(evidenceIds));
    }
}
