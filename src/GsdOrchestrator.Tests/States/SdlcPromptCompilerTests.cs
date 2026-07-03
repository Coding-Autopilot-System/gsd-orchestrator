using GsdOrchestrator.Workflows.Models;
using Xunit;

namespace GsdOrchestrator.Tests.States;

public class SdlcPromptCompilerTests
{
    [Fact]
    public void BuildPhasePrompt_IsDeterministic()
    {
        var profile = SdlcProfile.CasSdlcV1;
        var phase = profile.Phases[0];

        var first = SdlcPromptCompiler.BuildPhasePrompt(
            "Improve the loop",
            profile,
            phase,
            ["validated input"],
            ["prior evidence"],
            ["memory field"]);

        var second = SdlcPromptCompiler.BuildPhasePrompt(
            "Improve the loop",
            profile,
            phase,
            ["validated input"],
            ["prior evidence"],
            ["memory field"]);

        Assert.Equal(first, second);
        Assert.Contains("Goal: Improve the loop", first);
        Assert.Contains("Phase: understand / Understand", first);
        Assert.Contains("Rollback behavior", first);
    }

    [Fact]
    public void Digest_ReturnsStableSha256Hex()
    {
        var digest = SdlcPromptCompiler.Digest("prompt body");

        Assert.Equal(64, digest.Length);
        Assert.Equal(digest, SdlcPromptCompiler.Digest("prompt body"));
    }

    [Fact]
    public void TransitionPlanner_UsesProfileNavigation()
    {
        var profile = SdlcProfile.CasSdlcV1;

        Assert.Equal("research", SdlcTransitionPlanner.NextPhaseId(profile, "understand"));
        Assert.Equal("understand", SdlcTransitionPlanner.RollbackOrigin(profile, "research", ["understand"]));
        Assert.Equal("plan", SdlcTransitionPlanner.CurrentPhase(profile, "plan").Id);
    }
}
