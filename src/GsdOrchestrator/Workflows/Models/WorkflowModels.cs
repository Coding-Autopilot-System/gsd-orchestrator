using System.Text.Json;
using System.Text.Json.Serialization;

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

public enum SdlcBatch
{
    Discovery,
    Design,
    Change,
    Assurance,
    Closure
}

public enum SdlcPhaseStatus
{
    Pending,
    Ready,
    Running,
    Passed,
    Failed,
    Invalidated,
    RolledBack,
    Waiting,
    Terminal
}

public enum ValidationStatus { Pass, Warn, Block }

public enum TerminalStopReason
{
    None,
    Ambiguity,
    DestructiveAction,
    ExternalAction,
    PolicyDenied,
    BudgetExhausted,
    NoProgress,
    RuntimeExceeded,
    Unknown
}

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

public sealed record SdlcVerificationOutcome(
    string PhaseId,
    string Verifier,
    bool Passed,
    IReadOnlyList<string> InvalidatedPhaseIds,
    string? Reason = null);

public sealed record StateTransitionEvent(
    WorkflowState From,
    WorkflowState To,
    DateTimeOffset OccurredAt,
    string? Detail = null);

public static class SdlcWorkflowMap
{
    public static string PhaseIdForState(WorkflowState state) => state switch
    {
        WorkflowState.Idle => "understand",
        WorkflowState.Triaging => "understand",
        WorkflowState.Analyzing => "research",
        WorkflowState.Branching => "analyze",
        WorkflowState.Editing => "implement",
        WorkflowState.TestGenerating => "verify",
        WorkflowState.Validating => "verify",
        WorkflowState.Committing => "document",
        WorkflowState.PrCreating => "review",
        WorkflowState.Reviewing => "review",
        WorkflowState.Documenting => "update-memory",
        WorkflowState.Done => "finished",
        WorkflowState.Failed => "finished",
        _ => "understand"
    };
}

// ── Phase 16: Multi-repo configuration ──────────────────────────────────────

/// <summary>Identifies a single repo to watch/process and its per-repo rate limit delay.</summary>
public sealed record RepoConfig(
    string Owner,
    string Repo,
    int RateLimitDelaySeconds = 30);

public sealed record SdlcPhaseDefinition(
    string Id,
    string Name,
    SdlcBatch Batch,
    IReadOnlyList<string> Dependencies,
    string Verifier,
    IReadOnlyList<string> RequiredArtifacts,
    IReadOnlyList<string> SuccessCriteria,
    string? RollbackTo = null,
    int MaxAttempts = 1);

public sealed record SdlcProfile(
    string ProfileId,
    string ProfileVersion,
    int GoalBudget,
    IReadOnlyList<SdlcPhaseDefinition> Phases,
    int GoalAttempts,
    int Iterations,
    int RuntimeMinutes,
    int ModelCalls,
    int NoProgressLimit)
{
    public static SdlcProfile CasSdlcV1 { get; } = new(
        "cas-sdlc-v1",
        "v1.1",
        GoalBudget: 17,
        Phases:
        [
            new("understand", "Understand", SdlcBatch.Discovery, [], "repo-verifier", ["brief"], ["goal clarified"], null, 1),
            new("research", "Research", SdlcBatch.Discovery, ["understand"], "repo-verifier", ["sources"], ["evidence collected"], "understand", 1),
            new("analyze", "Analyze", SdlcBatch.Discovery, ["research"], "repo-verifier", ["analysis"], ["risks identified"], "research", 1),
            new("plan", "Plan", SdlcBatch.Design, ["analyze"], "repo-verifier", ["plan"], ["plan approved"], "analyze", 1),
            new("risk-assessment", "Risk Assessment", SdlcBatch.Design, ["plan"], "repo-verifier", ["risk"], ["risk documented"], "plan", 1),
            new("implement", "Implement", SdlcBatch.Change, ["plan", "risk-assessment"], "mutation-owner", ["patch"], ["change applied"], "plan", 2),
            new("verify", "Verify", SdlcBatch.Assurance, ["implement"], "verifier", ["results"], ["tests pass"], "implement", 1),
            new("review", "Review", SdlcBatch.Assurance, ["verify"], "verifier", ["review"], ["review pass"], "verify", 1),
            new("improve", "Improve", SdlcBatch.Assurance, ["review"], "verifier", ["repair"], ["repair bounded"], "implement", 2),
            new("document", "Document", SdlcBatch.Closure, ["improve"], "verifier", ["docs"], ["docs updated"], "improve", 1),
            new("update-memory", "Update Memory", SdlcBatch.Closure, ["document"], "verifier", ["memory"], ["candidate emitted"], "document", 1),
            new("finished", "Finished", SdlcBatch.Closure, ["update-memory"], "verifier", ["terminal"], ["terminal published"], "update-memory", 1)
        ],
        GoalAttempts: 12,
        Iterations: 12,
        RuntimeMinutes: 240,
        ModelCalls: 64,
        NoProgressLimit: 3);

    public SdlcPhaseDefinition GetPhase(string phaseId) =>
        Phases.First(phase => phase.Id == phaseId);

    public SdlcPhaseDefinition? TryGetPhase(string phaseId) =>
        Phases.FirstOrDefault(phase => phase.Id == phaseId);

    public string? NextPhaseId(string phaseId)
    {
        var currentIndex = IndexOfPhase(phaseId);
        return currentIndex < 0 || currentIndex + 1 >= Phases.Count
            ? null
            : Phases[currentIndex + 1].Id;
    }

    public string ResolveRollbackOrigin(string phaseId, IReadOnlyList<string> invalidatedPhaseIds)
    {
        var candidate = invalidatedPhaseIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        var phase = TryGetPhase(phaseId);
        if (phase?.RollbackTo is not null)
        {
            return phase.RollbackTo;
        }

        return phaseId;
    }

    private int IndexOfPhase(string phaseId) =>
        Phases.ToList().FindIndex(phase => phase.Id == phaseId);
}


