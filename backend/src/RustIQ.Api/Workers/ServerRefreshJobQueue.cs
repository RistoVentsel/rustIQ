using System.Threading.Channels;
using RustIQ.Application.Servers;

namespace RustIQ.Api.Workers;

public sealed class ServerRefreshJobQueue(
    Channel<bool> channel,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ServerRefreshJobQueue> logger) : BackgroundService, IServerRefreshJobQueue
{
    private int _queuedOrRunning;

    public bool TryQueueRefreshAll()
    {
        if (Interlocked.CompareExchange(ref _queuedOrRunning, 1, 0) != 0)
        {
            return false;
        }

        if (!channel.Writer.TryWrite(true))
        {
            Interlocked.Exchange(ref _queuedOrRunning, 0);
            return false;
        }

        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var refreshService = scope.ServiceProvider.GetRequiredService<IServerRefreshService>();
                await refreshService.RefreshAllServersAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                if (WorkerExceptionHandling.IsExpectedShutdownException(exception, stoppingToken))
                {
                    return;
                }

                logger.LogError(exception, "Background refresh-all job failed.");
            }
            finally
            {
                Interlocked.Exchange(ref _queuedOrRunning, 0);
            }
        }
    }
}
