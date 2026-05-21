using System.Text.Json.Nodes;

namespace GsdOrchestrator.Mcp;

public sealed record McpTool(
    string Name,
    string Description,
    JsonObject InputSchema);

public sealed record McpToolResult(
    string Text,
    bool IsError)
{
    /// <summary>
    /// Parses the inner JSON string that GitHub MCP server wraps in content[0].text.
    /// Always use this instead of reading Text directly for GitHub API responses.
    /// </summary>
    public JsonNode? ParseInnerJson()
    {
        if (string.IsNullOrWhiteSpace(Text)) return null;
        try { return JsonNode.Parse(Text); }
        catch { return null; }
    }
}
