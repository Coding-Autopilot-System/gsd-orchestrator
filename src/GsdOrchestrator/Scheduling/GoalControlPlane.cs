using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Scheduling;

public sealed class GoalControlPlane
{
    private readonly IGoalStore _store;
    private readonly ILogger<GoalControlPlane> _logger;

    public GoalControlPlane(IGoalStore store, ILogger<GoalControlPlane> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate.Goal.Status != GoalStatus.Planned)
            throw new InvalidOperationException("Only a validated Planned goal can start.");
        var now = DateTimeOffset.UtcNow;
        var started = aggregate with
        {
            Goal = aggregate.Goal with { Status = GoalStatus.Running },
            Transitions = [.. aggregate.Transitions, new TransitionRecord(Guid.NewGuid().ToString("N"), aggregate.Goal.Id, nameof(GoalStatus.Planned), nameof(GoalStatus.Running), "Goal started", now)],
            Events = [.. aggregate.Events, new GoalEventRecord(Guid.NewGuid().ToString("N"), aggregate.Goal.Id, NextSequence(aggregate), "goal.started", "{}", now)]
        };
        await _store.SaveAsync(started, cancellationToken);
        _logger.LogInformation("Goal {GoalId} started", aggregate.Goal.Id);
    }

    public Task<GoalAggregate?> InspectAsync(string goalId, CancellationToken cancellationToken = default) =>
        _store.LoadAsync(goalId, cancellationToken);

    public async Task CancelAsync(string goalId, string reason, string evidenceUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceUri);
        var aggregate = await Required(goalId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await _store.SaveAsync(aggregate with
        {
            Goal = aggregate.Goal with { Status = GoalStatus.Cancelled },
            Evidence = [.. aggregate.Evidence, new EvidenceRecord(Guid.NewGuid().ToString("N"), goalId, null, "cancellation", evidenceUri)],
            Transitions = [.. aggregate.Transitions, new TransitionRecord(Guid.NewGuid().ToString("N"), goalId, aggregate.Goal.Status.ToString(), nameof(GoalStatus.Cancelled), reason, now)],
            Events = [.. aggregate.Events, new GoalEventRecord(Guid.NewGuid().ToString("N"), goalId, NextSequence(aggregate), "goal.cancelled", "{}", now)],
            Leases = [],
            BudgetReservations = []
        }, cancellationToken);
    }

    public async Task ResumeAsync(string goalId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var aggregate = await Required(goalId, cancellationToken);
        if (aggregate.Goal.Status is not GoalStatus.Paused and not GoalStatus.Blocked and not GoalStatus.RetryScheduled)
            throw new InvalidOperationException($"Goal '{goalId}' cannot resume from {aggregate.Goal.Status}.");
        var now = DateTimeOffset.UtcNow;
        await _store.SaveAsync(aggregate with
        {
            Goal = aggregate.Goal with { Status = GoalStatus.Running },
            Transitions = [.. aggregate.Transitions, new TransitionRecord(Guid.NewGuid().ToString("N"), goalId, aggregate.Goal.Status.ToString(), nameof(GoalStatus.Running), reason, now)],
            Events = [.. aggregate.Events, new GoalEventRecord(Guid.NewGuid().ToString("N"), goalId, NextSequence(aggregate), "goal.resumed", "{}", now)]
        }, cancellationToken);
    }

    private async Task<GoalAggregate> Required(string goalId, CancellationToken cancellationToken) =>
        await _store.LoadAsync(goalId, cancellationToken) ?? throw new InvalidOperationException($"Goal '{goalId}' was not found.");

    private static long NextSequence(GoalAggregate aggregate) => aggregate.Events.Count == 0 ? 1 : aggregate.Events.Max(entry => entry.Sequence) + 1;
}
