namespace GsdOrchestrator.Scheduling;

public interface IGoalStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default);
    Task<GoalAggregate?> LoadAsync(string goalId, CancellationToken cancellationToken = default);
    Task<LeaseRecord?> TryAcquireLeaseAsync(LeaseRequest request, CancellationToken cancellationToken = default);
    Task<int> RecoverExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> TryReserveIdempotencyKeyAsync(string goalId, string workItemId, string key, string effectType, CancellationToken cancellationToken = default);
}
