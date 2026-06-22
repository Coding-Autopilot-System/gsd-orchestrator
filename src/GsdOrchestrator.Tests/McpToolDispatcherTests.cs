using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

public class McpToolDispatcherTests
{
    private static McpToolDispatcher BuildDispatcher(IMcpClient client, ResiliencePipelineRegistry<string> registry)
    {
        return new McpToolDispatcher(client, registry, NullLogger<McpToolDispatcher>.Instance);
    }

    private static ResiliencePipelineRegistry<string> BuildPassthroughRegistry()
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) => { });
        return registry;
    }

    // DISPATCHER-01: successful tool call returns result from client
    [Fact]
    public async Task CallAsync_SuccessfulCall_ReturnsClientResult()
    {
        var client = Substitute.For<IMcpClient>();
        client.CallToolAsync("my_tool", Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(new McpToolResult("result-text", false)));

        var sut = BuildDispatcher(client, BuildPassthroughRegistry());
        var result = await sut.CallAsync("my_tool", new JsonObject(), CancellationToken.None);

        Assert.Equal("result-text", result.Text);
        Assert.False(result.IsError);
    }

    // DISPATCHER-02: McpException from client propagates through passthrough pipeline
    [Fact]
    public async Task CallAsync_ClientThrowsMcpException_Propagates()
    {
        var client = Substitute.For<IMcpClient>();
        client.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new McpException("tool failed", isTransient: false));

        var sut = BuildDispatcher(client, BuildPassthroughRegistry());

        await Assert.ThrowsAsync<McpException>(
            () => sut.CallAsync("failing_tool", new JsonObject(), CancellationToken.None));
    }

    // DISPATCHER-03: BrokenCircuitException is wrapped as non-transient McpException
    [Fact]
    public async Task CallAsync_CircuitBreakerOpen_ThrowsWrappedNonTransientMcpException()
    {
        var client = Substitute.For<IMcpClient>();

        // Build registry with a circuit breaker that trips immediately
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("mcp-tools", (b, _) =>
        {
            b.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.01,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder().Handle<McpException>(ex => ex.IsTransient)
            });
        });

        // First call fails to open the circuit
        client.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new McpException("network down", isTransient: true));

        var sut = BuildDispatcher(client, registry);

        // Exhaust the circuit
        try { await sut.CallAsync("tool", new JsonObject()); } catch { }
        try { await sut.CallAsync("tool", new JsonObject()); } catch { }

        // Circuit is now open — next call should get the wrapped McpException
        var ex = await Assert.ThrowsAsync<McpException>(
            () => sut.CallAsync("tool", new JsonObject(), CancellationToken.None));
        Assert.False(ex.IsTransient);
        Assert.Contains("circuit breaker", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // DISPATCHER-04: ListToolsAsync delegates to client
    [Fact]
    public async Task ListToolsAsync_DelegatesToClient()
    {
        var client = Substitute.For<IMcpClient>();
        var tools = new List<McpTool> { new("tool_1", "desc", new JsonObject()) };
        client.ListToolsAsync(Arg.Any<CancellationToken>())
              .Returns(Task.FromResult<IReadOnlyList<McpTool>>(tools));

        var sut = BuildDispatcher(client, BuildPassthroughRegistry());
        var result = await sut.ListToolsAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("tool_1", result[0].Name);
    }

    // DISPATCHER-05: cancellation propagates through the pipeline
    [Fact]
    public async Task CallAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var client = Substitute.For<IMcpClient>();
        client.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .Returns(ci =>
              {
                  cts.Cancel();
                  ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
                  return Task.FromResult(new McpToolResult("", false));
              });

        var sut = BuildDispatcher(client, BuildPassthroughRegistry());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.CallAsync("tool", new JsonObject(), cts.Token));
    }
}
