using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace GsdOrchestrator.Workflows.Models;

public enum WorkflowState
{
    Idle,
    Triaging,      // Phase 13: issue classification before analysis
    Analyzing,
    Branching,
    Editing,
    TestGenerating,   // Phase 14: generate xUnit tests for edited source files
    Validating,
    Committing,
    PrCreating,
    Reviewing,
    Documenting,
    Done,
    Failed
}

public enum ValidationStatus { Pass, Warn, Block }

// ── Per-state output models ──────────────────────────────────────────────────

public sealed record IssueContext(
    int Number,
    string Title,
    string Body,
    IReadOnlyList<string> Labels,
    string RepoOwner,
    string RepoName,
    string DefaultBranch);

public sealed record AnalysisPlan(
    string BranchName,
    IReadOnlyList<PlannedFile> FilesToModify,
    string Summary,
    bool RequiresTests)
{
    public string IssueSummary { get; init; } = Summary;
}

public sealed record PlannedFile(string Path, string Rationale);

public sealed record BranchContext(
    string BranchName,
    string BaseSha,
    bool WasResumed = false);

public sealed record FileEdit(
    string Path,
    string OldSha,
    string NewSha,
    string CommitMessage);

public sealed record EditContext(IReadOnlyList<FileEdit> Edits);

public sealed record GeneratedTest(
    string SourcePath,
    string TestPath,
    string TestSha,
    bool WasSkipped,
    string? SkipReason);

public sealed record TestGenerationContext(IReadOnlyList<GeneratedTest> GeneratedTests);

public sealed record GateResult(string Gate, ValidationStatus Status, string? Detail = null);

public sealed record ValidationResult(
    ValidationStatus Status,
    IReadOnlyList<GateResult> Gates,
    string? ConflictDetail = null);

public sealed record CommitContext(string FinalCommitSha, string CommitUrl);

public sealed record PullRequestContext(
    int PrNumber,
    string PrUrl,
    string Title,
    string Body);

// ── Phase 15: PR review loop models ─────────────────────────────────────────

public sealed record ReviewComment(
    string Path,
    int Line,
    string Side,
    string Severity,
    string Body);

public sealed record ReviewResult(
    string Verdict,
    string Summary,
    IReadOnlyList<ReviewComment> Comments);

public sealed record PrReviewContext(
    int PrNumber,
    string Owner,
    string Repo,
    string Diff);

public sealed record TriageResult(
    string Classification,
    string Reason,
    int? DuplicateNumber);

public sealed record StateTransitionEvent(
    WorkflowState From,
    WorkflowState To,
    DateTimeOffset OccurredAt,
    string? Detail = null);

// ── Phase 16: Multi-repo configuration ──────────────────────────────────────

/// <summary>Identifies a single repo to watch/process and its per-repo rate limit delay.</summary>
public sealed record RepoConfig(
    string Owner,
    string Repo,
    int RateLimitDelaySeconds = 30);

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

// ── Root context ─────────────────────────────────────────────────────────────

public sealed record GsdWorkflowContext
{
    public string WorkflowId { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public IssueContext? Issue { get; init; }
    public AnalysisPlan? Plan { get; init; }
    public BranchContext? Branch { get; init; }
    public EditContext? Edits { get; init; }
    public ValidationResult? Validation { get; init; }
    public CommitContext? Commit { get; init; }
    public PullRequestContext? PullRequest { get; init; }
    public TriageResult? Triage { get; init; }
    public TestGenerationContext? TestGeneration { get; init; } // Phase 14
    public ReviewResult? Review { get; init; }        // Phase 15
    public PrReviewContext? PrReview { get; init; }   // Phase 15: --pr mode input
    public bool TriageModeOnly { get; init; } = false;
    public WorkflowState CurrentState { get; init; } = WorkflowState.Idle;
    public int RetryCount { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<StateTransitionEvent> History { get; init; } = [];

    public GsdWorkflowContext Transition(WorkflowState to, string? detail = null) =>
        this with
        {
            History = [.. History, new StateTransitionEvent(CurrentState, to, DateTimeOffset.UtcNow, detail)],
            CurrentState = to,
            RetryCount = 0
        };
}
