using GsdOrchestrator.Auth;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace GsdOrchestrator.Tests;

/// <summary>
/// Tests for GitHubPatProvider — reads GitHub PAT from IConfiguration.
/// </summary>
public class GitHubPatProviderTests
{
    private static IConfiguration BuildConfig(string? token)
    {
        var config = Substitute.For<IConfiguration>();
        config["GITHUB_PERSONAL_ACCESS_TOKEN"].Returns(token);
        return config;
    }

    // PATPROV-01: valid ghp_ token is accepted and stored
    [Fact]
    public void Constructor_ValidGhpToken_StoresToken()
    {
        var config = BuildConfig("ghp_abc123XYZ");
        var sut = new GitHubPatProvider(config);
        Assert.Equal("ghp_abc123XYZ", sut.Token);
    }

    // PATPROV-01: valid ghs_ token is accepted
    [Fact]
    public void Constructor_ValidGhsToken_StoresToken()
    {
        var config = BuildConfig("ghs_servicesecret99");
        var sut = new GitHubPatProvider(config);
        Assert.Equal("ghs_servicesecret99", sut.Token);
    }

    // PATPROV-01: valid github_pat_ fine-grained token is accepted
    [Fact]
    public void Constructor_ValidGithubPatPrefixToken_StoresToken()
    {
        var config = BuildConfig("github_pat_11ABCDE0000_LONGTOKEN");
        var sut = new GitHubPatProvider(config);
        Assert.Equal("github_pat_11ABCDE0000_LONGTOKEN", sut.Token);
    }

    // PATPROV-02: missing token throws InvalidOperationException
    [Fact]
    public void Constructor_NullToken_ThrowsInvalidOperationException()
    {
        var config = BuildConfig(null);
        var ex = Assert.Throws<InvalidOperationException>(() => new GitHubPatProvider(config));
        Assert.Contains("GITHUB_PERSONAL_ACCESS_TOKEN", ex.Message);
    }

    // PATPROV-02: whitespace-only token throws InvalidOperationException
    [Fact]
    public void Constructor_WhitespaceToken_ThrowsInvalidOperationException()
    {
        var config = BuildConfig("   ");
        var ex = Assert.Throws<InvalidOperationException>(() => new GitHubPatProvider(config));
        Assert.Contains("GITHUB_PERSONAL_ACCESS_TOKEN", ex.Message);
    }

    // PATPROV-03: token with wrong prefix throws InvalidOperationException
    [Fact]
    public void Constructor_WrongPrefixToken_ThrowsInvalidOperationException()
    {
        var config = BuildConfig("xgh_invalidtoken123");
        var ex = Assert.Throws<InvalidOperationException>(() => new GitHubPatProvider(config));
        Assert.Contains("valid GitHub token", ex.Message);
    }

    // PATPROV-03: plain token with no prefix throws
    [Fact]
    public void Constructor_PlainStringToken_ThrowsInvalidOperationException()
    {
        var config = BuildConfig("mysecrettoken");
        var ex = Assert.Throws<InvalidOperationException>(() => new GitHubPatProvider(config));
        Assert.Contains("valid GitHub token", ex.Message);
    }
}
