using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Scheduling;

public sealed class GoalScheduler
{
    private readonly IGoalStore _store;
    private readonly ILogger<GoalScheduler> _logger;

    public GoalScheduler(IGoalStore store, ILogger<GoalScheduler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<LeaseRecord?> TryAcquireLeaseAsync(LeaseRequest request, CancellationToken cancellationToken = default)
    {
        var lease = await _store.TryAcquireLeaseAsync(request, cancellationToken);
        if (lease is null)
            _logger.LogDebug("Lease denied for goal {GoalId} work item {WorkItemId}", request.GoalId, request.WorkItemId);
        else
            _logger.LogInformation("Lease {LeaseId} acquired for goal {GoalId} work item {WorkItemId}", lease.Id, request.GoalId, request.WorkItemId);
        return lease;
    }

    public Task<int> RecoverExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        _store.RecoverExpiredLeasesAsync(now, cancellationToken);
}
