namespace GsdOrchestrator.Verification;

public enum VerificationCategory { Build, Test, Lint, Security, Evaluation }
public enum VerificationOutcome { Passed, Failed, Inconclusive }

public sealed record VerificationCheck(string Name, VerificationCategory Category, IReadOnlyList<string> Command, bool Mandatory, TimeSpan Timeout);
public sealed record VerificationProfile(string Name, IReadOnlyList<VerificationCheck> Checks);
public sealed record CommandExecution(int ExitCode, bool TimedOut, bool ToolMissing, long DurationMilliseconds);
public sealed record VerificationCheckResult(string Name, VerificationCategory Category, bool Mandatory, VerificationOutcome Outcome, string EvidenceUri, int? ExitCode, long DurationMilliseconds);
public sealed record VerificationRunResult(VerificationOutcome Outcome, IReadOnlyList<VerificationCheckResult> Checks);
public sealed record RepairBudget(bool PolicyAllowsRepair, int ConsumedAttempts, int MaxAttempts, int ConsumedIterations, int MaxIterations, int ConsumedModelCalls, int MaxModelCalls);
public sealed record RepairRequest(string SourceWorkItemId, int AttemptNumber, IReadOnlyList<string> EvidenceUris, string Reason);
