using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

public class McpToolDispatcherSecondaryRateLimitTests
{
    private static McpToolDispatcher MkD(IMcpClient c)
    {
        var r = new ResiliencePipelineRegistry<string>();
        r.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(c, r, NullLogger<McpToolDispatcher>.Instance);
    }

    // Secondary rate limit path: first call throws secondary RL, then Task.Delay(65s) runs.
    // We cancel quickly to avoid waiting 65s, proving the branch was entered.
    [Fact]
    public async Task CallAsync_SecondaryRateLimit_EntersBranchThenCancels()
    {
        var client = Substitute.For<IMcpClient>();
        var callCount = 0;
        client.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .Returns<Task<McpToolResult>>(ci =>
              {
                  callCount++;
                  throw new McpException("secondary rate limit hit", isTransient: true, isSecondaryRateLimit: true);
              });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => MkD(client).CallAsync("tool", new JsonObject(), cts.Token));
        Assert.True(callCount >= 1, "secondary rate limit branch must have been entered");
    }

    // Non-secondary McpException is NOT retried inside the secondary-RL lambda
    [Fact]
    public async Task CallAsync_NonSecondaryMcpException_PropagatesWithoutSecondaryRetry()
    {
        var client = Substitute.For<IMcpClient>();
        client.CallToolAsync(Arg.Any<string>(), Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new McpException("plain", isTransient: false, isSecondaryRateLimit: false));
        var ex = await Assert.ThrowsAsync<McpException>(
            () => MkD(client).CallAsync("tool", new JsonObject(), CancellationToken.None));
        Assert.False(ex.IsSecondaryRateLimit);
    }
}
