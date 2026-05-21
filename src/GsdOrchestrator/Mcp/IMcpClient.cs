using System.Text.Json.Nodes;

namespace GsdOrchestrator.Mcp;

public interface IMcpClient : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken ct = default);
    Task<McpToolResult> CallToolAsync(string name, JsonObject arguments, CancellationToken ct = default);
}
