using GsdOrchestrator.Loop;
using GsdOrchestrator.Scheduling;
using Xunit;

namespace GsdOrchestrator.Tests;

public sealed class FailureStateTests
{
    [Fact]
    public void Timeout_Is_Classified_As_Transient_And_Retryable()
    {
        var result = FailureClassifier.Classify(new TimeoutException("timed out"), "component");

        Assert.Equal(FailureClass.Transient, result.FailureClass);
        Assert.True(result.Retryable);
        Assert.Equal(5d, result.RetryAfterSeconds);
    }

    [Fact]
    public void OperationCanceled_Is_Classified_As_Cancellation()
    {
        var result = FailureClassifier.Classify(new OperationCanceledException("cancelled"), "component");

        Assert.Equal(FailureClass.Cancellation, result.FailureClass);
        Assert.False(result.Retryable);
        Assert.Null(result.RetryAfterSeconds);
    }

    [Fact]
    public void InvalidOperation_Is_Classified_As_Deterministic()
    {
        var result = FailureClassifier.Classify(new InvalidOperationException("Goal 'x' was not found."), "component");

        Assert.Equal(FailureClass.Deterministic, result.FailureClass);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void UnauthorizedAccess_Is_Classified_As_Policy()
    {
        var result = FailureClassifier.Classify(new UnauthorizedAccessException("denied"), "component");

        Assert.Equal(FailureClass.Policy, result.FailureClass);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Unknown_Exception_Falls_Back_To_Unrecoverable()
    {
        var result = FailureClassifier.Classify(new UnknownFailureException("boom"), "component");

        Assert.Equal(FailureClass.Unrecoverable, result.FailureClass);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Cause_Chain_Is_Collected_Outermost_First()
    {
        var exception = new InvalidOperationException("outer", new TimeoutException("inner"));

        var result = FailureClassifier.Classify(exception, "component");

        Assert.Equal(["outer", "inner"], result.CauseChain);
    }

    private sealed class UnknownFailureException(string message) : Exception(message);
}
