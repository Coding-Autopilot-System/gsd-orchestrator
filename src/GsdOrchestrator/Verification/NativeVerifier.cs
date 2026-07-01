using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GsdOrchestrator.Verification;

public interface IVerificationCommandExecutor
{
    Task<CommandExecution> ExecuteAsync(VerificationCheck check, CancellationToken cancellationToken);
}

public sealed class NativeProcessCommandExecutor(string workingDirectory) : IVerificationCommandExecutor
{
    public async Task<CommandExecution> ExecuteAsync(VerificationCheck check, CancellationToken cancellationToken)
    {
        if (check.Command.Count == 0) return new(-1, false, true, 0);
        var start = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(check.Timeout);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = check.Command[0], WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        foreach (var argument in check.Command.Skip(1)) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error);
            return new(process.ExitCode, false, false, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        catch (Win32Exception)
        {
            return new(-1, false, true, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            return new(-1, true, false, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }
}

public sealed class NativeVerifier(IVerificationCommandExecutor executor)
{
    private static readonly VerificationCategory[] RequiredCategories = Enum.GetValues<VerificationCategory>();
    private static readonly Regex Unsafe = new("[^a-zA-Z0-9_-]", RegexOptions.Compiled);

    public async Task<VerificationRunResult> VerifyAsync(VerificationProfile profile, string runId, CancellationToken cancellationToken)
    {
        var results = new List<VerificationCheckResult>();
        foreach (var category in RequiredCategories.Where(category => profile.Checks.All(check => check.Category != category || !check.Mandatory)))
            results.Add(new($"missing-{category.ToString().ToLowerInvariant()}", category, true, VerificationOutcome.Inconclusive, Evidence(runId, $"missing-{category}"), null, 0));

        foreach (var check in profile.Checks)
        {
            var execution = await executor.ExecuteAsync(check, cancellationToken);
            var outcome = execution.ToolMissing || execution.TimedOut
                ? VerificationOutcome.Inconclusive
                : execution.ExitCode == 0 ? VerificationOutcome.Passed : VerificationOutcome.Failed;
            results.Add(new(check.Name, check.Category, check.Mandatory, outcome, Evidence(runId, check.Name), execution.ToolMissing ? null : execution.ExitCode, execution.DurationMilliseconds));
        }

        var mandatory = results.Where(check => check.Mandatory).ToList();
        var overall = mandatory.Any(check => check.Outcome == VerificationOutcome.Failed)
            ? VerificationOutcome.Failed
            : mandatory.Any(check => check.Outcome == VerificationOutcome.Inconclusive)
                ? VerificationOutcome.Inconclusive
                : VerificationOutcome.Passed;
        return new(overall, results);
    }

    private static string Evidence(string runId, string name) => $"cas://evidence/verification/{Unsafe.Replace(runId, "_")}/{Unsafe.Replace(name, "_")}";
}

public static class CompletionGuard
{
    public static void RequirePassed(VerificationRunResult result)
    {
        if (result.Outcome != VerificationOutcome.Passed || result.Checks.Any(check => check.Mandatory && check.Outcome != VerificationOutcome.Passed))
            throw new InvalidOperationException($"Goal cannot complete with verification outcome {result.Outcome}.");
    }
}

public static class RepairPolicy
{
    public static RepairRequest? CreateRepair(VerificationRunResult result, RepairBudget budget, string sourceWorkItemId)
    {
        if (result.Outcome != VerificationOutcome.Failed || !budget.PolicyAllowsRepair ||
            budget.ConsumedAttempts >= budget.MaxAttempts || budget.ConsumedIterations >= budget.MaxIterations ||
            budget.ConsumedModelCalls >= budget.MaxModelCalls)
            return null;
        var evidence = result.Checks.Where(check => check.Mandatory && check.Outcome == VerificationOutcome.Failed).Select(check => check.EvidenceUri).ToArray();
        return evidence.Length == 0 ? null : new(sourceWorkItemId, budget.ConsumedAttempts + 1, evidence, "Mandatory native verification failed within repair policy and budget.");
    }
}
