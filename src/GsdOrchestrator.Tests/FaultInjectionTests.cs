using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class FaultInjectionTests
{
    private static GsdStateMachine BuildSut(
        ICheckpointStore checkpoints,
        IWorkflowState[] states,
        IMcpClient? mcpClient = null)
    {
        var client = mcpClient ?? Substitute.For<IMcpClient>();
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        var dispatcher = new McpToolDispatcher(
            client,
            registry,
            NullLogger<McpToolDispatcher>.Instance);
        return new GsdStateMachine(
            checkpoints,
            dispatcher,
            states,
            NullLogger<GsdStateMachine>.Instance);
    }

    private static IWorkflowState MakeTransitionState(WorkflowState from, WorkflowState to)
    {
        var state = Substitute.For<IWorkflowState>();
        state.State.Returns(from);
        state.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<GsdWorkflowContext>().Transition(to)));
        return state;
    }

    [Fact]
    public async Task RunAsync_McpFailureMidGoal_ProducesTypedFailedStateAndCheckpoint()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var idle = MakeTransitionState(WorkflowState.Idle, WorkflowState.Analyzing);
        var analyzing = MakeTransitionState(WorkflowState.Analyzing, WorkflowState.Editing);
        var editing = Substitute.For<IWorkflowState>();
        editing.State.Returns(WorkflowState.Editing);
        editing.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpException("simulated MCP failure", isTransient: true));

        var sut = BuildSut(checkpoints, [idle, analyzing, editing]);

        var ctx = await sut.RunAsync("owner", "repo", 42, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, ctx.CurrentState);
        Assert.Equal(WorkflowState.Editing, ctx.FailedState);
        Assert.Contains("simulated MCP failure", ctx.FailureReason);
        await checkpoints.Received().SaveAsync(
            Arg.Is<GsdWorkflowContext>(saved =>
                saved.CurrentState == WorkflowState.Failed &&
                saved.FailedState == WorkflowState.Editing &&
                saved.FailureReason != null &&
                saved.FailureReason.Contains("simulated MCP failure", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeAsync_RetriesFailedStateOnce_ThenHaltsDeterministically()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var firstCheckpoint = FailedCheckpoint("retry-wf", retryCount: 0);
        var secondCheckpoint = FailedCheckpoint("retry-wf", retryCount: 1);
        checkpoints.LoadAsync("retry-wf", Arg.Any<CancellationToken>())
            .Returns(firstCheckpoint, secondCheckpoint);

        var editing = Substitute.For<IWorkflowState>();
        editing.State.Returns(WorkflowState.Editing);
        editing.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpException("editing exploded again", isTransient: true));

        var sut = BuildSut(checkpoints, [editing]);

        var firstResult = await sut.ResumeAsync("retry-wf", CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, firstResult.CurrentState);
        Assert.Equal(1, firstResult.RetryCount);
        Assert.Equal(WorkflowState.Editing, firstResult.FailedState);
        Assert.Contains("editing exploded again", firstResult.FailureReason);
        await checkpoints.Received().SaveAsync(
            Arg.Is<GsdWorkflowContext>(saved =>
                saved.CurrentState == WorkflowState.Failed &&
                saved.RetryCount == 1 &&
                saved.FailedState == WorkflowState.Editing),
            Arg.Any<CancellationToken>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ResumeAsync("retry-wf", CancellationToken.None));
        Assert.Contains("recovery retry limit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_TruncatesFailureReasonTo1024Characters()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var oversizedMessage = new string('x', 1200);
        var idle = Substitute.For<IWorkflowState>();
        idle.State.Returns(WorkflowState.Idle);
        idle.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(oversizedMessage));

        var sut = BuildSut(checkpoints, [idle]);

        var ctx = await sut.RunAsync("owner", "repo", 7, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, ctx.CurrentState);
        Assert.NotNull(ctx.FailureReason);
        Assert.Equal(1024, ctx.FailureReason!.Length);
    }

    [Fact]
    public async Task RunAsync_BudgetFailureMessage_SetsBudgetExhaustedStopReason()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var idle = Substitute.For<IWorkflowState>();
        idle.State.Returns(WorkflowState.Idle);
        idle.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("budget exhausted during planning"));

        var sut = BuildSut(checkpoints, [idle]);

        var ctx = await sut.RunAsync("owner", "repo", 8, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, ctx.CurrentState);
        Assert.Equal(TerminalStopReason.BudgetExhausted, ctx.StopReason);
    }

    private static GsdWorkflowContext FailedCheckpoint(string workflowId, int retryCount) =>
        new()
        {
            WorkflowId = workflowId,
            CurrentState = WorkflowState.Failed,
            FailedState = WorkflowState.Editing,
            RetryCount = retryCount,
            FailureReason = "prior failure",
            Issue = new IssueContext(1, "Retry workflow", "", [], "owner", "repo", "main")
        };
}
