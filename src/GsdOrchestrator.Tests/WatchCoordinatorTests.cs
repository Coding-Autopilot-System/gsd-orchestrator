using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GsdOrchestrator.Tests;

public class WatchCoordinatorTests
{
    [Fact]
    public async Task PollOnceAsync_TwoRepositories_VisitsBothInOrder()
    {
        var store = Substitute.For<IWatchStateStore>();
        store.IsProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var visited = new List<string>();
        var repos = new[] { new RepoConfig("org", "one", 0), new RepoConfig("org", "two", 0) };
        var sut = new WatchCoordinator(store, NullLogger<WatchCoordinator>.Instance);

        var result = await sut.PollOnceAsync(
            repos,
            (repo, _) => { visited.Add(repo.Repo); return Task.FromResult<IReadOnlyList<int>>([1]); },
            (_, _, _) => Task.FromResult(true),
            CancellationToken.None);

        Assert.Equal(["one", "two"], visited);
        Assert.Equal(2, result.Repositories.Count);
        Assert.All(result.Repositories, item => Assert.Null(item.Error));
    }

    [Fact]
    public async Task PollOnceAsync_FirstRepositoryFails_StillVisitsSecond()
    {
        var store = Substitute.For<IWatchStateStore>();
        var visited = new List<string>();
        var repos = new[] { new RepoConfig("org", "one", 0), new RepoConfig("org", "two", 0) };
        var sut = new WatchCoordinator(store, NullLogger<WatchCoordinator>.Instance);

        var result = await sut.PollOnceAsync(
            repos,
            (repo, _) =>
            {
                visited.Add(repo.Repo);
                if (repo.Repo == "one") throw new InvalidOperationException("first failed");
                return Task.FromResult<IReadOnlyList<int>>([]);
            },
            (_, _, _) => Task.FromResult(true),
            CancellationToken.None);

        Assert.Equal(["one", "two"], visited);
        Assert.NotNull(result.Repositories[0].Error);
        Assert.Null(result.Repositories[1].Error);
    }

    [Fact]
    public async Task PollOnceAsync_SuccessOnly_MarksOnlySuccessfulIssue()
    {
        var store = Substitute.For<IWatchStateStore>();
        store.IsProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var repo = new RepoConfig("org", "repo", 0);
        var sut = new WatchCoordinator(store, NullLogger<WatchCoordinator>.Instance);

        await sut.PollOnceAsync(
            [repo],
            (_, _) => Task.FromResult<IReadOnlyList<int>>([1, 2]),
            (_, issue, _) => Task.FromResult(issue == 1),
            CancellationToken.None);

        await store.Received(1).MarkProcessedAsync("org", "repo", "issue-1", Arg.Any<CancellationToken>());
        await store.DidNotReceive().MarkProcessedAsync("org", "repo", "issue-2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_AlreadyProcessed_SkipsDuplicate()
    {
        var store = Substitute.For<IWatchStateStore>();
        store.IsProcessedAsync("org", "repo", "issue-7", Arg.Any<CancellationToken>()).Returns(true);
        var processor = Substitute.For<Func<RepoConfig, int, CancellationToken, Task<bool>>>();
        var sut = new WatchCoordinator(store, NullLogger<WatchCoordinator>.Instance);

        await sut.PollOnceAsync(
            [new RepoConfig("org", "repo", 0)],
            (_, _) => Task.FromResult<IReadOnlyList<int>>([7]),
            processor,
            CancellationToken.None);

        await processor.DidNotReceive().Invoke(Arg.Any<RepoConfig>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_Cancelled_StopsImmediatelyWithoutMarking()
    {
        var store = Substitute.For<IWatchStateStore>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = new WatchCoordinator(store, NullLogger<WatchCoordinator>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.PollOnceAsync(
            [new RepoConfig("org", "repo", 0)],
            (_, _) => Task.FromResult<IReadOnlyList<int>>([1]),
            (_, _, _) => Task.FromResult(true),
            cts.Token));

        await store.DidNotReceive().MarkProcessedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
