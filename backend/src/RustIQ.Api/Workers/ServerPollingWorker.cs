using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RustIQ.Application.Realtime;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Api.Realtime;

namespace RustIQ.Api.Workers;

public sealed class ServerPollingWorker(
    IServiceScopeFactory serviceScopeFactory,
    IHubContext<ServerUpdatesHub> hubContext,
    IOptions<RustServerQueryOptions> options,
    IRustServerCatalog serverCatalog,
    ServerUpdateMessageFactory serverUpdateMessageFactory,
    ILogger<ServerPollingWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan ServerInfoInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CurrentPlayersInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MapMetadataInterval = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private DateTimeOffset _lastCurrentPlayersRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMapMetadataRefreshUtc = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Server polling worker started. Server info interval: {ServerInfoInterval}. Current players interval: {CurrentPlayersInterval}. Map metadata interval: {MapMetadataInterval}.",
            ServerInfoInterval,
            CurrentPlayersInterval,
            MapMetadataInterval);

        await PollOnceAsync(
            refreshCurrentPlayers: true,
            refreshMapMetadata: true,
            stoppingToken);

        using var timer = new PeriodicTimer(ServerInfoInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            var refreshCurrentPlayers = now - _lastCurrentPlayersRefreshUtc >= CurrentPlayersInterval;
            var refreshMapMetadata = now - _lastMapMetadataRefreshUtc >= MapMetadataInterval;

            await PollOnceAsync(refreshCurrentPlayers, refreshMapMetadata, stoppingToken);
        }
    }

    private async Task PollOnceAsync(
        bool refreshCurrentPlayers,
        bool refreshMapMetadata,
        CancellationToken cancellationToken)
    {
        if (!await _pollLock.WaitAsync(0, cancellationToken))
        {
            logger.LogWarning("Skipping polling cycle because the previous cycle is still running.");
            return;
        }

        try
        {
            var configuredServers = await serverCatalog.GetConfiguredServersAsync(cancellationToken);
            var offlineThreshold = TimeSpan.FromSeconds(options.Value.OfflineThresholdSeconds);

            if (configuredServers.Count == 0)
            {
                return;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();

            await pollingService.EnsureConfiguredServersExistAsync(configuredServers, cancellationToken);

            foreach (var configuredServer in configuredServers)
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    var result = await pollingService.PollConfiguredServerAsync(
                        configuredServer,
                        offlineThreshold,
                        refreshCurrentPlayers,
                        refreshMapMetadata,
                        cancellationToken);
                    var updateMessage = serverUpdateMessageFactory.Create(result, DateTimeOffset.UtcNow);

                    await hubContext.Clients
                        .Group(ServerUpdatesHub.GetServerGroupName(result.Id))
                        .SendAsync("ServerUpdated", updateMessage, cancellationToken);

                    stopwatch.Stop();

                    logger.LogInformation(
                        "Polled Rust server {Host}:{QueryPort}. Status: {Status}. Players: {Players}/{MaxPlayers}. DurationMs: {DurationMs}. Refreshed current players: {RefreshedCurrentPlayers}. Refreshed map metadata: {RefreshedMapMetadata}.",
                        result.Host,
                        result.QueryPort,
                        result.Status,
                        result.Players,
                        result.MaxPlayers,
                        stopwatch.ElapsedMilliseconds,
                        refreshCurrentPlayers,
                        refreshMapMetadata);
                }
                catch (Exception exception)
                {
                    if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
                    {
                        return;
                    }

                    stopwatch.Stop();

                    logger.LogError(
                        exception,
                        "Polling failed for Rust server {Host}:{QueryPort} after {DurationMs} ms. ErrorSummary: {ErrorSummary}",
                        configuredServer.Host,
                        configuredServer.QueryPort,
                        stopwatch.ElapsedMilliseconds,
                        exception.Message);
                }
            }

            var now = DateTimeOffset.UtcNow;
            if (refreshCurrentPlayers)
            {
                _lastCurrentPlayersRefreshUtc = now;
            }

            if (refreshMapMetadata)
            {
                _lastMapMetadataRefreshUtc = now;
            }
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "Rust server polling cycle failed.");
        }
        finally
        {
            _pollLock.Release();
        }
    }
}
