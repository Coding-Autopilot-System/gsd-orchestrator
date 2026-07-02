using GsdOrchestrator.Observability;
using GsdOrchestrator.Scheduling;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class OperatorProjectionTests
{
    [Fact]
    public void Project_ExposesCompleteDeterministicGoalView()
    {
        var limits = new ExecutionLimits(3, 4, 2, 600, 10, 2);
        var aggregate = new GoalAggregate(
            new GoalRecord("goal-1", "corr-1", GoalStatus.Failed, limits),
            [new("work-1", "goal-1", "repo", "provider", WorkItemStatus.Failed, 2, "idem-1")],
            [new("goal-1", "work-1", "work-0")],
            [new("attempt-1", "goal-1", "work-1", 1, AttemptStatus.Failed, DateTimeOffset.UnixEpoch)],
            [new("lease-1", "goal-1", "work-1", "worker-1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5))],
            [new("budget-1", "goal-1", "work-1", "provider", "repo", 3, 1)],
            [new("evidence-1", "goal-1", "work-1", "test", "file:///evidence/test.json")],
            [new("transition-1", "goal-1", "Verifying", "Failed", "UnrecoverableFault", DateTimeOffset.UnixEpoch)],
            [new("event-1", "goal-1", 1, "verification.decision", """{"outcome":"stop"}""", DateTimeOffset.UnixEpoch)],
            []);

        var view = OperatorProjection.Project(aggregate);

        Assert.Equal("corr-1", view.CorrelationId);
        Assert.Equal(GoalStatus.Failed, view.Status);
        Assert.Equal(7, view.RemainingModelCalls);
        Assert.Equal(1, view.RemainingAttempts);
        Assert.Equal(GoalStopReason.UnrecoverableFault, view.StopReason);
        Assert.Single(view.WorkItems);
        Assert.Single(view.Dependencies);
        Assert.Single(view.Leases);
        Assert.Single(view.Attempts);
        Assert.Single(view.Evidence);
        Assert.Single(view.Events);
    }

    [Fact]
    public void Project_RedactsNoDataBecauseAggregateContainsOnlyBoundedMetadata()
    {
        var aggregate = new GoalAggregate(
            new GoalRecord("goal-1", "corr-1", GoalStatus.Running, new(1, 1, 1, 60, 1, 1)),
            [], [], [], [], [], [], [], [], []);

        var view = OperatorProjection.Project(aggregate);

        Assert.Null(view.StopReason);
        Assert.Equal(1, view.RemainingModelCalls);
        Assert.Equal(0, view.RemainingAttempts);
    }
}
