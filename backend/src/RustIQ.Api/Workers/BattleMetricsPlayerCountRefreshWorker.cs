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
    private DateTimeOffset? _lastDirectoryRefreshUtc;
    private DateTimeOffset? _lastFastLaneRefreshUtc;
    private DateTimeOffset? _lastNormalOnlineLaneRefreshUtc;
    private DateTimeOffset? _lastSlowOfflineLaneRefreshUtc;
    private DateTimeOffset? _lastVeryStaleOfflineLaneRefreshUtc;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("BattleMetrics player count refresh worker is disabled.");
            return;
        }

        var tickInterval = TimeSpan.FromSeconds(options.Value.WorkerTickSeconds);

        logger.LogInformation(
            "BattleMetrics refresh worker started. Tick: {TickInterval}. Directory: {DirectoryInterval}. FastLane: {FastLaneInterval}. NormalOnlineLane: {NormalOnlineLaneInterval}. SlowOfflineLane: {SlowOfflineLaneInterval}. VeryStaleOfflineLane: {VeryStaleOfflineLaneInterval}.",
            tickInterval,
            TimeSpan.FromMinutes(options.Value.DirectoryRefreshIntervalMinutes),
            TimeSpan.FromSeconds(options.Value.FastLaneRefreshIntervalSeconds),
            TimeSpan.FromMinutes(options.Value.NormalOnlineRefreshIntervalMinutes),
            TimeSpan.FromMinutes(options.Value.SlowOfflineRefreshIntervalMinutes),
            TimeSpan.FromMinutes(options.Value.VeryStaleOfflineRefreshIntervalMinutes));

        await RefreshDueWorkAsync(stoppingToken);

        using var timer = new PeriodicTimer(tickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshDueWorkAsync(stoppingToken);
        }
    }

    private async Task RefreshDueWorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IServerRepository>();
            var refreshService = scope.ServiceProvider.GetRequiredService<IServerRefreshService>();

            var now = DateTimeOffset.UtcNow;
            if (IsDue(_lastDirectoryRefreshUtc, now, TimeSpan.FromMinutes(options.Value.DirectoryRefreshIntervalMinutes)))
            {
                try
                {
                    await RefreshDirectoryAsync(refreshService, now, cancellationToken);
                    _lastDirectoryRefreshUtc = now;
                }
                catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
                {
                    logger.LogError(exception, "BattleMetrics directory population sweep failed.");
                }
            }

            var refreshedServerIds = new HashSet<Guid>();
            await RunLaneRefreshAsync(
                ServerRefreshLane.Fast,
                _lastFastLaneRefreshUtc,
                value => _lastFastLaneRefreshUtc = value,
                TimeSpan.FromSeconds(options.Value.FastLaneRefreshIntervalSeconds),
                options.Value.FastLaneBatchSize,
                repository,
                refreshedServerIds,
                now,
                cancellationToken);

            await RunLaneRefreshAsync(
                ServerRefreshLane.NormalOnline,
                _lastNormalOnlineLaneRefreshUtc,
                value => _lastNormalOnlineLaneRefreshUtc = value,
                TimeSpan.FromMinutes(options.Value.NormalOnlineRefreshIntervalMinutes),
                options.Value.NormalOnlineLaneBatchSize,
                repository,
                refreshedServerIds,
                now,
                cancellationToken);

            await RunLaneRefreshAsync(
                ServerRefreshLane.SlowOffline,
                _lastSlowOfflineLaneRefreshUtc,
                value => _lastSlowOfflineLaneRefreshUtc = value,
                TimeSpan.FromMinutes(options.Value.SlowOfflineRefreshIntervalMinutes),
                options.Value.SlowOfflineLaneBatchSize,
                repository,
                refreshedServerIds,
                now,
                cancellationToken);

            await RunLaneRefreshAsync(
                ServerRefreshLane.VeryStaleOffline,
                _lastVeryStaleOfflineLaneRefreshUtc,
                value => _lastVeryStaleOfflineLaneRefreshUtc = value,
                TimeSpan.FromMinutes(options.Value.VeryStaleOfflineRefreshIntervalMinutes),
                options.Value.VeryStaleOfflineLaneBatchSize,
                repository,
                refreshedServerIds,
                now,
                cancellationToken);
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "BattleMetrics refresh worker failed.");
        }
    }

    private async Task RefreshDirectoryAsync(
        IServerRefreshService refreshService,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
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
    }

    private async Task RunLaneRefreshAsync(
        ServerRefreshLane refreshLane,
        DateTimeOffset? lastRefreshUtc,
        Action<DateTimeOffset> setLastRefreshUtc,
        TimeSpan interval,
        int batchSize,
        IServerRepository repository,
        HashSet<Guid> refreshedServerIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            await RefreshLaneIfDueAsync(
                refreshLane,
                lastRefreshUtc,
                setLastRefreshUtc,
                interval,
                batchSize,
                repository,
                refreshedServerIds,
                now,
                cancellationToken);
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogError(exception, "BattleMetrics {RefreshLane} lane refresh failed.", refreshLane);
        }
    }

    private async Task RefreshLaneIfDueAsync(
        ServerRefreshLane refreshLane,
        DateTimeOffset? lastRefreshUtc,
        Action<DateTimeOffset> setLastRefreshUtc,
        TimeSpan interval,
        int batchSize,
        IServerRepository repository,
        HashSet<Guid> refreshedServerIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!IsDue(lastRefreshUtc, now, interval))
        {
            return;
        }

        var dueBeforeUtc = now.Subtract(interval);
        var servers = await repository.GetServersForRefreshLaneAsync(
            refreshLane,
            dueBeforeUtc,
            now,
            batchSize,
            options.Value.PopularRankThreshold,
            options.Value.PopularPlayerThreshold,
            options.Value.PopularPeakPlayerThreshold,
            TimeSpan.FromMinutes(options.Value.RecentPeakWindowMinutes),
            TimeSpan.FromMinutes(options.Value.PopularOfflineGraceMinutes),
            TimeSpan.FromMinutes(options.Value.SlowOfflineAfterMinutes),
            TimeSpan.FromHours(options.Value.VeryStaleOfflineAfterHours),
            cancellationToken);

        // Claimed up front on this thread, so the set shared between lanes never needs a lock.
        var dueServers = servers
            .Where(server => refreshedServerIds.Add(server.Id))
            .ToArray();

        var refreshedCount = await PollLaneServersAsync(refreshLane, dueServers, cancellationToken);

        setLastRefreshUtc(now);

        if (refreshedCount > 0)
        {
            logger.LogInformation(
                "BattleMetrics {RefreshLane} refresh completed. RefreshedServers: {RefreshedServers}.",
                refreshLane,
                refreshedCount);
        }
    }

    private async Task<int> PollLaneServersAsync(
        ServerRefreshLane refreshLane,
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        if (servers.Count == 0)
        {
            return 0;
        }

        var offlineThreshold = TimeSpan.FromSeconds(queryOptions.Value.OfflineThresholdSeconds);
        using var semaphore = new SemaphoreSlim(Math.Max(1, options.Value.RefreshConcurrency));
        var refreshedCount = 0;

        await Task.WhenAll(servers.Select(async server =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // A scope per server: polling writes through EF, and a DbContext cannot be shared across
                // concurrent work. It also keeps the change tracker down to one server per poll instead
                // of accumulating every server and snapshot touched during the tick.
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();

                var result = await pollingService.PollConfiguredServerAsync(
                    ToConfiguredServer(server),
                    offlineThreshold,
                    options.Value.RefreshCurrentPlayers,
                    options.Value.RefreshMapMetadata,
                    ToPollSource(refreshLane),
                    cancellationToken);
                var updateMessage = serverUpdateMessageFactory.Create(result, DateTimeOffset.UtcNow);

                await hubContext.Clients
                    .Group(ServerUpdatesHub.GetServerGroupName(result.Id))
                    .SendAsync("ServerUpdated", updateMessage, cancellationToken);

                Interlocked.Increment(ref refreshedCount);
            }
            catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                logger.LogError(
                    exception,
                    "BattleMetrics {RefreshLane} refresh failed for server {Host}:{QueryPort}.",
                    refreshLane,
                    server.Host,
                    server.QueryPort);
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return refreshedCount;
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

    private static ServerPollSource ToPollSource(ServerRefreshLane refreshLane)
    {
        return refreshLane switch
        {
            ServerRefreshLane.Fast => ServerPollSource.FastLane,
            ServerRefreshLane.NormalOnline => ServerPollSource.NormalOnlineLane,
            ServerRefreshLane.SlowOffline => ServerPollSource.SlowOfflineLane,
            ServerRefreshLane.VeryStaleOffline => ServerPollSource.VeryStaleOfflineLane,
            _ => ServerPollSource.Unknown
        };
    }

    private static bool IsDue(DateTimeOffset? lastRefreshUtc, DateTimeOffset now, TimeSpan interval)
    {
        return lastRefreshUtc is null || now - lastRefreshUtc.Value >= interval;
    }
}
