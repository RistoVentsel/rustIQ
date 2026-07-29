namespace RustIQ.Infrastructure.ServerQuery;

public sealed class BattleMetricsRequestPacer(TimeSpan minimumRequestInterval)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRequestAtUtc = DateTimeOffset.MinValue;

    public async Task WaitForNextRequestAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var delay = _nextRequestAtUtc - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            _nextRequestAtUtc = DateTimeOffset.UtcNow.Add(minimumRequestInterval);
        }
        finally
        {
            _gate.Release();
        }
    }
}
