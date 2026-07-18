using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Api.Workers;

public sealed class MapMetadataRefreshWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<MapMetadataRefreshOptions> options,
    ILogger<MapMetadataRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Scheduled map metadata refresh worker is disabled.");
            return;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
        logger.LogInformation(
            "Scheduled map metadata refresh worker started. TimeZone: {TimeZoneId}. LocalHours: {LocalHours}. BatchSize: {BatchSize}. DelayBetweenServersMilliseconds: {DelayBetweenServersMilliseconds}. DelayBetweenBatchesSeconds: {DelayBetweenBatchesSeconds}.",
            timeZone.Id,
            string.Join(",", options.Value.LocalHours.Order()),
            options.Value.BatchSize,
            options.Value.DelayBetweenServersMilliseconds,
            options.Value.DelayBetweenBatchesSeconds);

        var nextRunUtc = MapMetadataRefreshSchedule.GetNextRunUtc(
            DateTimeOffset.UtcNow,
            timeZone,
            options.Value.LocalHours);

        if (options.Value.RunOnStartup)
        {
            var startupCutoffUtc = DateTimeOffset.UtcNow.AddHours(-options.Value.StartupStaleAfterHours);
            await RunSweepAsync(startupCutoffUtc, "startup", stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nextRunLocal = TimeZoneInfo.ConvertTime(nextRunUtc, timeZone);

            logger.LogInformation(
                "Next map metadata refresh sweep is scheduled for {NextRunLocal} ({NextRunUtc} UTC).",
                nextRunLocal,
                nextRunUtc);

            if (nextRunUtc > nowUtc)
            {
                await Task.Delay(nextRunUtc - nowUtc, stoppingToken);
            }

            await RunSweepAsync(nextRunUtc, "scheduled", stoppingToken);
            nextRunUtc = MapMetadataRefreshSchedule.GetNextRunUtc(
                nextRunUtc,
                timeZone,
                options.Value.LocalHours);
        }
    }

    private async Task RunSweepAsync(
        DateTimeOffset fetchedBeforeUtc,
        string trigger,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<Server> targets;
            await using (var targetScope = serviceScopeFactory.CreateAsyncScope())
            {
                var repository = targetScope.ServiceProvider.GetRequiredService<IServerRepository>();
                targets = await repository.GetServersForMapMetadataRefreshAsync(
                    fetchedBeforeUtc,
                    cancellationToken);
            }

            logger.LogInformation(
                "Map metadata refresh sweep started. Trigger: {Trigger}. TargetServers: {TargetServers}. FetchedBeforeUtc: {FetchedBeforeUtc}.",
                trigger,
                targets.Count,
                fetchedBeforeUtc);

            var updatedCount = 0;
            var failedCount = 0;
            var batches = targets.Chunk(options.Value.BatchSize).ToArray();

            for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
            {
                await using var batchScope = serviceScopeFactory.CreateAsyncScope();
                var pollingService = batchScope.ServiceProvider.GetRequiredService<ServerPollingService>();

                var batch = batches[batchIndex];
                for (var serverIndex = 0; serverIndex < batch.Length; serverIndex++)
                {
                    var server = batch[serverIndex];
                    try
                    {
                        var updated = await pollingService.RefreshMapMetadataAsync(
                            ToConfiguredServer(server),
                            cancellationToken);
                        if (updated)
                        {
                            updatedCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
                    {
                        failedCount++;
                        logger.LogWarning(
                            exception,
                            "Map metadata refresh failed for server {ServerId} ({Host}:{QueryPort}).",
                            server.Id,
                            server.Host,
                            server.QueryPort);
                    }

                    if (serverIndex < batch.Length - 1
                        && options.Value.DelayBetweenServersMilliseconds > 0)
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(options.Value.DelayBetweenServersMilliseconds),
                            cancellationToken);
                    }
                }

                if (batchIndex < batches.Length - 1)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(options.Value.DelayBetweenBatchesSeconds),
                        cancellationToken);
                }
            }

            logger.LogInformation(
                "Map metadata refresh sweep completed. Trigger: {Trigger}. TargetServers: {TargetServers}. UpdatedServers: {UpdatedServers}. FailedServers: {FailedServers}.",
                trigger,
                targets.Count,
                updatedCount,
                failedCount);
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "Map metadata refresh sweep failed. Trigger: {Trigger}.", trigger);
        }
    }

    private static ConfiguredRustServer ToConfiguredServer(Server server)
    {
        return new ConfiguredRustServer
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
            DiscoverySource = server.DiscoverySource,
            SummarySource = server.SummarySource,
            PlayerCountSource = server.PlayerCountSource,
            PlayerListSource = server.PlayerListSource,
            ServerType = server.ServerType
        };
    }
}
