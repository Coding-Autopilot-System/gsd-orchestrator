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
    public async Task RunAsync_Converts_Worker_Exception_Into_Typed_Failure_State()
    {
        await using var fixture = await Fixture.CreateAsync([Passed()], worker: new ThrowingWorker(new TimeoutException("worker timeout")));

        var result = await fixture.Coordinator.RunAsync("goal-1", Budget());

        Assert.Equal(GoalStatus.Failed, result.Aggregate.Goal.Status);
        Assert.Contains(result.Aggregate.Evidence, item => item.Kind == "failure-state");
        var failureEvent = Assert.Single(result.Aggregate.Events, item => item.Type == "goal.failed");
        Assert.Contains("Transient", failureEvent.PayloadJson);
        Assert.Contains("worker timeout", failureEvent.PayloadJson);
        Assert.Single(fixture.Learning.Outcomes);
        Assert.Contains("worker timeout", fixture.Learning.Outcomes[0].Summary);
        Assert.Equal(GoalStatus.Failed, fixture.Learning.Outcomes[0].Status);
    }

    [Fact]
    public void PolicyGuard_DeniesEnvironmentAndHoldsExternalActions()
    {
        Assert.Throws<UnauthorizedAccessException>(() => LoopPolicyGuard.RequireReadablePath("repo/.env"));
        Assert.Equal(ExternalActionDecision.WaitingApproval, LoopPolicyGuard.EvaluateExternalAction("push", false));
        Assert.Equal(ExternalActionDecision.WaitingApproval, LoopPolicyGuard.EvaluateExternalAction("deploy", false));
    }

    private static RepairBudget Budget() => new(true, 0, 2, 0, 2, 0, 10);
    private static VerificationRunResult Passed() => Result(VerificationOutcome.Passed);
    private static VerificationRunResult Failed() => Result(VerificationOutcome.Failed);
    private static VerificationRunResult Result(VerificationOutcome outcome) => new(outcome,
        [new("test", VerificationCategory.Test, true, outcome, $"cas://evidence/{outcome}", outcome == VerificationOutcome.Passed ? 0 : 1, 1)]);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path;
        public LoopCoordinator Coordinator { get; }
        public CapturingLearning Learning { get; }

        private Fixture(string path, LoopCoordinator coordinator, CapturingLearning learning) => (_path, Coordinator, Learning) = (path, coordinator, learning);

        public static async Task<Fixture> CreateAsync(IReadOnlyList<VerificationRunResult> results, ILoopWorker? worker = null)
        {
            var path = Path.Combine(Path.GetTempPath(), $"loop-{Guid.NewGuid():N}.db");
            var store = new SqliteGoalStore(path, NullLogger<SqliteGoalStore>.Instance);
            await store.InitializeAsync();
            await store.SaveAsync(Aggregate());
            var learning = new CapturingLearning();
            return new(path, new(store, worker ?? new SuccessfulWorker(), new ScriptedVerifier(results), learning, NullLogger<LoopCoordinator>.Instance), learning);
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
    }

    private sealed class SuccessfulWorker : ILoopWorker
    {
        public Task<LoopWorkResult> ExecuteAsync(LoopWorkRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LoopWorkResult(true, [$"cas://evidence/worker/{request.Attempt}"], request.IsRepair ? "repair" : "feature"));
    }

    private sealed class ThrowingWorker(Exception exception) : ILoopWorker
    {
        public Task<LoopWorkResult> ExecuteAsync(LoopWorkRequest request, CancellationToken cancellationToken) =>
            Task.FromException<LoopWorkResult>(exception);
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
