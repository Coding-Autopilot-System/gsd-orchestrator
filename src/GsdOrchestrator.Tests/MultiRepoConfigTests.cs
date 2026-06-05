using System.IO;
using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public class MultiRepoConfigTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    // ── Test 1: MULTI-01 — GSD_REPOS JSON array parsed into RepoConfig list ──
    [Fact]
    public void Load_WithGsdReposJsonArray_ReturnsAllRepos()
    {
        var config = BuildConfig(new()
        {
            ["GSD_REPOS"] =
                """[{"owner":"org1","repo":"repo-a"},{"owner":"org2","repo":"repo-b","rateLimitDelaySeconds":60}]"""
        });
        var result = RepoConfigLoader.Load(config); // throws NotImplementedException (RED)
        Assert.Equal(2, result.Count);
        Assert.Equal("org1", result[0].Owner);
        Assert.Equal("repo-a", result[0].Repo);
        Assert.Equal(30, result[0].RateLimitDelaySeconds); // default
        Assert.Equal("org2", result[1].Owner);
        Assert.Equal("repo-b", result[1].Repo);
        Assert.Equal(60, result[1].RateLimitDelaySeconds);
    }

    // ── Test 2: MULTI-01 — fallback to GSD_GITHUB_OWNER + GSD_GITHUB_REPO ──
    [Fact]
    public void Load_WithLegacyEnvVars_ReturnsSingleRepoConfig()
    {
        var config = BuildConfig(new()
        {
            ["GSD_GITHUB_OWNER"] = "legacy-owner",
            ["GSD_GITHUB_REPO"] = "legacy-repo"
        });
        var result = RepoConfigLoader.Load(config); // throws NotImplementedException (RED)
        Assert.Single(result);
        Assert.Equal("legacy-owner", result[0].Owner);
        Assert.Equal("legacy-repo", result[0].Repo);
        Assert.Equal(30, result[0].RateLimitDelaySeconds);
    }

    // ── Test 3: MULTI-01 — missing both config sources throws ────────────────
    [Fact]
    public void Load_WithNoRepoConfig_ThrowsInvalidOperationException()
    {
        var config = BuildConfig(new());
        // throws NotImplementedException from stub (RED — will become InvalidOperationException in GREEN)
        Assert.Throws<InvalidOperationException>(() => RepoConfigLoader.Load(config));
    }

    // ── Test 4: MULTI-04 — rateLimitDelaySeconds defaults to 30 ─────────────
    [Fact]
    public void Load_WithGsdReposOmittingDelay_DefaultsTo30Seconds()
    {
        var config = BuildConfig(new()
        {
            ["GSD_REPOS"] = """[{"owner":"acme","repo":"api"}]"""
        });
        var result = RepoConfigLoader.Load(config); // throws NotImplementedException (RED)
        Assert.Single(result);
        Assert.Equal(30, result[0].RateLimitDelaySeconds);
    }

    // ── Test 5: MULTI-01 — GSD_REPOS takes priority over legacy env vars ────
    [Fact]
    public void Load_WhenBothGsdReposAndLegacyPresent_PrefersGsdRepos()
    {
        var config = BuildConfig(new()
        {
            ["GSD_REPOS"] = """[{"owner":"new-owner","repo":"new-repo"}]""",
            ["GSD_GITHUB_OWNER"] = "old-owner",
            ["GSD_GITHUB_REPO"] = "old-repo"
        });
        var result = RepoConfigLoader.Load(config); // throws NotImplementedException (RED)
        Assert.Single(result);
        Assert.Equal("new-owner", result[0].Owner);
        Assert.Equal("new-repo", result[0].Repo);
    }

    // ── Test 6: MULTI-01 — malformed GSD_REPOS JSON throws ──────────────────
    [Fact]
    public void Load_WithMalformedGsdReposJson_ThrowsException()
    {
        var config = BuildConfig(new()
        {
            ["GSD_REPOS"] = "not-valid-json"
        });
        // throws NotImplementedException from stub (RED — will become JsonException in GREEN)
        Assert.Throws<System.Text.Json.JsonException>(() => RepoConfigLoader.Load(config));
    }

    // ── Test 7: MULTI-03 — StatePath namespaces checkpoint file ─── (GREEN)
    [Fact]
    public async Task SaveAsync_WithIssueContext_CreatesNamespacedCheckpointFile()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"gsd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var store = new FileCheckpointStore(tmpDir, NullLogger<FileCheckpointStore>.Instance);
            var ctx = new GsdWorkflowContext
            {
                Issue = new IssueContext(42, "title", "body", [], "myorg", "myrepo", "main"),
                WorkflowId = "abc123"
            };

            await store.SaveAsync(ctx, CancellationToken.None);

            var stateDir = Path.Combine(tmpDir, ".gsd", "state");
            var files = Directory.GetFiles(stateDir, "*.json");
            Assert.Single(files);
            Assert.Contains("myorg_myrepo_abc123.json", Path.GetFileName(files[0]));
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }
}
