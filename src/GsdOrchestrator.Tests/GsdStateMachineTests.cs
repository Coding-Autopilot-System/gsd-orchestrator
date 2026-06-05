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

public class GsdStateMachineTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GsdStateMachine BuildSut(
        ICheckpointStore checkpoints,
        IWorkflowState[] states,
        IMcpClient? mcpClient = null)
    {
        var client = mcpClient ?? Substitute.For<IMcpClient>();
        var registry = new ResiliencePipelineRegistry<string>();
        // Pass-through pipeline — no retry/circuit breaker in unit tests
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

    private static IWorkflowState MakeState(WorkflowState from, WorkflowState to)
    {
        var state = Substitute.For<IWorkflowState>();
        state.State.Returns(from);
        state.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<GsdWorkflowContext>() with { CurrentState = to }));
        return state;
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SingleStateTransitionsToDone_ReturnsDoneContext()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        checkpoints.ArchiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var idleState = MakeState(WorkflowState.Idle, WorkflowState.Done);
        var sut = BuildSut(checkpoints, [idleState]);

        var ctx = await sut.RunAsync("owner", "repo", 42, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Done, ctx.CurrentState);
        await checkpoints.Received().ArchiveAsync(ctx.WorkflowId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_StateThrowsException_ContextTransitionsToFailed()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var idleState = Substitute.For<IWorkflowState>();
        idleState.State.Returns(WorkflowState.Idle);
        idleState.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("simulated failure"));
        var sut = BuildSut(checkpoints, [idleState]);

        var ctx = await sut.RunAsync("owner", "repo", 1, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, ctx.CurrentState);
        Assert.NotNull(ctx.FailureReason);
    }

    [Fact]
    public async Task RunAsync_NoHandlerForState_ThrowsInvalidOperationException()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();

        // No states registered — Idle has no handler, throws before SaveAsync
        var sut = BuildSut(checkpoints, []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync("owner", "repo", 1, triageModeOnly: false, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_MultipleStateTransitions_AllCheckpointsSaved()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        checkpoints.ArchiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var idleState = MakeState(WorkflowState.Idle, WorkflowState.Analyzing);
        var analyzingState = MakeState(WorkflowState.Analyzing, WorkflowState.Done);
        var sut = BuildSut(checkpoints, [idleState, analyzingState]);

        var ctx = await sut.RunAsync("owner", "repo", 5, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Done, ctx.CurrentState);
        // SaveAsync: before Idle (×1) + before Analyzing (×1) + final checkpoint (×1) = 3
        await checkpoints.Received(3).SaveAsync(
            Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeAsync_CheckpointExists_ResumesFromSavedState()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        var savedCtx = new GsdWorkflowContext
        {
            WorkflowId = "test-wf-01",
            CurrentState = WorkflowState.Analyzing,
            Issue = new IssueContext(7, "Test issue", "", [], "owner", "repo", "main")
        };
        checkpoints.LoadAsync("test-wf-01", Arg.Any<CancellationToken>())
            .Returns(savedCtx);
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        checkpoints.ArchiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var analyzingState = MakeState(WorkflowState.Analyzing, WorkflowState.Done);
        var sut = BuildSut(checkpoints, [analyzingState]);

        var ctx = await sut.ResumeAsync("test-wf-01", CancellationToken.None);

        Assert.Equal(WorkflowState.Done, ctx.CurrentState);
    }

    [Fact]
    public async Task ResumeAsync_NoCheckpointExists_ThrowsInvalidOperationException()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GsdWorkflowContext?)null);
        var sut = BuildSut(checkpoints, []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ResumeAsync("missing-id", CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var idleState = Substitute.For<IWorkflowState>();
        idleState.State.Returns(WorkflowState.Idle);
        idleState.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                cts.Cancel();
                ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return ci.Arg<GsdWorkflowContext>();
            });
        var sut = BuildSut(checkpoints, [idleState]);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.RunAsync("owner", "repo", 1, triageModeOnly: false, cts.Token));
    }
}
