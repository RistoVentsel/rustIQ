namespace RustIQ.Api.Workers;

internal static class WorkerExceptionHandling
{
    public static bool IsExpectedShutdownException(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return exception is OperationCanceledException or ObjectDisposedException;
        }

        return exception is ObjectDisposedException;
    }
}
