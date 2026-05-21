namespace GsdOrchestrator.Mcp;

public sealed class McpException : Exception
{
    public bool IsTransient { get; }
    public bool IsSecondaryRateLimit { get; }
    public int? ErrorCode { get; }

    public McpException(string message, bool isTransient = false, bool isSecondaryRateLimit = false, int? errorCode = null)
        : base(message)
    {
        IsTransient = isTransient;
        IsSecondaryRateLimit = isSecondaryRateLimit;
        ErrorCode = errorCode;
    }

    internal static McpException FromToolError(string errorText)
    {
        bool isSecondary = errorText.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase);
        bool isPrimary = errorText.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase)
                      || errorText.Contains("API rate limit", StringComparison.OrdinalIgnoreCase);

        return new McpException(
            message: errorText,
            isTransient: isSecondary || isPrimary,
            isSecondaryRateLimit: isSecondary);
    }
}
