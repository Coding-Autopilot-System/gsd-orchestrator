using GsdOrchestrator.Loop;
using GsdOrchestrator.Scheduling;
using GsdOrchestrator.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class LoopCoordinatorTests
{
    [Fact]
    public async Task RunAsync_CompletesAndPublishesOneTerminalOutcome()
    {
        await using var fixture = await Fixture.CreateAsync([Passed()]);

        var result = await fixture.Coordinator.RunAsync("goal-1", Budget());

        Assert.Equal(GoalStatus.Completed, result.Aggregate.Goal.Status);
        Assert.Equal(1, result.WorkerAttempts);
        Assert.Equal(1, result.VerificationRuns);
        Assert.False(result.RepairCreated);
        Assert.Single(fixture.Learning.Outcomes);
        Assert.NotNull(fixture.Worker.LastRequest);
        Assert.Equal("loop_verifier", fixture.Worker!.LastRequest!.Contract.DownstreamConsumer);
        Assert.Equal("cas.loop.step-result.v1", fixture.Worker.LastRequest.Contract.OutputSchema);
        Assert.Equal(new[] { "research", "architecture", "security", "test" }, fixture.Worker.LastRequest.Contract.FanOut.RequiredRoles);
        Assert.Collection(
            fixture.Worker.LastRequest.Contract.FanOut.Branches,
            branch =>
            {
                Assert.Equal("research", branch.Role);
                Assert.Empty(branch.DependsOnRoles);
                Assert.NotEmpty(branch.ExpectedArtifacts);
            },
            branch =>
            {
                Assert.Equal("architecture", branch.Role);
                Assert.Equal(["research"], branch.DependsOnRoles);
                Assert.NotEmpty(branch.ExpectedArtifacts);
            },
            branch =>
            {
                Assert.Equal("security", branch.Role);
                Assert.Equal(["research", "architecture"], branch.DependsOnRoles);
                Assert.NotEmpty(branch.ExpectedArtifacts);
            },
            branch =>
            {
                Assert.Equal("test", branch.Role);
                Assert.Equal(["architecture"], branch.DependsOnRoles);
                Assert.NotEmpty(branch.ExpectedArtifacts);
            });
        Assert.Equal(["research", "architecture", "security", "test"], fixture.Worker.LastRequest.Contract.FanOut.Aggregation.RequiredRoles);
        Assert.Equal("all_required_terminal", fixture.Worker.LastRequest.Contract.FanOut.Aggregation.RuleSet);
        Assert.Contains(result.Aggregate.Events, item => item.Type == "step.contract.declared");
        Assert.Contains(result.Aggregate.Events, item => item.Type == "verification.decision");
        Assert.Contains(result.Aggregate.Events, item => item.Type == "goal.completed");
    }

    [Fact]
    public async Task RunAsync_CreatesBoundedRepairAndReverifies()
    {
        await using var fixture = await Fixture.CreateAsync([Failed(), Passed()]);

        var result = await fixture.Coordinator.RunAsync("goal-1", Budget());

        Assert.Equal(GoalStatus.Completed, result.Aggregate.Goal.Status);
        Assert.Equal(2, result.WorkerAttempts);
        Assert.Equal(2, result.VerificationRuns);
        Assert.True(result.RepairCreated);
        Assert.Contains(result.Aggregate.Events, item => item.Type == "repair.created");
        Assert.Single(fixture.Learning.Outcomes);
    }

    [Fact]
    public async Task RunAsync_InconclusiveVerification_RecordsRequestEvidenceDecision()
    {
        await using var fixture = await Fixture.CreateAsync([Inconclusive()]);

        var result = await fixture.Coordinator.RunAsync("goal-1", Budget());

        Assert.Equal(GoalStatus.Failed, result.Aggregate.Goal.Status);
        Assert.Contains(result.Aggregate.Events, item => item.Type == "verification.decision" && item.PayloadJson.Contains("request_evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_RejectsWorkerResultThatViolatesDeclaredStepContract()
    {
        await using var fixture = await Fixture.CreateAsync([Passed()], new InvalidWorker());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.RunAsync("goal-1", Budget()));

        Assert.Contains("role set", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_RejectsFanOutPlanThatViolatesDeterministicPolicy()
    {
        await using var fixture = await Fixture.CreateAsync([Passed()], new SuccessfulWorker(), aggregateFactory: Fixture.InvalidFanOutAggregate);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.RunAsync("goal-1", Budget()));

        Assert.Contains("fan-out", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyGuard_DeniesEnvironmentAndHoldsExternalActions()
    {
        Assert.Throws<UnauthorizedAccessException>(() => LoopPolicyGuard.RequireReadablePath("repo/.env"));
        Assert.Equal(ExternalActionDecision.WaitingApproval, LoopPolicyGuard.EvaluateExternalAction("push", false));
        Assert.Equal(ExternalActionDecision.WaitingApproval, LoopPolicyGuard.EvaluateExternalAction("deploy", false));
    }

    [Fact]
    public void PolicyGuard_DeniesMultipleMutationOwnersWithoutMergeStrategy()
    {
        var plan = new LoopFanOutPlan(
            3,
            [
                new("research", [], ["cas://artifact/work-1/research/summary"], true),
                new("architecture", ["research"], ["cas://artifact/work-1/architecture/plan"], true),
                new("security", ["research", "architecture"], ["cas://artifact/work-1/security/review"]),
                new("test", ["architecture"], ["cas://artifact/work-1/test/coverage"])
            ],
            new("all_required_terminal", ["research", "architecture", "security", "test"], true),
            "loop_verifier");

        var error = Assert.Throws<InvalidOperationException>(() => LoopPolicyGuard.ValidateFanOutPlan(new ExecutionLimits(3, 3, 2, 600, 10, 2), plan));

        Assert.Contains("mutation owners", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RepairBudget Budget() => new(true, 0, 2, 0, 2, 0, 10);
    private static VerificationRunResult Passed() => Result(VerificationOutcome.Passed);
    private static VerificationRunResult Failed() => Result(VerificationOutcome.Failed);
    private static VerificationRunResult Inconclusive() => Result(VerificationOutcome.Inconclusive);
    private static VerificationRunResult Result(VerificationOutcome outcome) => new(outcome,
        [new("test", VerificationCategory.Test, true, outcome, $"cas://evidence/{outcome}", outcome == VerificationOutcome.Passed ? 0 : 1, 1)]);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path;
        public LoopCoordinator Coordinator { get; }
        public CapturingLearning Learning { get; }
        public SuccessfulWorker? Worker { get; }

        private Fixture(string path, LoopCoordinator coordinator, CapturingLearning learning, SuccessfulWorker? worker) => (_path, Coordinator, Learning, Worker) = (path, coordinator, learning, worker);

        public static async Task<Fixture> CreateAsync(IReadOnlyList<VerificationRunResult> results, ILoopWorker? worker = null, Func<GoalAggregate>? aggregateFactory = null)
        {
            var path = Path.Combine(Path.GetTempPath(), $"loop-{Guid.NewGuid():N}.db");
            var store = new SqliteGoalStore(path, NullLogger<SqliteGoalStore>.Instance);
            await store.InitializeAsync();
            await store.SaveAsync((aggregateFactory ?? Aggregate)());
            var learning = new CapturingLearning();
            var inspectingWorker = worker is null ? new SuccessfulWorker() : null;
            var effectiveWorker = worker ?? inspectingWorker!;
            return new(path, new(store, effectiveWorker, new ScriptedVerifier(results), learning), learning, inspectingWorker);
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_path)) File.Delete(_path);
            return ValueTask.CompletedTask;
        }

        private static GoalAggregate Aggregate() => new(
            new("goal-1", "corr-1", GoalStatus.Planned, new(3, 3, 2, 600, 10, 2)),
            [new("work-1", "goal-1", "repo", "provider", WorkItemStatus.Ready, 2, "idem-1")],
            [], [], [], [], [], [], [], []);

        public static GoalAggregate InvalidFanOutAggregate() => new(
            new("goal-1", "corr-1", GoalStatus.Planned, new(0, 3, 2, 600, 10, 2)),
            [new("work-1", "goal-1", "repo", "provider", WorkItemStatus.Ready, 2, "idem-1")],
            [], [], [], [], [], [], [], []);
    }

    private sealed class SuccessfulWorker : ILoopWorker
    {
        public LoopWorkRequest? LastRequest { get; private set; }

        public Task<LoopWorkResult> ExecuteAsync(LoopWorkRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new LoopWorkResult(
                true,
                request.Contract.FanOut.RequiredRoles.Select(role => $"cas://evidence/worker/{request.Attempt}/{role}").ToArray(),
                request.IsRepair ? "repair" : "feature",
                request.Contract.ContractId,
                request.Contract.ContextBundleId,
                request.Contract.OutputSchema,
                request.Contract.FanOut.RequiredRoles,
                request.Contract.FanOut.RequiredRoles.Select(role => new LoopBranchResult(role, "succeeded", [$"cas://evidence/worker/{request.Attempt}/{role}"], true)).ToArray(),
                new LoopFanInState(request.Contract.FanOut.AggregatorRole, request.Contract.FanOut.RequiredRoles.Select(role => new LoopBranchResult(role, "succeeded", [$"cas://evidence/worker/{request.Attempt}/{role}"], true)).ToArray(), true, true)));
        }
    }

    private sealed class InvalidWorker : ILoopWorker
    {
        public Task<LoopWorkResult> ExecuteAsync(LoopWorkRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LoopWorkResult(
                true,
                request.Contract.FanOut.RequiredRoles.Select(role => $"cas://evidence/worker/1/{role}").ToArray(),
                "invalid",
                request.Contract.ContractId,
                request.Contract.ContextBundleId,
                request.Contract.OutputSchema,
                ["research"],
                [new LoopBranchResult("research", "succeeded", ["cas://evidence/worker/1/research"], true)],
                new LoopFanInState(request.Contract.FanOut.AggregatorRole, [new LoopBranchResult("research", "succeeded", ["cas://evidence/worker/1/research"], true)], true, true)));
    }

    private sealed class ScriptedVerifier(IReadOnlyList<VerificationRunResult> results) : ILoopVerifier
    {
        private int _index;
        public Task<VerificationRunResult> VerifyAsync(string goalId, LoopWorkResult work, CancellationToken cancellationToken) =>
            Task.FromResult(results[_index++]);
    }

    public sealed class CapturingLearning : ITerminalOutcomePublisher
    {
        public List<TerminalLoopOutcome> Outcomes { get; } = [];
        public Task PublishAsync(TerminalLoopOutcome outcome, CancellationToken cancellationToken)
        {
            Outcomes.Add(outcome);
            return Task.CompletedTask;
        }
    }
}
