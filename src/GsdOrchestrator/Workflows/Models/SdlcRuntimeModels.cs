using System.Security.Cryptography;
using System.Text;

namespace GsdOrchestrator.Workflows.Models;

public sealed record SdlcRunRecord(
    string RunId,
    string Goal,
    SdlcProfile Profile,
    string CurrentPhaseId,
    SdlcPhaseStatus CurrentPhaseStatus,
    string CurrentVerifier,
    string? RollbackOrigin = null,
    string? RollbackReason = null,
    int AttemptCount = 0,
    int IterationCount = 0,
    int ModelCallCount = 0,
    int NoProgressCount = 0,
    DateTimeOffset StartedAt = default,
    IReadOnlyList<string>? MemoryCandidates = null,
    IReadOnlyList<SdlcPhaseExecutionRecord>? PhaseExecutions = null,
    IReadOnlyList<SdlcVerificationRecord>? Verifications = null,
    IReadOnlyList<SdlcMemoryCandidateRecord>? MemoryCandidateRecords = null,
    IReadOnlyList<string>? InvalidatedPhaseIds = null,
    string? TerminalOutcome = null)
{
    public static SdlcRunRecord Create(string runId, string goal, SdlcProfile profile) =>
        new(runId, goal, profile, "understand", SdlcPhaseStatus.Pending, profile.Phases[0].Verifier, StartedAt: DateTimeOffset.UtcNow);

    public SdlcRunRecord AdvanceAttempt() => this with { AttemptCount = AttemptCount + 1 };

    public SdlcRunRecord AdvanceIteration() => this with { IterationCount = IterationCount + 1 };

    public SdlcRunRecord AdvanceModelCall() => this with { ModelCallCount = ModelCallCount + 1 };

    public SdlcRunRecord RecordPhaseExecution(SdlcPhaseExecutionRecord record) =>
        this with { PhaseExecutions = [.. (PhaseExecutions ?? []), record] };

    public SdlcRunRecord RecordVerification(SdlcVerificationRecord record) =>
        this with
        {
            Verifications = [.. (Verifications ?? []), record],
            InvalidatedPhaseIds = record.InvalidatedPhaseIds.Count == 0
                ? InvalidatedPhaseIds
                : [.. (InvalidatedPhaseIds ?? []), .. record.InvalidatedPhaseIds]
        };

    public SdlcRunRecord RecordMemoryCandidate(SdlcMemoryCandidateRecord record) =>
        this with { MemoryCandidateRecords = [.. (MemoryCandidateRecords ?? []), record] };

    public SdlcRunRecord RecordTerminalOutcome(string outcome) =>
        string.IsNullOrWhiteSpace(TerminalOutcome)
            ? this with { TerminalOutcome = outcome }
            : this;
}

public sealed record SdlcPhaseExecutionRecord(
    string PhaseId,
    SdlcPhaseStatus Status,
    string Verifier,
    string PromptDigest,
    string Prompt,
    string? RollbackOrigin = null,
    string? RollbackReason = null,
    IReadOnlyList<string>? Artifacts = null,
    string? ResultSummary = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null);

public sealed record SdlcVerificationRecord(
    string PhaseId,
    string Verifier,
    bool Passed,
    IReadOnlyList<string> InvalidatedPhaseIds,
    string? Reason = null,
    DateTimeOffset? CompletedAt = null);

public sealed record SdlcRollbackDirective(
    string FromPhaseId,
    string ToPhaseId,
    string Reason);

public sealed record SdlcMemoryCandidateRecord(
    string PhaseId,
    string Version,
    string Summary,
    IReadOnlyList<string> Evidence,
    DateTimeOffset CreatedAt);

public static class SdlcPromptCompiler
{
    public static string BuildPhasePrompt(
        string goal,
        SdlcProfile profile,
        SdlcPhaseDefinition phase,
        IReadOnlyList<string> validatedInputs,
        IReadOnlyList<string> priorEvidence,
        IReadOnlyList<string> memoryCandidateFields)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Goal: {goal}");
        builder.AppendLine($"Profile: {profile.ProfileId} ({profile.ProfileVersion})");
        builder.AppendLine($"Phase: {phase.Id} / {phase.Name}");
        builder.AppendLine($"Batch: {phase.Batch}");
        builder.AppendLine($"Verifier: {phase.Verifier}");
        builder.AppendLine($"Success criteria: {string.Join(" | ", phase.SuccessCriteria)}");
        builder.AppendLine($"Required artifacts: {string.Join(" | ", phase.RequiredArtifacts)}");
        builder.AppendLine($"Dependencies: {string.Join(" | ", phase.Dependencies)}");
        builder.AppendLine($"Rollback to: {phase.RollbackTo ?? "none"}");
        builder.AppendLine($"Validated inputs: {string.Join(" | ", validatedInputs)}");
        builder.AppendLine($"Prior evidence: {string.Join(" | ", priorEvidence)}");
        builder.AppendLine($"Memory candidate fields: {string.Join(" | ", memoryCandidateFields)}");
        builder.AppendLine("Failure behavior: pause only for unresolved ambiguity, destructive or externally visible actions, policy denial requiring human authority, or exhausted budgets.");
        builder.AppendLine("Rollback behavior: verifier may invalidate the earliest affected dependency and resume from there.");
        return builder.ToString();
    }

    public static string Digest(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

public static class SdlcTransitionPlanner
{
    public static SdlcPhaseDefinition CurrentPhase(SdlcProfile profile, string phaseId) =>
        profile.GetPhase(phaseId);

    public static string? NextPhaseId(SdlcProfile profile, string phaseId) =>
        profile.NextPhaseId(phaseId);

    public static string RollbackOrigin(SdlcProfile profile, string phaseId, IReadOnlyList<string> invalidatedPhaseIds) =>
        profile.ResolveRollbackOrigin(phaseId, invalidatedPhaseIds);
}
