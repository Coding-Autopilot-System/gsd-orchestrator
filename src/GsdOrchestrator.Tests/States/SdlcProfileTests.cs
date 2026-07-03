using GsdOrchestrator.Workflows.Models;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class SdlcProfileTests
{
    [Fact]
    public void CasSdlcV1_ContainsTwelvePhases_InFiveBatches()
    {
        var profile = SdlcProfile.CasSdlcV1;

        Assert.Equal("cas-sdlc-v1", profile.ProfileId);
        Assert.Equal("v1.1", profile.ProfileVersion);
        Assert.Equal(12, profile.Phases.Count);
        Assert.Contains(profile.Phases, phase => phase.Id == "understand" && phase.Batch == SdlcBatch.Discovery);
        Assert.Contains(profile.Phases, phase => phase.Id == "risk-assessment" && phase.Batch == SdlcBatch.Design);
        Assert.Contains(profile.Phases, phase => phase.Id == "implement" && phase.Batch == SdlcBatch.Change);
        Assert.Contains(profile.Phases, phase => phase.Id == "review" && phase.Batch == SdlcBatch.Assurance);
        Assert.Contains(profile.Phases, phase => phase.Id == "finished" && phase.Batch == SdlcBatch.Closure);
    }

    [Fact]
    public void GsdWorkflowContext_DefaultsToCasSdlcProfile()
    {
        var ctx = new GsdWorkflowContext();

        Assert.Equal("1.0", ctx.SchemaVersion);
        Assert.Equal(SdlcProfile.CasSdlcV1, ctx.SdlcProfile);
    }

    [Fact]
    public void Transition_KeepsPhaseIdentityButResetsWorkflowState()
    {
        var ctx = new GsdWorkflowContext().WithSdlcRun("Improve the loop");

        var next = ctx.Transition(WorkflowState.Branching);

        Assert.NotNull(next.SdlcRun);
        Assert.Equal("understand", next.SdlcRun!.CurrentPhaseId);
        Assert.Equal("repo-verifier", next.SdlcRun.CurrentVerifier);
        Assert.Equal(WorkflowState.Branching, next.CurrentState);
    }

    [Fact]
    public void WithSdlcVerification_UpdatesPhaseAndRollbackMetadata()
    {
        var ctx = new GsdWorkflowContext().WithSdlcRun("Improve the loop");
        var next = ctx.WithSdlcVerification(new SdlcVerificationOutcome(
            "research",
            "repo-verifier",
            Passed: false,
            InvalidatedPhaseIds: ["understand"],
            Reason: "evidence missing"));

        Assert.NotNull(next.SdlcRun);
        Assert.Equal("research", next.SdlcRun!.CurrentPhaseId);
        Assert.Equal(SdlcPhaseStatus.Failed, next.SdlcRun.CurrentPhaseStatus);
        Assert.Equal("understand", next.SdlcRun.RollbackOrigin);
        Assert.Equal("evidence missing", next.SdlcRun.RollbackReason);
    }

    [Fact]
    public void ResolveRollbackOrigin_UsesInvalidatedPhaseWhenProvided()
    {
        var profile = SdlcProfile.CasSdlcV1;

        var rollbackOrigin = profile.ResolveRollbackOrigin("verify", ["research", "analyze"]);

        Assert.Equal("research", rollbackOrigin);
    }

    [Fact]
    public void NextPhaseId_ReturnsNextSequentialPhase()
    {
        var profile = SdlcProfile.CasSdlcV1;

        Assert.Equal("research", profile.NextPhaseId("understand"));
        Assert.Equal("finished", profile.NextPhaseId("update-memory"));
        Assert.Null(profile.NextPhaseId("finished"));
    }
}
