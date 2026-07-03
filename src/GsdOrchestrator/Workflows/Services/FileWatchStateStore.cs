using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.Services;

public sealed class FileWatchStateStore : IWatchStateStore
{
    private static readonly Regex UnsafeSegment =
        new("[^a-zA-Z0-9_-]", RegexOptions.Compiled);

    private readonly string _stateDirectory;
    private readonly ILogger<FileWatchStateStore> _logger;

    public FileWatchStateStore(string rootPath, ILogger<FileWatchStateStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _stateDirectory = Path.GetFullPath(Path.Combine(rootPath, ".gsd", "watch"));
        _logger = logger;
        Directory.CreateDirectory(_stateDirectory);
    }

    public Task<bool> IsProcessedAsync(
        string owner,
        string repo,
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(GetPath(owner, repo, key)));
    }

    public async Task MarkProcessedAsync(
        string owner,
        string repo,
        string key,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(owner, repo, key);
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            _logger.LogDebug(
                "Recorded successful watch item {Owner}/{Repo} {WatchKey}",
                owner,
                repo,
                key);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private string GetPath(string owner, string repo, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Path.Combine(
            _stateDirectory,
            $"{Sanitize(owner)}_{Sanitize(repo)}_{Sanitize(key)}.done");
    }

    private static string Sanitize(string value) => UnsafeSegment.Replace(value, "_");
}
