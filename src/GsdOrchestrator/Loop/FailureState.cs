using System.Net.Http;
using System.Net.Sockets;
using GsdOrchestrator.Scheduling;

namespace GsdOrchestrator.Loop;

public sealed record FailureState(
    FailureClass FailureClass,
    string Component,
    string Message,
    bool Retryable,
    string? ExceptionType = null,
    IReadOnlyList<string>? CauseChain = null,
    double? RetryAfterSeconds = null);

public static class FailureClassifier
{
    private const int MaxMessageLength = 1024;
    private const int MaxCauseChainEntries = 10;

    public static FailureState Classify(Exception exception, string component)
    {
        var (failureClass, retryable, retryAfterSeconds) = exception switch
        {
            OperationCanceledException => (FailureClass.Cancellation, false, (double?)null),
            TimeoutException => (FailureClass.Transient, true, 5d),
            HttpRequestException => (FailureClass.Transient, true, 5d),
            SocketException => (FailureClass.Transient, true, 5d),
            UnauthorizedAccessException => (FailureClass.Policy, false, (double?)null),
            ArgumentOutOfRangeException => (FailureClass.Policy, false, (double?)null),
            InvalidOperationException => (FailureClass.Deterministic, false, (double?)null),
            ArgumentException => (FailureClass.Deterministic, false, (double?)null),
            _ => (FailureClass.Unrecoverable, false, (double?)null),
        };

        return new FailureState(
            failureClass,
            component,
            Truncate(exception.Message),
            retryable,
            exception.GetType().FullName,
            BuildCauseChain(exception),
            retryAfterSeconds);
    }

    private static string Truncate(string message) =>
        message.Length <= MaxMessageLength ? message : message[..MaxMessageLength];

    private static IReadOnlyList<string>? BuildCauseChain(Exception exception)
    {
        var chain = new List<string>(capacity: 4);
        var current = exception;
        while (current is not null && chain.Count < MaxCauseChainEntries)
        {
            chain.Add(Truncate(current.Message));
            current = current.InnerException;
        }

        return chain.Count == 0 ? null : chain;
    }
}
