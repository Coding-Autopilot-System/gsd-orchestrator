using GsdOrchestrator.Scheduling;
using Xunit;

namespace GsdOrchestrator.Tests;

public class GoalDecisionPolicyTests
{
    [Theory]
    [InlineData(FailureClass.Transient, DecisionAction.Retry)]
    [InlineData(FailureClass.Deterministic, DecisionAction.CreateRepair)]
    [InlineData(FailureClass.Policy, DecisionAction.Stop)]
    [InlineData(FailureClass.Cancellation, DecisionAction.Stop)]
    [InlineData(FailureClass.Unrecoverable, DecisionAction.Stop)]
    public void Decide_ClassifiesEveryFailureWithBudgetAndEvidence(FailureClass failure, DecisionAction action)
    {
        var result = GoalDecisionPolicy.DecideFailure(failure, consumedAttempts: 1, maxAttempts: 3, noProgressCount: 0, noProgressLimit: 2, ["evidence-1"]);
        Assert.Equal(action, result.Action);
        Assert.Equal(1, result.ConsumedAttempts);
        Assert.NotEmpty(result.Reason);
        Assert.Equal(["evidence-1"], result.EvidenceIds);
    }

    [Fact]
    public void Decide_ExhaustedAttempts_StopsWithExhaustion()
    {
        var result = GoalDecisionPolicy.DecideFailure(FailureClass.Transient, 3, 3, 0, 2, ["e"]);
        Assert.Equal(GoalStopReason.Exhaustion, result.StopReason);
    }

    [Fact]
    public void Decide_NoProgressLimit_StopsBeforeRetry()
    {
        var result = GoalDecisionPolicy.DecideFailure(FailureClass.Deterministic, 1, 3, 2, 2, ["e"]);
        Assert.Equal(GoalStopReason.NoProgress, result.StopReason);
    }

    [Theory]
    [InlineData(GoalStopReason.Passed)]
    [InlineData(GoalStopReason.Exhaustion)]
    [InlineData(GoalStopReason.Cancellation)]
    [InlineData(GoalStopReason.Denial)]
    [InlineData(GoalStopReason.ApprovalWait)]
    [InlineData(GoalStopReason.Deadlock)]
    [InlineData(GoalStopReason.UnrecoverableFault)]
    [InlineData(GoalStopReason.NoProgress)]
    public void Stop_ExposesEveryDistinctReasonWithEvidence(GoalStopReason reason)
    {
        var result = GoalDecisionPolicy.Stop(reason, "explicit outcome", ["evidence-1"]);
        Assert.Equal(reason, result.StopReason);
        Assert.Equal(DecisionAction.Stop, result.Action);
        Assert.NotEmpty(result.EvidenceIds);
    }

    [Fact]
    public void Stop_WithoutEvidence_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => GoalDecisionPolicy.Stop(GoalStopReason.Deadlock, "deadlock", []));
    }
}
