using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.Registry;
using Xunit;

namespace GsdOrchestrator.Tests;

public class ReviewingStateErrorPathTests
{
    private static McpToolDispatcher MkD(IMcpClient c)
    {
        var r = new ResiliencePipelineRegistry<string>();
        r.TryAddBuilder("mcp-tools", (b, _) => { });
        return new McpToolDispatcher(c, r, NullLogger<McpToolDispatcher>.Instance);
    }
    private static IConfiguration Cfg(string rev = "")
    { var c = Substitute.For<IConfiguration>(); c["GSD_REVIEWERS"].Returns(rev); return c; }
    private static ReviewingState Sut(IMcpClient m, IChatClient l, string rev = "") =>
        new(MkD(m), l, Cfg(rev), NullLogger<ReviewingState>.Instance);
    private static GsdWorkflowContext PrCtx(string diff = "d") =>
        new() { PrReview = new PrReviewContext(7,"o","r",diff), CurrentState = WorkflowState.Reviewing };
    private static IChatClient LlmApprove()
    {
        var l = Substitute.For<IChatClient>();
        l.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(),Arg.Any<ChatOptions?>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
             "{\"verdict\":\"APPROVE\",\"summary\":\"ok\",\"comments\":[]}"))));
        return l;
    }

    [Fact]
    public async Task GetPr_ReturnsError_FallsBackAndCompletes()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("err",true)));
        m.CallToolAsync(Arg.Is<string>("create_pull_request_review"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{}",false)));
        var res = await Sut(m,LlmApprove()).ExecuteAsync(PrCtx(),CancellationToken.None);
        Assert.Equal(WorkflowState.Done,res.CurrentState);
    }

    [Fact]
    public async Task GetPr_ThrowsMcpException_FallsBackAndCompletes()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .ThrowsAsync(new McpException("notfound",isTransient:false));
        m.CallToolAsync(Arg.Is<string>("create_pull_request_review"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{}",false)));
        var res = await Sut(m,LlmApprove()).ExecuteAsync(PrCtx(),CancellationToken.None);
        Assert.Equal(WorkflowState.Done,res.CurrentState);
    }

    [Fact]
    public async Task LlmThrows_AllAttempts_ThrowsInvalidOperationException()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{\"title\":\"T\",\"body\":\"\"}",false)));
        var l = Substitute.For<IChatClient>();
        l.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(),Arg.Any<ChatOptions?>(),Arg.Any<CancellationToken>())
         .ThrowsAsync(new HttpRequestException("down"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Sut(m,l).ExecuteAsync(PrCtx(),CancellationToken.None));
    }

    [Fact]
    public async Task LlmCanceled_PropagatesImmediately()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{\"title\":\"T\",\"body\":\"\"}",false)));
        using var cts = new CancellationTokenSource();
        var l = Substitute.For<IChatClient>();
        l.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(),Arg.Any<ChatOptions?>(),Arg.Any<CancellationToken>())
         .Returns<Task<ChatResponse>>(ci=>{
             cts.Cancel();
             ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
             return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,"")));
         });
        await Assert.ThrowsAsync<OperationCanceledException>(()=>Sut(m,l).ExecuteAsync(PrCtx(),cts.Token));
    }

    [Fact]
    public async Task SubmitReview_ReturnsError_ThrowsMcpException()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{\"title\":\"T\",\"body\":\"\"}",false)));
        m.CallToolAsync(Arg.Is<string>("create_pull_request_review"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("fail",true)));
        await Assert.ThrowsAsync<McpException>(()=>Sut(m,LlmApprove()).ExecuteAsync(PrCtx(),CancellationToken.None));
    }

    [Fact]
    public async Task WithReviewers_CompletesNormally()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{\"title\":\"T\",\"body\":\"\"}",false)));
        m.CallToolAsync(Arg.Is<string>("create_pull_request_review"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{}",false)));
        var res = await Sut(m,LlmApprove(),rev:"alice,bob").ExecuteAsync(PrCtx(),CancellationToken.None);
        Assert.Equal(WorkflowState.Done,res.CurrentState);
    }

    [Fact]
    public async Task LargeDiff_TruncatedBeforeLlm()
    {
        var m = Substitute.For<IMcpClient>();
        m.CallToolAsync(Arg.Is<string>("get_pull_request"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{\"title\":\"T\",\"body\":\"\"}",false)));
        m.CallToolAsync(Arg.Is<string>("create_pull_request_review"),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(new McpToolResult("{}",false)));
        var big = new string('+',50_000);
        var seen = new List<string>();
        var l = Substitute.For<IChatClient>();
        l.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(),Arg.Any<ChatOptions?>(),Arg.Any<CancellationToken>())
         .Returns(ci=>{
             foreach(var msg in ci.Arg<IEnumerable<ChatMessage>>()) if(msg.Text!=null)seen.Add(msg.Text);
             return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                 "{\"verdict\":\"APPROVE\",\"summary\":\"ok\",\"comments\":[]}")));
         });
        await Sut(m,l).ExecuteAsync(PrCtx(big),CancellationToken.None);
        Assert.Contains(seen,s=>s.Contains("[diff truncated"));
    }
}
