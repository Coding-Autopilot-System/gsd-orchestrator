namespace GsdOrchestrator.Auth;

public sealed class GitHubPatProvider
{
    public string Token { get; }

    public GitHubPatProvider(IConfiguration config)
    {
        var token = config["GITHUB_PERSONAL_ACCESS_TOKEN"];

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "GITHUB_PERSONAL_ACCESS_TOKEN is not set. Copy .env.example to .env and add your GitHub PAT.");

        // Sanity-check the token format without logging the value
        if (!token.StartsWith("ghp_", StringComparison.Ordinal) &&
            !token.StartsWith("ghs_", StringComparison.Ordinal) &&
            !token.StartsWith("github_pat_", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "GITHUB_PERSONAL_ACCESS_TOKEN does not look like a valid GitHub token (expected ghp_, ghs_, or github_pat_ prefix).");

        Token = token;
    }
}
