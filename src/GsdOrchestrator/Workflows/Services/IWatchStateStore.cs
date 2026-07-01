namespace GsdOrchestrator.Workflows.Services;

public interface IWatchStateStore
{
    Task<bool> IsProcessedAsync(
        string owner,
        string repo,
        string key,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        string owner,
        string repo,
        string key,
        CancellationToken cancellationToken = default);
}
