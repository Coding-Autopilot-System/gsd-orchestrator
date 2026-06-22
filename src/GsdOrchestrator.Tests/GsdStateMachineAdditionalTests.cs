using System.Text.Json.Nodes;
using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

/// <summary>
/// Additional tests covering GsdStateMachine gaps: GetState, PostFailureCommentAsync.
/// </summary>
public class GsdStateMachineAdditionalTests
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
            client, registry, NullLogger<McpToolDispatcher>.Instance);
        return new GsdStateMachine(
            checkpoints, dispatcher, states, NullLogger<GsdStateMachine>.Instance);
    }

    private static IWorkflowState MakeState(WorkflowState from, WorkflowState to)
    {
        var s = Substitute.For<IWorkflowState>();
        s.State.Returns(from);
        s.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
         .Returns(ci => Task.FromResult(ci.Arg<GsdWorkflowContext>() with { CurrentState = to }));
        return s;
    }

    // SM-GETSTATE-01: GetState returns registered state handler
    [Fact]
    public void GetState_RegisteredState_ReturnsHandler()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        var idleState = MakeState(WorkflowState.Idle, WorkflowState.Done);
        var sut = BuildSut(checkpoints, [idleState]);

        var handler = sut.GetState(WorkflowState.Idle);

        Assert.NotNull(handler);
        Assert.Equal(WorkflowState.Idle, handler.State);
    }

    // SM-GETSTATE-02: GetState for unregistered state throws InvalidOperationException
    [Fact]
    public void GetState_UnregisteredState_ThrowsInvalidOperationException()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        var sut = BuildSut(checkpoints, []);

        var ex = Assert.Throws<InvalidOperationException>(
            () => sut.GetState(WorkflowState.Idle));
        Assert.Contains("Idle", ex.Message);
    }

    // SM-FAILURE-01: on failure, add_issue_comment is called with failure details
    [Fact]
    public async Task RunAsync_StateThrowsException_PostsFailureComment()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(new McpToolResult("", false)));

        var failingState = Substitute.For<IWorkflowState>();
        failingState.State.Returns(WorkflowState.Idle);
        failingState.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
                    .Returns<Task<GsdWorkflowContext>>(_ => throw new InvalidOperationException("boom"));

        var sut = BuildSut(checkpoints, [failingState], mcp);

        var ctx = await sut.RunAsync("owner", "repo", 1, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, ctx.CurrentState);
        // Verify add_issue_comment was called for the failure notification
        await mcp.Received().CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // SM-FAILURE-02: when Issue is null, PostFailureCommentAsync is a no-op (no MCP call)
    [Fact]
    public async Task RunAsync_FailureWithNullIssue_DoesNotCallMcp()
    {
        // Create a state machine with NO states and override so failure happens with no issue context
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var mcp = Substitute.For<IMcpClient>();

        var failingState = Substitute.For<IWorkflowState>();
        failingState.State.Returns(WorkflowState.Idle);
        failingState.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
                    .Returns<Task<GsdWorkflowContext>>(ci =>
                    {
                        // Return a failed context with no issue
                        var ctx = ci.Arg<GsdWorkflowContext>() with
                        {
                            Issue = null,
                            FailureReason = "no issue",
                            CurrentState = WorkflowState.Failed
                        };
                        return Task.FromResult(ctx);
                    });

        var sut = BuildSut(checkpoints, [failingState], mcp);
        var result = await sut.RunAsync("owner", "repo", 1, triageModeOnly: false, CancellationToken.None);

        Assert.Equal(WorkflowState.Failed, result.CurrentState);
        // No add_issue_comment should be fired when Issue is null
        await mcp.DidNotReceive().CallToolAsync(
            Arg.Is<string>("add_issue_comment"),
            Arg.Any<JsonObject>(),
            Arg.Any<CancellationToken>());
    }

    // SM-FAILURE-03: PostFailureCommentAsync McpException is swallowed (logged, not rethrown)
    [Fact]
    public async Task RunAsync_FailureCommentThrows_DoesNotRethrow()
    {
        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.SaveAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var mcp = Substitute.For<IMcpClient>();
        mcp.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
           .Returns<Task<McpToolResult>>(_ => throw new McpException("mcp dead", isTransient: false));

        var failingState = Substitute.For<IWorkflowState>();
        failingState.State.Returns(WorkflowState.Idle);
        failingState.ExecuteAsync(Arg.Any<GsdWorkflowContext>(), Arg.Any<CancellationToken>())
                    .Returns<Task<GsdWorkflowContext>>(_ => throw new InvalidOperationException("state exploded"));

        var sut = BuildSut(checkpoints, [failingState], mcp);

        // Should not throw even when MCP call for comment also fails
        var ctx = await sut.RunAsync("owner", "repo", 1, triageModeOnly: false, CancellationToken.None);
        Assert.Equal(WorkflowState.Failed, ctx.CurrentState);
    }
}
