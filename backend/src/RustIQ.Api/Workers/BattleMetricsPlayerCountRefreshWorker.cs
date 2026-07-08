using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RustIQ.Api.Realtime;
using RustIQ.Application.Realtime;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Api.Workers;

public sealed class BattleMetricsPlayerCountRefreshWorker(
    IServiceScopeFactory serviceScopeFactory,
    IHubContext<ServerUpdatesHub> hubContext,
    ServerUpdateMessageFactory serverUpdateMessageFactory,
    IOptions<BattleMetricsSummaryRefreshOptions> options,
    IOptions<RustServerQueryOptions> queryOptions,
    ILogger<BattleMetricsPlayerCountRefreshWorker> logger)
    : BackgroundService
{
    private DateTimeOffset? _lastSnapshotRefreshUtc;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("BattleMetrics player count refresh worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.RefreshIntervalSeconds);

        logger.LogInformation(
            "BattleMetrics player count refresh worker started. BatchSize: {BatchSize}. RefreshInterval: {RefreshInterval}. StaleAfterSeconds: {StaleAfterSeconds}.",
            options.Value.BatchSize,
            interval,
            options.Value.StaleAfterSeconds);

        await RefreshOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshOnceAsync(stoppingToken);
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IServerRepository>();
            var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();
            var refreshService = scope.ServiceProvider.GetRequiredService<IServerRefreshService>();

            var now = DateTimeOffset.UtcNow;
            var snapshotInterval = TimeSpan.FromMinutes(options.Value.SnapshotIntervalMinutes);
            var captureSnapshots = _lastSnapshotRefreshUtc is null
                || now - _lastSnapshotRefreshUtc.Value >= snapshotInterval;
            var directoryRefreshedServers = await refreshService.RefreshBattleMetricsDirectoryServersAsync(
                captureSnapshots,
                cancellationToken);
            if (captureSnapshots && directoryRefreshedServers > 0)
            {
                _lastSnapshotRefreshUtc = now;
            }

            if (directoryRefreshedServers > 0)
            {
                logger.LogInformation(
                    "BattleMetrics directory population sweep completed. RefreshedServers: {RefreshedServers}. CapturedSnapshots: {CapturedSnapshots}.",
                    directoryRefreshedServers,
                    captureSnapshots);
            }

            var staleBeforeUtc = now.AddSeconds(-options.Value.StaleAfterSeconds);
            var servers = await repository.GetServersForPlayerCountRefreshAsync(
                ServerPlayerCountSource.BattleMetrics,
                staleBeforeUtc,
                options.Value.BatchSize,
                cancellationToken);

            foreach (var server in servers)
            {
                var configuredServer = new ConfiguredRustServer
                {
                    BattleMetricsServerId = server.BattleMetricsServerId,
                    Name = server.Name,
                    Address = server.Address,
                    Host = server.Host,
                    GamePort = server.GamePort,
                    QueryPort = server.QueryPort,
                    Country = server.Country,
                    DiscoverySource = server.DiscoverySource,
                    SummarySource = server.SummarySource,
                    PlayerCountSource = server.PlayerCountSource,
                    PlayerListSource = server.PlayerListSource
                };

                var result = await pollingService.PollConfiguredServerAsync(
                    configuredServer,
                    TimeSpan.FromSeconds(queryOptions.Value.OfflineThresholdSeconds),
                    options.Value.RefreshCurrentPlayers,
                    options.Value.RefreshMapMetadata,
                    cancellationToken);
                var updateMessage = serverUpdateMessageFactory.Create(result, DateTimeOffset.UtcNow);

                await hubContext.Clients
                    .Group(ServerUpdatesHub.GetServerGroupName(result.Id))
                    .SendAsync("ServerUpdated", updateMessage, cancellationToken);
            }

            if (servers.Count > 0)
            {
                logger.LogInformation(
                    "BattleMetrics player count refresh completed. RefreshedServers: {RefreshedServers}.",
                    servers.Count);
            }
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "BattleMetrics player count refresh failed.");
        }
    }
}
