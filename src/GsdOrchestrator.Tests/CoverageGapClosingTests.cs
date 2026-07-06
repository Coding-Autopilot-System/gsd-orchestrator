using System.Text.Json.Nodes;
using GsdOrchestrator.Loop;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Scheduling;
using GsdOrchestrator.Verification;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

/// <summary>
/// Phase 26-01: targeted tests for previously-uncovered branches identified from the
/// Task-1 cobertura baseline (branch-rate 0.6913). Each test asserts observable
/// behavior (return value, thrown exception type, or published side-effect) per the
/// plan's threat model (T-26-01: no assertion-free coverage padding).
/// </summary>
public class CoverageGapClosingTests
{
    // ---- SdlcWorkflowMap.PhaseIdForState: 0.1428 branch-rate (2/14) before this test ----
    [Theory]
    [InlineData(WorkflowState.Idle, "understand")]
    [InlineData(WorkflowState.Triaging, "understand")]
    [InlineData(WorkflowState.Analyzing, "research")]
    [InlineData(WorkflowState.Branching, "analyze")]
    [InlineData(WorkflowState.Editing, "implement")]
    [InlineData(WorkflowState.TestGenerating, "verify")]
    [InlineData(WorkflowState.Validating, "verify")]
    [InlineData(WorkflowState.Committing, "document")]
    [InlineData(WorkflowState.PrCreating, "review")]
    [InlineData(WorkflowState.Reviewing, "review")]
    [InlineData(WorkflowState.Documenting, "update-memory")]
    [InlineData(WorkflowState.Done, "finished")]
    [InlineData(WorkflowState.Failed, "finished")]
    public void PhaseIdForState_MapsEveryWorkflowStateToExpectedSdlcPhase(WorkflowState state, string expectedPhaseId)
    {
        Assert.Equal(expectedPhaseId, SdlcWorkflowMap.PhaseIdForState(state));
    }

    [Fact]
    public void PhaseIdForState_UnknownEnumValue_FallsBackToUnderstand()
    {
        // Cast an out-of-range int to exercise the switch expression's default arm.
        var unknown = (WorkflowState)999;
        Assert.Equal("understand", SdlcWorkflowMap.PhaseIdForState(unknown));
    }

    // ---- SdlcProfile.ResolveRollbackOrigin: only the "invalidated phase provided" branch was covered ----
    [Fact]
    public void ResolveRollbackOrigin_NoInvalidatedPhases_FallsBackToPhaseRollbackTo()
    {
        var profile = SdlcProfile.CasSdlcV1;

        var origin = profile.ResolveRollbackOrigin("verify", []);

        Assert.Equal("implement", origin);
    }

    [Fact]
    public void ResolveRollbackOrigin_UnknownPhaseAndNoInvalidated_ReturnsPhaseIdItself()
    {
        var profile = SdlcProfile.CasSdlcV1;

        var origin = profile.ResolveRollbackOrigin("no-such-phase", []);

        Assert.Equal("no-such-phase", origin);
    }

    [Fact]
    public void GetPhase_KnownPhaseId_ReturnsDefinition()
    {
        var profile = SdlcProfile.CasSdlcV1;

        var phase = profile.GetPhase("implement");

        Assert.Equal("Implement", phase.Name);
        Assert.Equal(SdlcBatch.Change, phase.Batch);
    }

    [Fact]
    public void TryGetPhase_UnknownPhaseId_ReturnsNull()
    {
        var profile = SdlcProfile.CasSdlcV1;

        Assert.Null(profile.TryGetPhase("no-such-phase"));
    }

    // ---- GsdWorkflowContext.Transition: Failed-state branch was uncovered ----
    [Fact]
    public void Transition_ToFailed_RecordsFailedStateAndFailureReason()
    {
        var ctx = new GsdWorkflowContext { FailureReason = "boom" };

        var next = ctx.Transition(WorkflowState.Failed, "verification failed");

        Assert.Equal(WorkflowState.Failed, next.CurrentState);
        Assert.Equal(WorkflowState.Idle, next.FailedState);
        Assert.Equal("boom", next.FailureReason);
        Assert.Single(next.History);
        Assert.Equal(WorkflowState.Failed, next.History[0].To);
    }

