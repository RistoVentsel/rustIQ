using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RustIQ.Api.Realtime;
using RustIQ.Application.Realtime;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;

namespace RustIQ.Api.Workers;

public sealed class WatchedServerRefreshWorker(
    IServiceScopeFactory serviceScopeFactory,
    IWatchedServerRegistry watchedServerRegistry,
    IHubContext<ServerUpdatesHub> hubContext,
    ServerUpdateMessageFactory serverUpdateMessageFactory,
    IOptions<RustServerQueryOptions> queryOptions,
    ILogger<WatchedServerRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private const int RefreshConcurrency = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Watched server refresh worker started. RefreshInterval: {RefreshInterval}. RefreshConcurrency: {RefreshConcurrency}.",
            RefreshInterval,
            RefreshConcurrency);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshWatchedServersAsync(stoppingToken);
        }
    }

    private async Task RefreshWatchedServersAsync(CancellationToken cancellationToken)
    {
        var serverIds = watchedServerRegistry.GetWatchedServerIds();
        if (serverIds.Count == 0)
        {
            return;
        }

        using var semaphore = new SemaphoreSlim(RefreshConcurrency);
        await Task.WhenAll(serverIds.Select(async serverId =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                await RefreshWatchedServerAsync(serverId, cancellationToken);
            }
            catch (Exception exception)
            {
                if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
                {
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Watched server refresh failed for server {ServerId}.",
                    serverId);
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    private async Task RefreshWatchedServerAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();

        var server = await repository.GetByIdWithMapMetadataAsync(serverId, cancellationToken);
        if (server is null)
        {
            return;
        }

        var configuredServer = new ConfiguredRustServer
        {
            BattleMetricsServerId = server.BattleMetricsServerId,
            Name = server.Name,
            Address = server.Address,
            Host = server.Host,
            GamePort = server.GamePort,
            QueryPort = server.QueryPort,
            Country = server.Country,
            Latitude = server.Latitude,
            Longitude = server.Longitude,
            LastWipeAtUtc = server.LastWipeAtUtc,
            NextWipeAtUtc = server.NextWipeAtUtc,
            QueuePlayers = server.QueuePlayers,
            ServerType = server.ServerType,
            DiscoverySource = server.DiscoverySource,
            SummarySource = server.SummarySource,
            PlayerCountSource = server.PlayerCountSource,
            PlayerListSource = server.PlayerListSource
        };
        var result = await pollingService.PollConfiguredServerAsync(
            configuredServer,
            TimeSpan.FromSeconds(queryOptions.Value.WatchedOfflineThresholdSeconds),
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            cancellationToken);
        var updateMessage = serverUpdateMessageFactory.Create(result, DateTimeOffset.UtcNow);

        await hubContext.Clients
            .Group(ServerUpdatesHub.GetServerGroupName(result.Id))
            .SendAsync("ServerUpdated", updateMessage, cancellationToken);
    }
}
