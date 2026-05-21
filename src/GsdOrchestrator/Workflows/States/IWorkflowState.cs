using GsdOrchestrator.Workflows.Models;

namespace GsdOrchestrator.Workflows.States;

public interface IWorkflowState
{
    WorkflowState State { get; }
    Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct);
}