    [Fact]
    public void Transition_ToFailed_PreservesExistingFailedStateAcrossRepeatedFailures()
    {
        var ctx = new GsdWorkflowContext().Transition(WorkflowState.Failed, "first failure");

        var again = ctx.Transition(WorkflowState.Failed, "second failure");

        // FailedState should stick to the first-recorded failure origin (Idle), not be overwritten.
        Assert.Equal(WorkflowState.Idle, again.FailedState);
    }

    [Fact]
    public void Transition_ToNonFailedState_ClearsFailureMetadataAndResetsRetryCount()
    {
        var ctx = new GsdWorkflowContext { RetryCount = 3, FailedState = WorkflowState.Editing, FailureReason = "boom" };

        var next = ctx.Transition(WorkflowState.Branching);

        Assert.Equal(0, next.RetryCount);
        Assert.Null(next.FailedState);
        Assert.Null(next.FailureReason);
    }

    // ---- GsdWorkflowContext.WithSdlcVerification: only the "failed" branch was covered ----
    [Fact]
    public void WithSdlcVerification_Passed_ClearsPendingRollbackAndResetsNoProgressCount()
    {
        var ctx = new GsdWorkflowContext().WithSdlcRun("Improve the loop");

        var next = ctx.WithSdlcVerification(new SdlcVerificationOutcome(
            "research", "repo-verifier", Passed: true, InvalidatedPhaseIds: [], Reason: null));

        Assert.Null(next.PendingRollback);
        Assert.Equal(SdlcPhaseStatus.Passed, next.SdlcRun!.CurrentPhaseStatus);
        Assert.Equal(0, next.SdlcRun.NoProgressCount);
    }

    [Fact]
    public void WithSdlcVerification_NoSdlcRun_ReturnsContextUnchanged()
    {
        var ctx = new GsdWorkflowContext();

        var next = ctx.WithSdlcVerification(new SdlcVerificationOutcome(
            "research", "repo-verifier", Passed: false, InvalidatedPhaseIds: [], Reason: "n/a"));

        Assert.Null(next.SdlcRun);
        Assert.Null(next.PendingRollback);
    }

    [Fact]
    public void WithSdlcPhaseExecution_NoSdlcRun_ReturnsContextUnchanged()
    {
        var ctx = new GsdWorkflowContext();

        var next = ctx.WithSdlcPhaseExecution(new SdlcPhaseExecutionRecord(
            "understand", SdlcPhaseStatus.Passed, "repo-verifier", "digest-1", "prompt text"));

        Assert.Null(next.SdlcRun);
    }

    // ---- LoopPolicyGuard: happy-path and unknown-action branches were uncovered ----
    [Fact]
    public void RequireReadablePath_NoEnvSegment_DoesNotThrow()
    {
        var exception = Record.Exception(() => LoopPolicyGuard.RequireReadablePath("repo/src/Program.cs"));
        Assert.Null(exception);
    }

    [Fact]
    public void EvaluateExternalAction_ApprovedKnownAction_ReturnsAuthorized()
    {
        Assert.Equal(ExternalActionDecision.Authorized, LoopPolicyGuard.EvaluateExternalAction("push", approved: true));
    }

