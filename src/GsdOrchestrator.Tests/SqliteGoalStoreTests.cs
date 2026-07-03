using System.Text.Json;
using GsdOrchestrator.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class SqliteGoalStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"goal-store-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndReload_ReconstructsEveryAuthoritativeProjection()
    {
        var path = Path.Combine(_root, "goals.db");
        var original = GoalFixtures.CompleteAggregate();
        var first = new SqliteGoalStore(path, NullLogger<SqliteGoalStore>.Instance);
        await first.InitializeAsync();
        await first.SaveAsync(original);

        var restarted = new SqliteGoalStore(path, NullLogger<SqliteGoalStore>.Instance);
        await restarted.InitializeAsync();
        var loaded = await restarted.LoadAsync(original.Goal.Id);

        Assert.NotNull(loaded);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(loaded));
    }

    [Fact]
    public async Task Initialize_EnablesWalAndForeignKeys()
    {
        var store = new SqliteGoalStore(Path.Combine(_root, "settings.db"), NullLogger<SqliteGoalStore>.Instance);
        await store.InitializeAsync();
        var settings = await store.GetDatabaseSettingsAsync();
        Assert.Equal("wal", settings.JournalMode, StringComparer.OrdinalIgnoreCase);
        Assert.True(settings.ForeignKeys);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}

internal static class GoalFixtures
{
    public static GoalAggregate CompleteAggregate()
    {
        var now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var goal = new GoalRecord("goal-1", "corr-1", GoalStatus.Running, new ExecutionLimits(3, 3, 3, 1800, 20, 2));
        return new GoalAggregate(
            goal,
            [new WorkItemRecord("work-1", goal.Id, "org/repo", "local", WorkItemStatus.Running, 3, "idem-1")],
            [new DependencyRecord(goal.Id, "work-1", "bootstrap")],
            [new AttemptRecord("attempt-1", goal.Id, "work-1", 1, AttemptStatus.Running, now)],
            [new LeaseRecord("lease-1", goal.Id, "work-1", "worker-1", now, now.AddMinutes(5))],
            [new BudgetReservationRecord("budget-1", goal.Id, "work-1", "local", "org/repo", 1, 1)],
            [new EvidenceRecord("evidence-1", goal.Id, "work-1", "test", "cas://evidence/1")],
            [new TransitionRecord("transition-1", goal.Id, "Draft", "Running", "accepted", now)],
            [new GoalEventRecord("event-1", goal.Id, 1, "goal.started", "{}", now)],
            [new IdempotencyRecord("idem-1", goal.Id, "work-1", "commit", now)]);
    }
}
