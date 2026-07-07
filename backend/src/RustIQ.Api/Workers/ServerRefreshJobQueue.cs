using System.Threading.Channels;
using RustIQ.Application.Servers;

namespace RustIQ.Api.Workers;

public sealed class ServerRefreshJobQueue(
    Channel<bool> channel,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ServerRefreshJobQueue> logger) : BackgroundService, IServerRefreshJobQueue
{
    private readonly object _stateLock = new();
    private int _queuedOrRunning;
    private bool _isRunning;
    private DateTimeOffset? _queuedAtUtc;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _completedAtUtc;
    private DateTimeOffset? _failedAtUtc;
    private string? _lastErrorMessage;

    public bool TryQueueRefreshAll()
    {
        if (Interlocked.CompareExchange(ref _queuedOrRunning, 1, 0) != 0)
        {
            return false;
        }

        lock (_stateLock)
        {
            _isRunning = false;
            _queuedAtUtc = DateTimeOffset.UtcNow;
            _lastErrorMessage = null;
        }

        if (!channel.Writer.TryWrite(true))
        {
            Interlocked.Exchange(ref _queuedOrRunning, 0);
            lock (_stateLock)
            {
                _queuedAtUtc = null;
            }

            return false;
        }

        return true;
    }

    public ServerRefreshJobStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new ServerRefreshJobStatus(
                Volatile.Read(ref _queuedOrRunning) != 0,
                _isRunning,
                _queuedAtUtc,
                _startedAtUtc,
                _completedAtUtc,
                _failedAtUtc,
                _lastErrorMessage);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                lock (_stateLock)
                {
                    _isRunning = true;
                    _startedAtUtc = DateTimeOffset.UtcNow;
                    _lastErrorMessage = null;
                }

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var refreshService = scope.ServiceProvider.GetRequiredService<IServerRefreshService>();
                await refreshService.RefreshAllServersAsync(stoppingToken);

                lock (_stateLock)
                {
                    _completedAtUtc = DateTimeOffset.UtcNow;
                    _lastErrorMessage = null;
                }
            }
            catch (Exception exception)
            {
                if (WorkerExceptionHandling.IsExpectedShutdownException(exception, stoppingToken))
                {
                    return;
                }

                lock (_stateLock)
                {
                    _failedAtUtc = DateTimeOffset.UtcNow;
                    _lastErrorMessage = exception.Message;
                }

                logger.LogError(exception, "Background refresh-all job failed.");
            }
            finally
            {
                lock (_stateLock)
                {
                    _isRunning = false;
                    _queuedAtUtc = null;
                }

                Interlocked.Exchange(ref _queuedOrRunning, 0);
            }
        }
    }
}
