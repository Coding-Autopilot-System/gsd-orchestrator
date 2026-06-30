using GsdOrchestrator.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class GoalControlPlaneTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"control-plane-{Guid.NewGuid():N}");

    [Fact]
    public async Task StartInspectCancel_PersistsExplicitLifecycle()
    {
        var (store, sut) = await Create();
        var aggregate = GoalFixtures.CompleteAggregate() with
        {
            Goal = GoalFixtures.CompleteAggregate().Goal with { Status = GoalStatus.Planned },
            Transitions = [], Events = [], Evidence = [], Leases = [], BudgetReservations = [], Attempts = [], IdempotencyKeys = []
        };

        await sut.StartAsync(aggregate);
        Assert.Equal(GoalStatus.Running, (await sut.InspectAsync("goal-1"))!.Goal.Status);

        await sut.CancelAsync("goal-1", "operator request", "cas://evidence/cancel");
        var cancelled = await store.LoadAsync("goal-1");
        Assert.Equal(GoalStatus.Cancelled, cancelled!.Goal.Status);
        Assert.Contains(cancelled.Transitions, transition => transition.To == nameof(GoalStatus.Cancelled));
        Assert.Contains(cancelled.Evidence, evidence => evidence.Uri == "cas://evidence/cancel");
    }

    [Fact]
    public async Task Resume_PausedGoal_ReturnsToRunningWithoutLosingEvidence()
    {
        var (store, sut) = await Create();
        var aggregate = GoalFixtures.CompleteAggregate() with
        {
            Goal = GoalFixtures.CompleteAggregate().Goal with { Status = GoalStatus.Paused }
        };
        await store.SaveAsync(aggregate);

        await sut.ResumeAsync("goal-1", "operator resume");
        var resumed = await sut.InspectAsync("goal-1");
        Assert.Equal(GoalStatus.Running, resumed!.Goal.Status);
        Assert.Equal(aggregate.Evidence.Count, resumed.Evidence.Count);
    }

    private async Task<(SqliteGoalStore Store, GoalControlPlane Sut)> Create()
    {
        var store = new SqliteGoalStore(Path.Combine(_root, "goals.db"), NullLogger<SqliteGoalStore>.Instance);
        await store.InitializeAsync();
        return (store, new GoalControlPlane(store, NullLogger<GoalControlPlane>.Instance));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