    [Fact]
    public void EvaluateExternalAction_UnknownAction_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LoopPolicyGuard.EvaluateExternalAction("unknown_action", approved: true));
    }

    // ---- NativeProcessCommandExecutor: 0% branch-rate before this test (only exercised via FakeExecutor elsewhere) ----
    [Fact]
    public async Task NativeProcessCommandExecutor_EmptyCommand_ReturnsToolMissingWithoutSpawningProcess()
    {
        var executor = new NativeProcessCommandExecutor(Environment.CurrentDirectory);
        var check = new VerificationCheck("empty", VerificationCategory.Build, [], true, TimeSpan.FromSeconds(5));

        var execution = await executor.ExecuteAsync(check, CancellationToken.None);

        Assert.Equal(-1, execution.ExitCode);
        Assert.True(execution.ToolMissing);
        Assert.False(execution.TimedOut);
    }

    [Fact]
    public async Task NativeProcessCommandExecutor_SuccessfulCommand_ReturnsZeroExitCode()
    {
        var executor = new NativeProcessCommandExecutor(Environment.CurrentDirectory);
        var command = OperatingSystem.IsWindows()
            ? new List<string> { "cmd.exe", "/c", "exit 0" }
            : new List<string> { "/bin/sh", "-c", "exit 0" };
        var check = new VerificationCheck("success", VerificationCategory.Build, command, true, TimeSpan.FromSeconds(15));

        var execution = await executor.ExecuteAsync(check, CancellationToken.None);

        Assert.Equal(0, execution.ExitCode);
        Assert.False(execution.TimedOut);
        Assert.False(execution.ToolMissing);
    }

    [Fact]
    public async Task NativeProcessCommandExecutor_NonZeroExitCommand_ReturnsThatExitCode()
    {
        var executor = new NativeProcessCommandExecutor(Environment.CurrentDirectory);
        var command = OperatingSystem.IsWindows()
            ? new List<string> { "cmd.exe", "/c", "exit 7" }
            : new List<string> { "/bin/sh", "-c", "exit 7" };
        var check = new VerificationCheck("nonzero", VerificationCategory.Build, command, true, TimeSpan.FromSeconds(15));

        var execution = await executor.ExecuteAsync(check, CancellationToken.None);

        Assert.Equal(7, execution.ExitCode);
        Assert.False(execution.ToolMissing);
    }

    [Fact]
    public async Task NativeProcessCommandExecutor_MissingExecutable_ReturnsToolMissing()
    {
        var executor = new NativeProcessCommandExecutor(Environment.CurrentDirectory);
        var check = new VerificationCheck(
            "missing", VerificationCategory.Build,
            [$"definitely-not-a-real-executable-{Guid.NewGuid():N}.exe"], true, TimeSpan.FromSeconds(5));

        var execution = await executor.ExecuteAsync(check, CancellationToken.None);

        Assert.Equal(-1, execution.ExitCode);
        Assert.True(execution.ToolMissing);
        Assert.False(execution.TimedOut);
    }

    // ---- McpTerminalOutcomePublisher: 0% branch-rate before this test (status switch never exercised) ----
    [Theory]
    [InlineData(GoalStatus.Completed, "completed")]
    [InlineData(GoalStatus.Cancelled, "cancelled")]
    [InlineData(GoalStatus.Blocked, "blocked")]
    [InlineData(GoalStatus.BudgetExhausted, "budget_exhausted")]
    [InlineData(GoalStatus.Failed, "failed")]
    [InlineData(GoalStatus.Draft, "failed")] // default arm of the switch
    public async Task McpTerminalOutcomePublisher_PublishAsync_MapsGoalStatusToWireStatus(GoalStatus status, string expectedWireStatus)
    {
        var client = Substitute.For<IMcpClient>();
        client.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(new McpToolResult("ok", false)));
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        var dispatcher = new McpToolDispatcher(client, registry, NullLogger<McpToolDispatcher>.Instance);
        var publisher = new McpTerminalOutcomePublisher(dispatcher);
        var outcome = new TerminalLoopOutcome("goal-1", "corr-1", status, "done", ["cas://evidence/1"]);

        await publisher.PublishAsync(outcome, CancellationToken.None);

        await client.Received(1).CallToolAsync(
            "record_terminal_outcome",
            Arg.Is<JsonObject>(obj =>
                obj["goal_id"]!.GetValue<string>() == "goal-1" &&
                obj["status"]!.GetValue<string>() == expectedWireStatus &&
                obj["summary"]!.GetValue<string>() == "done"),
            Arg.Any<CancellationToken>());
    }
}
