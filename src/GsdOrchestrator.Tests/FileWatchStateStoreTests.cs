using GsdOrchestrator.Workflows.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class FileWatchStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"watch-store-{Guid.NewGuid():N}");

    [Fact]
    public async Task MarkProcessedAsync_SurvivesStoreRecreation()
    {
        var first = new FileWatchStateStore(_root, NullLogger<FileWatchStateStore>.Instance);
        await first.MarkProcessedAsync("org", "repo", "issue-42", CancellationToken.None);

        var second = new FileWatchStateStore(_root, NullLogger<FileWatchStateStore>.Instance);

        Assert.True(await second.IsProcessedAsync("org", "repo", "issue-42", CancellationToken.None));
    }

    [Fact]
    public async Task MarkProcessedAsync_SanitizesPathAndLeavesNoTemporaryFile()
    {
        var sut = new FileWatchStateStore(_root, NullLogger<FileWatchStateStore>.Instance);

        await sut.MarkProcessedAsync("../org", "repo/../../escape", "issue:7", CancellationToken.None);

        var files = Directory.GetFiles(Path.Combine(_root, ".gsd", "watch"));
        Assert.Single(files);
        Assert.EndsWith(".done", files[0]);
        Assert.DoesNotContain("..", Path.GetFileName(files[0]));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, ".gsd", "watch"), "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
