using System.Reflection;
using GsdOrchestrator.Mcp;
using Xunit;

namespace GsdOrchestrator.Tests;

/// <summary>
/// Covers McpException.FromToolError (internal static factory — lines 18-27)
/// via reflection since the method is internal to GsdOrchestrator.
/// </summary>
public class McpExceptionFromToolErrorTests
{
    private static McpException InvokeFromToolError(string text)
    {
        var method = typeof(McpException)
            .GetMethod("FromToolError", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (McpException)method.Invoke(null, [text])!;
    }

    [Fact]
    public void FromToolError_PlainText_IsNotTransientNotSecondary()
    {
        var ex = InvokeFromToolError("generic API error");
        Assert.False(ex.IsTransient);
        Assert.False(ex.IsSecondaryRateLimit);
        Assert.Equal("generic API error", ex.Message);
    }

    [Fact]
    public void FromToolError_SecondaryRateLimitText_SetsBothFlags()
    {
        var ex = InvokeFromToolError("You have exceeded the secondary rate limit.");
        Assert.True(ex.IsSecondaryRateLimit);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public void FromToolError_SecondaryRateLimitMixedCase_Detected()
    {
        var ex = InvokeFromToolError("Secondary Rate Limit triggered");
        Assert.True(ex.IsSecondaryRateLimit);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public void FromToolError_PrimaryRateLimitExceeded_IsTransientNotSecondary()
    {
        var ex = InvokeFromToolError("rate limit exceeded on this endpoint");
        Assert.True(ex.IsTransient);
        Assert.False(ex.IsSecondaryRateLimit);
    }

    [Fact]
    public void FromToolError_ApiRateLimit_IsTransientNotSecondary()
    {
        var ex = InvokeFromToolError("API rate limit reached for user");
        Assert.True(ex.IsTransient);
        Assert.False(ex.IsSecondaryRateLimit);
    }

    [Fact]
    public void FromToolError_PreservesMessageVerbatim()
    {
        var msg = "Tool error: NOT_FOUND (404)";
        var ex = InvokeFromToolError(msg);
        Assert.Equal(msg, ex.Message);
    }
}
