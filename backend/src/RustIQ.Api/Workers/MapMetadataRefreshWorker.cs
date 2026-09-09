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

        if (options.Value.RunOnStartup)
        {
            var startupCutoffUtc = DateTimeOffset.UtcNow.AddHours(-options.Value.StartupStaleAfterHours);
            await RunSweepAsync(startupCutoffUtc, "startup", stoppingToken);
        }

        var nextRunUtc = MapMetadataRefreshSchedule.GetNextRunUtc(
            DateTimeOffset.UtcNow,
            timeZone,
            options.Value.LocalHours);

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
                DateTimeOffset.UtcNow,
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
                    options.Value.MaxServersPerRun,
                    cancellationToken);
            }

            logger.LogInformation(
                "Map metadata refresh sweep started. Trigger: {Trigger}. TargetServers: {TargetServers}. FetchedBeforeUtc: {FetchedBeforeUtc}.",
                trigger,
                targets.Count,
                fetchedBeforeUtc);

            var updatedCount = 0;
            var failedCount = 0;
            var batches = targets.Chunk(options.Value.BatchSize);
            var processedCount = 0;

            foreach (var batch in batches)
            {
                var refreshResults = await Task.WhenAll(batch.Select((server, serverIndex) =>
                    RefreshServerAsync(server, serverIndex, cancellationToken)));
                updatedCount += refreshResults.Count(updated => updated);
                failedCount += refreshResults.Count(updated => !updated);
                processedCount += batch.Length;

                if (processedCount == targets.Count || processedCount % 250 == 0)
                {
                    logger.LogInformation(
                        "Map metadata refresh sweep progress. Trigger: {Trigger}. ProcessedServers: {ProcessedServers}/{TargetServers}. UpdatedServers: {UpdatedServers}. FailedServers: {FailedServers}.",
                        trigger,
                        processedCount,
                        targets.Count,
                        updatedCount,
                        failedCount);
                }

                if (processedCount < targets.Count
                    && options.Value.DelayBetweenBatchesSeconds > 0)
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

    private async Task<bool> RefreshServerAsync(
        Server server,
        int serverIndex,
        CancellationToken cancellationToken)
    {
        if (serverIndex > 0 && options.Value.DelayBetweenServersMilliseconds > 0)
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(
                    (long)serverIndex * options.Value.DelayBetweenServersMilliseconds),
                cancellationToken);
        }

        try
        {
            // Each concurrent refresh needs its own scoped DbContext.
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();
            return await pollingService.RefreshMapMetadataAsync(
                ToConfiguredServer(server),
                cancellationToken);
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogWarning(
                exception,
                "Map metadata refresh failed for server {ServerId} ({Host}:{QueryPort}).",
                server.Id,
                server.Host,
                server.QueryPort);
            return false;
        }
    }

    private static ConfiguredRustServer ToConfiguredServer(Server server)
    {
        return new ConfiguredRustServer
        {
            BattleMetricsServerId = server.BattleMetricsServerId,
            Name = server.Name,
            Map = server.Map,
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
