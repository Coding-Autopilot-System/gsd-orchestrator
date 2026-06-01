using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace GsdOrchestrator.Mcp;

/// <summary>
/// Wraps IMcpClient with resilience (Polly retry + circuit breaker), logging, and secondary
/// rate limit special-casing. Inject and use this instead of calling IMcpClient directly.
/// </summary>
public sealed class McpToolDispatcher
{
    private readonly IMcpClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<McpToolDispatcher> _logger;

    public McpToolDispatcher(
        IMcpClient client,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<McpToolDispatcher> logger)
    {
        _client = client;
        _pipeline = pipelineProvider.GetPipeline("mcp-tools");
        _logger = logger;
    }

    public async Task<McpToolResult> CallAsync(
        string tool, JsonObject args, CancellationToken ct = default)
    {
        try
        {
            return await _pipeline.ExecuteAsync(async token =>
            {
                try
                {
                    return await _client.CallToolAsync(tool, args, token);
                }
                catch (McpException ex) when (ex.IsSecondaryRateLimit)
                {
                    // Secondary rate limit: MUST wait at least 60 seconds — bypass Polly retry
                    _logger.LogWarning(
                        "Secondary rate limit on '{Tool}'. Waiting 65s before retry.", tool);
                    await Task.Delay(TimeSpan.FromSeconds(65), token);
                    return await _client.CallToolAsync(tool, args, token);
                }
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            throw new McpException(
                "MCP circuit breaker open — too many consecutive failures",
                isTransient: false);
        }
    }

    public Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken ct = default) =>
        _client.ListToolsAsync(ct);
}
