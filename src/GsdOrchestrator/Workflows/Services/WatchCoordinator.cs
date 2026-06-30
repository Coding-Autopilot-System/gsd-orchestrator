using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.Services;

public sealed record WatchRepositoryResult(
    RepoConfig Repository,
    int Discovered,
    int Succeeded,
    string? Error = null);

public sealed record WatchIntervalResult(IReadOnlyList<WatchRepositoryResult> Repositories);

public sealed class WatchCoordinator
{
    private readonly IWatchStateStore _stateStore;
    private readonly ILogger<WatchCoordinator> _logger;

    public WatchCoordinator(IWatchStateStore stateStore, ILogger<WatchCoordinator> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<WatchIntervalResult> PollOnceAsync(
        IReadOnlyList<RepoConfig> repositories,
        Func<RepoConfig, CancellationToken, Task<IReadOnlyList<int>>> listOpenIssues,
        Func<RepoConfig, int, CancellationToken, Task<bool>> processIssue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(listOpenIssues);
        ArgumentNullException.ThrowIfNull(processIssue);

        var results = new List<WatchRepositoryResult>(repositories.Count);
        foreach (var repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var issues = await listOpenIssues(repository, cancellationToken);
                var succeeded = 0;
                for (var index = 0; index < issues.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var issueNumber = issues[index];
                    var key = $"issue-{issueNumber}";
                    if (await _stateStore.IsProcessedAsync(
                            repository.Owner,
                            repository.Repo,
                            key,
                            cancellationToken))
                        continue;

                    try
                    {
                        if (await processIssue(repository, issueNumber, cancellationToken))
                        {
                            await _stateStore.MarkProcessedAsync(
                                repository.Owner,
                                repository.Repo,
                                key,
                                cancellationToken);
                            succeeded++;
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Watch issue failed for {Owner}/{Repo}#{IssueNumber}",
                            repository.Owner,
                            repository.Repo,
                            issueNumber);
                    }

                    if (index < issues.Count - 1 && repository.RateLimitDelaySeconds > 0)
                        await Task.Delay(TimeSpan.FromSeconds(repository.RateLimitDelaySeconds), cancellationToken);
                }

                results.Add(new WatchRepositoryResult(repository, issues.Count, succeeded));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Watch repository pass failed for {Owner}/{Repo}; continuing interval",
                    repository.Owner,
                    repository.Repo);
                results.Add(new WatchRepositoryResult(repository, 0, 0, exception.Message));
            }
        }

        return new WatchIntervalResult(results);
    }
}
