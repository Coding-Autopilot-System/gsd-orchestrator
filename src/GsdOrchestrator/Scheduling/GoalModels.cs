namespace GsdOrchestrator.Scheduling;

public enum GoalStatus { Draft, Refined, Planned, Running, Integrating, Verifying, Completed, WaitingApproval, RetryScheduled, Paused, Cancelled, Blocked, Failed, BudgetExhausted }
public enum WorkItemStatus { Pending, Ready, Running, Succeeded, Failed, Cancelled }
public enum AttemptStatus { Running, Succeeded, Failed, Cancelled }
public enum FailureClass { Transient, Deterministic, Policy, Cancellation, Unrecoverable }
public enum GoalStopReason { Passed, Exhaustion, Cancellation, Denial, ApprovalWait, Deadlock, UnrecoverableFault, NoProgress }

public sealed record ExecutionLimits(int MaxFanOut, int MaxIterations, int MaxAttemptsPerWorkItem, int MaxRuntimeSeconds, int MaxModelCalls, int NoProgressLimit);
public sealed record GoalRecord(string Id, string CorrelationId, GoalStatus Status, ExecutionLimits Limits);
public sealed record WorkItemRecord(string Id, string GoalId, string Repository, string Provider, WorkItemStatus Status, int MaxAttempts, string IdempotencyKey);
public sealed record DependencyRecord(string GoalId, string WorkItemId, string DependsOnWorkItemId);
public sealed record AttemptRecord(string Id, string GoalId, string WorkItemId, int Number, AttemptStatus Status, DateTimeOffset StartedAt);
public sealed record LeaseRecord(string Id, string GoalId, string WorkItemId, string Owner, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt);
public sealed record BudgetReservationRecord(string Id, string GoalId, string WorkItemId, string Provider, string Repository, int ModelCalls, int ConcurrencyUnits);
public sealed record EvidenceRecord(string Id, string GoalId, string? WorkItemId, string Kind, string Uri);
public sealed record TransitionRecord(string Id, string GoalId, string From, string To, string Reason, DateTimeOffset OccurredAt);
public sealed record GoalEventRecord(string Id, string GoalId, long Sequence, string Type, string PayloadJson, DateTimeOffset OccurredAt);
public sealed record IdempotencyRecord(string Key, string GoalId, string WorkItemId, string EffectType, DateTimeOffset ReservedAt);

public sealed record GoalAggregate(
    GoalRecord Goal,
    IReadOnlyList<WorkItemRecord> WorkItems,
    IReadOnlyList<DependencyRecord> Dependencies,
    IReadOnlyList<AttemptRecord> Attempts,
    IReadOnlyList<LeaseRecord> Leases,
    IReadOnlyList<BudgetReservationRecord> BudgetReservations,
    IReadOnlyList<EvidenceRecord> Evidence,
    IReadOnlyList<TransitionRecord> Transitions,
    IReadOnlyList<GoalEventRecord> Events,
    IReadOnlyList<IdempotencyRecord> IdempotencyKeys);

public sealed record DatabaseSettings(string JournalMode, bool ForeignKeys);
public sealed record LeaseRequest(string GoalId, string WorkItemId, string Owner, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt, int GlobalLimit, int ProviderLimit, int RepositoryLimit);