// ── Root context ─────────────────────────────────────────────────────────────

public sealed record GsdWorkflowContext
{
    public string SchemaVersion { get; init; } = "1.1";
    public SdlcProfile SdlcProfile { get; init; } = SdlcProfile.CasSdlcV1;
    public string WorkflowId { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public SdlcRunRecord? SdlcRun { get; init; }
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
    public WorkflowState? FailedState { get; init; }
    public string? FailureReason { get; init; }
    public TerminalStopReason StopReason { get; init; } = TerminalStopReason.None;
    public IReadOnlyList<StateTransitionEvent> History { get; init; } = [];
    public SdlcRollbackDirective? PendingRollback { get; init; }

    public GsdWorkflowContext Transition(WorkflowState to, string? detail = null) =>
        this with
        {
            History = [.. History, new StateTransitionEvent(CurrentState, to, DateTimeOffset.UtcNow, detail)],
            CurrentState = to,
            RetryCount = to == WorkflowState.Failed ? RetryCount : 0,
            FailedState = to == WorkflowState.Failed ? FailedState ?? CurrentState : null,
            FailureReason = to == WorkflowState.Failed ? FailureReason : null,
            PendingRollback = null
        };

    public GsdWorkflowContext WithSdlcRun(string goal) =>
        this with { SdlcRun = SdlcRunRecord.Create(WorkflowId, goal, SdlcProfile) };

    public GsdWorkflowContext WithSdlcPhaseExecution(SdlcPhaseExecutionRecord record) =>
        this with
        {
            SdlcRun = SdlcRun is null
                ? null
                : SdlcRun.RecordPhaseExecution(record)
        };

    public GsdWorkflowContext WithSdlcVerification(SdlcVerificationOutcome outcome) =>
        this with
        {
            PendingRollback = SdlcRun is null || outcome.Passed
                ? null
                : new SdlcRollbackDirective(
                    outcome.PhaseId,
                    SdlcRun.Profile.ResolveRollbackOrigin(outcome.PhaseId, outcome.InvalidatedPhaseIds),
                    outcome.Reason ?? "verification failed"),
            SdlcRun = SdlcRun is null
                ? null
                : SdlcRun with
                {
                    CurrentPhaseId = outcome.PhaseId,
                    CurrentPhaseStatus = outcome.Passed ? SdlcPhaseStatus.Passed : SdlcPhaseStatus.Failed,
                    CurrentVerifier = outcome.Verifier,
                    RollbackOrigin = outcome.Passed ? SdlcRun.RollbackOrigin : SdlcRun.Profile.ResolveRollbackOrigin(outcome.PhaseId, outcome.InvalidatedPhaseIds),
                    RollbackReason = outcome.Passed ? SdlcRun.RollbackReason : outcome.Reason,
                    NoProgressCount = outcome.Passed ? 0 : SdlcRun.NoProgressCount + 1
                }
        };

    public GsdWorkflowContext WithSdlcVerificationRecord(SdlcVerificationRecord record) =>
        this with
        {
            SdlcRun = SdlcRun is null
                ? null
                : SdlcRun.RecordVerification(record)
        };
}
