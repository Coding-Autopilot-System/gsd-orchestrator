using GsdOrchestrator.Mcp;
using Xunit;

namespace GsdOrchestrator.Tests;

public class McpExceptionTests
{
    [Fact]
    public void Constructor_DefaultFlags_AreAllFalse()
    {
        var ex = new McpException("some error");
        Assert.False(ex.IsTransient);
        Assert.False(ex.IsSecondaryRateLimit);
        Assert.Null(ex.ErrorCode);
        Assert.Equal("some error", ex.Message);
    }

    [Fact]
    public void Constructor_IsTransientTrue_SetsFlag()
    {
        var ex = new McpException("transient", isTransient: true);
        Assert.True(ex.IsTransient);
        Assert.False(ex.IsSecondaryRateLimit);
    }

    [Fact]
    public void Constructor_IsSecondaryRateLimitTrue_SetsBothFlags()
    {
        var ex = new McpException("rate limit", isTransient: true, isSecondaryRateLimit: true);
        Assert.True(ex.IsTransient);
        Assert.True(ex.IsSecondaryRateLimit);
    }

    [Fact]
    public void Constructor_WithErrorCode_StoresErrorCode()
    {
        var ex = new McpException("error", errorCode: 429);
        Assert.Equal(429, ex.ErrorCode);
    }

    [Fact]
    public void Constructor_IsTransientFalseIsSecondaryFalse_FlagsStayFalse()
    {
        var ex = new McpException("msg", isTransient: false, isSecondaryRateLimit: false);
        Assert.False(ex.IsTransient);
        Assert.False(ex.IsSecondaryRateLimit);
    }

    [Fact]
    public void Constructor_AllParameters_Stored()
    {
        var ex = new McpException("full", isTransient: true, isSecondaryRateLimit: true, errorCode: 503);
        Assert.Equal("full", ex.Message);
        Assert.True(ex.IsTransient);
        Assert.True(ex.IsSecondaryRateLimit);
        Assert.Equal(503, ex.ErrorCode);
    }

    [Fact]
    public void McpException_IsException_InheritanceChain()
    {
        var ex = new McpException("test");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
