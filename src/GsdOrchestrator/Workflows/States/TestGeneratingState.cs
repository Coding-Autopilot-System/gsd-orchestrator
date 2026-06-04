using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class TestGeneratingState : IWorkflowState
{
    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<TestGeneratingState> _logger;

    public WorkflowState State => WorkflowState.TestGenerating;

    public TestGeneratingState(McpToolDispatcher mcp, IChatClient llm, ILogger<TestGeneratingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
        => throw new NotImplementedException("Wave 2 implementation pending");
}
