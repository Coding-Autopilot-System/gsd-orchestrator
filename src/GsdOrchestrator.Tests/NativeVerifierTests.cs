using GsdOrchestrator.Verification;
using Xunit;

namespace GsdOrchestrator.Tests;

public class NativeVerifierTests
{
    [Fact]
    public async Task Verify_AllMandatoryCategoriesPass_AllowsCompletion()
    {
        var runner = new NativeVerifier(new FakeExecutor(_ => new CommandExecution(0, false, false, 10)));
        var result = await runner.VerifyAsync(Profile(), "run-1", CancellationToken.None);
        Assert.Equal(VerificationOutcome.Passed, result.Outcome);
        CompletionGuard.RequirePassed(result);
        Assert.All(result.Checks, check => Assert.StartsWith("cas://evidence/verification/run-1/", check.EvidenceUri));
    }

    [Fact]
    public async Task Verify_MissingMandatoryCategory_IsInconclusive()
    {
        var profile = new VerificationProfile("incomplete", Profile().Checks.Where(check => check.Category != VerificationCategory.Security).ToArray());
        var result = await new NativeVerifier(new FakeExecutor(_ => new CommandExecution(0, false, false, 1))).VerifyAsync(profile, "run-2", CancellationToken.None);
        Assert.Equal(VerificationOutcome.Inconclusive, result.Outcome);
        Assert.Throws<InvalidOperationException>(() => CompletionGuard.RequirePassed(result));
    }

    [Theory]
    [InlineData(1, false, false, VerificationOutcome.Failed)]
    [InlineData(0, true, false, VerificationOutcome.Inconclusive)]
    [InlineData(0, false, true, VerificationOutcome.Inconclusive)]
    public async Task Verify_FailureTimeoutOrMissingTool_FailsClosed(int exit, bool timeout, bool missing, VerificationOutcome expected)
    {
        var runner = new NativeVerifier(new FakeExecutor(_ => new CommandExecution(exit, timeout, missing, 20)));
        var result = await runner.VerifyAsync(Profile(), "run-3", CancellationToken.None);
        Assert.Equal(expected, result.Outcome);
        Assert.Throws<InvalidOperationException>(() => CompletionGuard.RequirePassed(result));
    }

    [Fact]
    public void RepairPolicy_CreatesRepairOnlyForFailedMandatoryCheckWithinEveryBudget()
    {
        var failed = new VerificationRunResult(VerificationOutcome.Failed,
            [new VerificationCheckResult("tests", VerificationCategory.Test, true, VerificationOutcome.Failed, "cas://evidence/fail", 1, 5)]);
        Assert.NotNull(RepairPolicy.CreateRepair(failed, new RepairBudget(true, 1, 3, 1, 3, 1, 20), "work-1"));
        Assert.Null(RepairPolicy.CreateRepair(failed, new RepairBudget(false, 1, 3, 1, 3, 1, 20), "work-1"));
        Assert.Null(RepairPolicy.CreateRepair(failed, new RepairBudget(true, 3, 3, 1, 3, 1, 20), "work-1"));
        var inconclusive = failed with { Outcome = VerificationOutcome.Inconclusive };
        Assert.Null(RepairPolicy.CreateRepair(inconclusive, new RepairBudget(true, 1, 3, 1, 3, 1, 20), "work-1"));
    }

    private static VerificationProfile Profile() => new("default",
    [
        Check("build", VerificationCategory.Build), Check("test", VerificationCategory.Test),
        Check("lint", VerificationCategory.Lint), Check("security", VerificationCategory.Security),
        Check("evaluation", VerificationCategory.Evaluation)
    ]);

    private static VerificationCheck Check(string name, VerificationCategory category) => new(name, category, ["tool", name], true, TimeSpan.FromSeconds(30));

    private sealed class FakeExecutor(Func<VerificationCheck, CommandExecution> result) : IVerificationCommandExecutor
    {
        public Task<CommandExecution> ExecuteAsync(VerificationCheck check, CancellationToken cancellationToken) => Task.FromResult(result(check));
    }
}
