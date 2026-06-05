using System.Text.Json;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Checkpointing;

public interface ICheckpointStore
{
    Task SaveAsync(GsdWorkflowContext ctx, CancellationToken ct = default);
    Task<GsdWorkflowContext?> LoadAsync(string workflowId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListActiveWorkflowsAsync(CancellationToken ct = default);
    Task ArchiveAsync(string workflowId, CancellationToken ct = default);
}

/// <summary>
/// Persists GsdWorkflowContext as JSON files under .gsd/state/{workflowId}.json.
/// Uses atomic write (temp file + rename) to prevent corrupt checkpoints.
/// </summary>
public sealed class FileCheckpointStore : ICheckpointStore
{
    private readonly string _stateDir;
    private readonly ILogger<FileCheckpointStore> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileCheckpointStore(string repoRoot, ILogger<FileCheckpointStore> logger)
    {
        _stateDir = Path.Combine(repoRoot, ".gsd", "state");
        _logger = logger;
        Directory.CreateDirectory(_stateDir);
    }

    public async Task SaveAsync(GsdWorkflowContext ctx, CancellationToken ct = default)
    {
        var path = StatePath(ctx.Issue?.RepoOwner ?? "", ctx.Issue?.RepoName ?? "", ctx.WorkflowId);
        var tmp = path + ".tmp";

        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(fs, ctx, JsonOpts, ct);

        // Atomic rename — prevents partial writes leaving corrupt checkpoints
        File.Move(tmp, path, overwrite: true);
        _logger.LogDebug("Checkpoint saved: {WorkflowId} → {State}", ctx.WorkflowId, ctx.CurrentState);
    }

    public async Task<GsdWorkflowContext?> LoadAsync(string workflowId, CancellationToken ct = default)
    {
        var path = StatePath(workflowId);
        if (!File.Exists(path)) return null;

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<GsdWorkflowContext>(fs, JsonOpts, ct);
    }

    public Task<IReadOnlyList<string>> ListActiveWorkflowsAsync(CancellationToken ct = default)
    {
        var ids = Directory.EnumerateFiles(_stateDir, "*.json")
            .Where(f => !f.EndsWith(".failed.json", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(ids);
    }

    public Task ArchiveAsync(string workflowId, CancellationToken ct = default)
    {
        var src = StatePath(workflowId);
        if (!File.Exists(src)) return Task.CompletedTask;

        var archiveDir = Path.Combine(_stateDir, "..", "archive");
        Directory.CreateDirectory(archiveDir);
        File.Move(src, Path.Combine(archiveDir, $"{workflowId}.json"), overwrite: true);
        return Task.CompletedTask;
    }

    private string StatePath(string workflowId) =>
        Path.Combine(_stateDir, $"{workflowId}.json");

    /// <summary>Per-repo namespaced path — MULTI-03. New saves include owner+repo prefix.</summary>
    private string StatePath(string owner, string repo, string workflowId)
    {
        var prefix = (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
            ? workflowId
            : $"{Sanitize(owner)}_{Sanitize(repo)}_{workflowId}";
        return Path.Combine(_stateDir, $"{prefix}.json");
    }

    /// <summary>
    /// T-16-05: Sanitize owner/repo by replacing path-traversal characters with underscores.
    /// Prevents a crafted GSD_REPOS value from writing checkpoint files outside _stateDir.
    /// </summary>
    private static string Sanitize(string segment) =>
        segment.Replace('/', '_').Replace('\\', '_').Replace("..", "__");
}
