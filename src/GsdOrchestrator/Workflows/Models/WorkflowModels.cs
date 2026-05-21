using System.Text.Json.Serialization;

namespace GsdOrchestrator.Workflows.Models;

public enum WorkflowState
{
    Idle,
    Analyzing,
    Branching,
    Editing,
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

public sealed record StateTransitionEvent(
    WorkflowState From,
    WorkflowState To,
    DateTimeOffset OccurredAt,
    string? Detail = null);

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
    public WorkflowState CurrentState { get; init; } = WorkflowState.Idle;
    public int RetryCount { get; init; }
    public string? FailureReason { get; init; }
    public List<StateTransitionEvent> History { get; init; } = [];

    public GsdWorkflowContext Transition(WorkflowState to, string? detail = null) =>
        this with
        {
            History = [.. History, new StateTransitionEvent(CurrentState, to, DateTimeOffset.UtcNow, detail)],
            CurrentState = to,
            RetryCount = 0
        };
}
