using GsdOrchestrator.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class GoalSchedulerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"scheduler-{Guid.NewGuid():N}");

    [Fact]
    public async Task TryAcquireLease_IncompleteDependency_IsDenied()
    {
        var store = await CreateStore();
        var aggregate = AggregateWithTwoItems(WorkItemStatus.Pending);
        await store.SaveAsync(aggregate);
        var scheduler = new GoalScheduler(store, NullLogger<GoalScheduler>.Instance);

        var lease = await scheduler.TryAcquireLeaseAsync(Request("work-2"));

        Assert.Null(lease);
    }

    [Fact]
    public async Task TryAcquireLease_CompetingCalls_RespectsGlobalLimitAtomically()
    {
        var store = await CreateStore();
        await store.SaveAsync(IndependentAggregate());
        var scheduler = new GoalScheduler(store, NullLogger<GoalScheduler>.Instance);

        var results = await Task.WhenAll(
            scheduler.TryAcquireLeaseAsync(Request("work-1", global: 1)),
            scheduler.TryAcquireLeaseAsync(Request("work-2", global: 1)));

        Assert.Single(results, result => result is not null);
    }

    [Theory]
    [InlineData(10, 1, 10)]
    [InlineData(10, 10, 1)]
    public async Task TryAcquireLease_ProviderOrRepositoryLimit_DeniesSecond(int global, int provider, int repository)
    {
        var store = await CreateStore();
        await store.SaveAsync(IndependentAggregate());
        var scheduler = new GoalScheduler(store, NullLogger<GoalScheduler>.Instance);
        Assert.NotNull(await scheduler.TryAcquireLeaseAsync(Request("work-1", global, provider, repository)));
        Assert.Null(await scheduler.TryAcquireLeaseAsync(Request("work-2", global, provider, repository)));
    }

    [Fact]
    public async Task RecoverExpiredLeases_AllowsReacquire()
    {
        var store = await CreateStore();
        await store.SaveAsync(IndependentAggregate());
        var scheduler = new GoalScheduler(store, NullLogger<GoalScheduler>.Instance);
        var now = DateTimeOffset.UtcNow;
        Assert.NotNull(await scheduler.TryAcquireLeaseAsync(
            new LeaseRequest("goal-1", "work-1", "worker-1", now.AddMinutes(-2), now.AddMinutes(-1), 3, 3, 3)));

        Assert.Equal(1, await scheduler.RecoverExpiredLeasesAsync(now));
        Assert.NotNull(await scheduler.TryAcquireLeaseAsync(Request("work-1")));
    }

    [Fact]
    public async Task ReserveIdempotencyKey_RestartRejectsDuplicateEffect()
    {
        var path = Path.Combine(_root, "goals.db");
        var first = new SqliteGoalStore(path, NullLogger<SqliteGoalStore>.Instance);
        await first.InitializeAsync();
        await first.SaveAsync(IndependentAggregate());
        Assert.True(await first.TryReserveIdempotencyKeyAsync("goal-1", "work-1", "commit:abc", "commit"));

        var restarted = new SqliteGoalStore(path, NullLogger<SqliteGoalStore>.Instance);
        await restarted.InitializeAsync();
        Assert.False(await restarted.TryReserveIdempotencyKeyAsync("goal-1", "work-1", "commit:abc", "commit"));
    }

    private LeaseRequest Request(string workItemId, int global = 3, int provider = 3, int repository = 3, DateTimeOffset? expiresAt = null) =>
        new("goal-1", workItemId, "worker-1", DateTimeOffset.UtcNow, expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5), global, provider, repository);

    private async Task<SqliteGoalStore> CreateStore()
    {
        var store = new SqliteGoalStore(Path.Combine(_root, "goals.db"), NullLogger<SqliteGoalStore>.Instance);
        await store.InitializeAsync();
        return store;
    }

    private static GoalAggregate AggregateWithTwoItems(WorkItemStatus dependencyStatus)
    {
        var seed = GoalFixtures.CompleteAggregate();
        return seed with
        {
            WorkItems =
            [
                new WorkItemRecord("work-1", "goal-1", "org/repo", "local", dependencyStatus, 3, "idem-1"),
                new WorkItemRecord("work-2", "goal-1", "org/repo", "local", WorkItemStatus.Ready, 3, "idem-2")
            ],
            Dependencies = [new DependencyRecord("goal-1", "work-2", "work-1")],
            Attempts = [], Leases = [], BudgetReservations = [], IdempotencyKeys = []
        };
    }

    private static GoalAggregate IndependentAggregate()
    {
        var seed = GoalFixtures.CompleteAggregate();
        return seed with
        {
            WorkItems =
            [
                new WorkItemRecord("work-1", "goal-1", "org/repo", "local", WorkItemStatus.Ready, 3, "idem-1"),
                new WorkItemRecord("work-2", "goal-1", "org/repo", "local", WorkItemStatus.Ready, 3, "idem-2")
            ],
            Dependencies = [], Attempts = [], Leases = [], BudgetReservations = [], IdempotencyKeys = []
        };
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
