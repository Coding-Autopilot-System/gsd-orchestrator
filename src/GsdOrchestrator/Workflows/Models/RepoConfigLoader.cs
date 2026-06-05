using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace GsdOrchestrator.Workflows.Models;

/// <summary>
/// Loads the list of repos to watch from IConfiguration.
/// MULTI-01: reads GSD_REPOS JSON array; falls back to GSD_GITHUB_OWNER + GSD_GITHUB_REPO.
/// </summary>
public static class RepoConfigLoader
{
    /// <summary>
    /// MULTI-01: If GSD_REPOS is set, parse it as a JSON array of repo objects.
    /// Otherwise fall back to GSD_GITHUB_OWNER + GSD_GITHUB_REPO (single-repo backwards compat).
    /// Throws InvalidOperationException when neither source is configured.
    /// </summary>
    public static IReadOnlyList<RepoConfig> Load(IConfiguration config)
    {
        var reposJson = config["GSD_REPOS"];
        if (!string.IsNullOrWhiteSpace(reposJson))
        {
            var dtos = JsonSerializer.Deserialize<List<RepoConfigDto>>(reposJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("GSD_REPOS JSON deserialized to null");
            return dtos.Select(d => new RepoConfig(d.Owner, d.Repo, d.RateLimitDelaySeconds))
                       .ToList()
                       .AsReadOnly();
        }

        var owner = config["GSD_GITHUB_OWNER"];
        var repo = config["GSD_GITHUB_REPO"];
        if (!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo))
            return [new RepoConfig(owner, repo)];

        throw new InvalidOperationException(
            "Multi-repo config missing. Set GSD_REPOS (JSON array) or GSD_GITHUB_OWNER + GSD_GITHUB_REPO.");
    }

    private sealed record RepoConfigDto(string Owner, string Repo, int RateLimitDelaySeconds = 30);
}
