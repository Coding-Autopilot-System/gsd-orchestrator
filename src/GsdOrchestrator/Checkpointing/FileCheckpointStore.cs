using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly string _archiveDir;
    private readonly ILogger<FileCheckpointStore> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// CR-02/T-16-05: Allowlist-based sanitizer — only alphanumerics, hyphens, and underscores.
    /// Replaces every other character with '_' to prevent path traversal.
    /// </summary>
    private static readonly Regex SafeSegment =
        new(@"[^a-zA-Z0-9\-_]", RegexOptions.Compiled);

    private static string Sanitize(string segment) =>
        SafeSegment.Replace(segment, "_");

    public FileCheckpointStore(string repoRoot, ILogger<FileCheckpointStore> logger)
    {
        _stateDir   = Path.Combine(repoRoot, ".gsd", "state");
        _archiveDir = Path.GetFullPath(Path.Combine(repoRoot, ".gsd", "archive"));
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

    private const string CurrentSchemaVersion = "1.0";

    /// <summary>
    /// CR-03: Try exact (legacy) path first, then scan for namespaced *_{workflowId}.json.
    /// This allows resuming workflows saved after Phase 16 namespacing was introduced.
    /// Phase 18: Validates SchemaVersion — returns null and logs a warning on mismatch.
    /// </summary>
    public async Task<GsdWorkflowContext?> LoadAsync(string workflowId, CancellationToken ct = default)
    {
        // Try exact match first (legacy / no-owner-repo path)
        var exactPath = StatePath(workflowId);
        if (File.Exists(exactPath))
        {
            await using var fs = new FileStream(exactPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var ctx = await JsonSerializer.DeserializeAsync<GsdWorkflowContext>(fs, JsonOpts, ct);
            return ValidateSchemaVersion(ctx, workflowId);
        }

        // Try namespaced match: *_{sanitizedWorkflowId}.json
        var sanitized = Sanitize(workflowId);
        var candidates = Directory.GetFiles(_stateDir, $"*_{sanitized}.json");
        if (candidates.Length == 1)
        {
            await using var fs2 = new FileStream(candidates[0], FileMode.Open, FileAccess.Read, FileShare.Read);
            var ctx2 = await JsonSerializer.DeserializeAsync<GsdWorkflowContext>(fs2, JsonOpts, ct);
            return ValidateSchemaVersion(ctx2, workflowId);
        }

        return null;
    }

    private GsdWorkflowContext? ValidateSchemaVersion(GsdWorkflowContext? ctx, string workflowId)
    {
        if (ctx is null) return null;
        if (ctx.SchemaVersion != CurrentSchemaVersion)
        {
            _logger.LogWarning(
                "Checkpoint schema version mismatch: expected {Expected}, found {Found}. Starting fresh.",
                CurrentSchemaVersion, ctx.SchemaVersion);
            return null;
        }
        return ctx;
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

    /// <summary>
    /// CR-03: Try exact path first, then scan for namespaced *_{workflowId}.json.
    /// WR-03: Uses _archiveDir computed once in the constructor.
    /// </summary>
    public Task ArchiveAsync(string workflowId, CancellationToken ct = default)
    {
        // Try exact match first
        var src = StatePath(workflowId);
        if (!File.Exists(src))
        {
            // Try namespaced match
            var sanitized = Sanitize(workflowId);
            var candidates = Directory.GetFiles(_stateDir, $"*_{sanitized}.json");
            if (candidates.Length == 1)
                src = candidates[0];
            else
                return Task.CompletedTask;
        }

        Directory.CreateDirectory(_archiveDir);
        var destName = Path.GetFileName(src);
        File.Move(src, Path.Combine(_archiveDir, destName), overwrite: true);
        return Task.CompletedTask;
    }

    /// <summary>CR-01: Sanitize workflowId to prevent path traversal.</summary>
    private string StatePath(string workflowId) =>
        Path.Combine(_stateDir, $"{Sanitize(workflowId)}.json");

    /// <summary>Per-repo namespaced path — MULTI-03. New saves include owner+repo prefix.</summary>
    private string StatePath(string owner, string repo, string workflowId)
    {
        var prefix = (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
            ? Sanitize(workflowId)
            : $"{Sanitize(owner)}_{Sanitize(repo)}_{Sanitize(workflowId)}";
        return Path.Combine(_stateDir, $"{prefix}.json");
    }
}
