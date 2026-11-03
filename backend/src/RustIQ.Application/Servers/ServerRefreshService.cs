using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed class ServerRefreshService(
    IServerRepository serverRepository,
    IRustServerCatalog serverCatalog,
    IBattleMetricsServerDirectoryClient battleMetricsServerDirectoryClient,
    IBattleMetricsPlayerCountClient battleMetricsPlayerCountClient,
    ServerPollingService serverPollingService,
    int battleMetricsDirectoryPageSize,
    int battleMetricsDirectoryMaxPages,
    TimeSpan offlineThreshold) : IServerRefreshService
{
    private const int PlayerCountRefreshConcurrency = 16;

    public async Task RefreshServersAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        foreach (var server in servers)
        {
            await RefreshServerAsync(server, cancellationToken);
        }
    }

    public async Task RefreshAllServersAsync(
        CancellationToken cancellationToken)
    {
        var configuredServers = await serverCatalog.GetConfiguredServersAsync(cancellationToken);
        if (configuredServers.Count > 0)
        {
            await serverPollingService.EnsureConfiguredServersExistAsync(configuredServers, cancellationToken);
        }

        var servers = await serverRepository.GetAllAsync(cancellationToken);
        await RefreshPlayerCountsAsync(servers, cancellationToken);
    }

    public async Task<int> RefreshBattleMetricsDirectoryServersAsync(
        bool captureSnapshots,
        CancellationToken cancellationToken)
    {
        // The directory is asked first on purpose. When the BattleMetrics API is disabled it answers
        // with nothing, and loading every row in the server table to match against an empty directory
        // was pure waste - this sweep runs every few minutes and the table only grows with discovery.
        IReadOnlyList<BattleMetricsServerSummary> summaries;
        try
        {
            summaries = await battleMetricsServerDirectoryClient.GetRustServerSummariesAsync(
                battleMetricsDirectoryPageSize,
                battleMetricsDirectoryMaxPages,
                cancellationToken);
        }
        catch (BattleMetricsRateLimitException exception)
        {
            summaries = exception.PartialSummaries;
        }

        if (summaries.Count == 0)
        {
            return 0;
        }

        var servers = await serverRepository.GetAllAsync(cancellationToken);
        if (servers.Count == 0)
        {
            return 0;
        }

        var directoryRefreshResults = BuildDirectoryPlayerCounts(servers, summaries);

        await ApplyPlayerCountRefreshesAsync(directoryRefreshResults, captureSnapshots, cancellationToken);

        return directoryRefreshResults.Count;
    }

    public async Task RefreshServerAsync(
        Server server,
        CancellationToken cancellationToken)
    {
        await PollServerAsync(
            server,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            cancellationToken);
    }

    public async Task RefreshCurrentPlayersAsync(
        Server server,
        CancellationToken cancellationToken)
    {
        await PollServerAsync(
            server,
            refreshCurrentPlayers: true,
            refreshMapMetadata: false,
            cancellationToken);
    }

    private async Task PollServerAsync(
        Server server,
        bool refreshCurrentPlayers,
        bool refreshMapMetadata,
        CancellationToken cancellationToken)
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

        await serverPollingService.PollConfiguredServerAsync(
            configuredServer,
            offlineThreshold,
            refreshCurrentPlayers,
            refreshMapMetadata,
            ServerPollSource.RefreshJob,
            cancellationToken);
    }

    private async Task RefreshPlayerCountsAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        if (servers.Count == 0)
        {
            return;
        }

        Dictionary<Guid, PlayerCountRefreshResult> directoryRefreshResults;
        var directoryRateLimited = false;
        try
        {
            directoryRefreshResults = await FetchDirectoryPlayerCountsAsync(servers, cancellationToken);
        }
        catch (BattleMetricsRateLimitException exception)
        {
            directoryRateLimited = true;
            directoryRefreshResults = BuildDirectoryPlayerCounts(servers, exception.PartialSummaries);
        }

        if (directoryRefreshResults.Count > 0)
        {
            await ApplyPlayerCountRefreshesAsync(directoryRefreshResults, captureSnapshots: true, cancellationToken);
        }

        if (directoryRateLimited)
        {
            return;
        }

        var serversNotRefreshedFromDirectory = servers
            .Where(server => !directoryRefreshResults.ContainsKey(server.Id))
            .ToArray();

        if (serversNotRefreshedFromDirectory.Length == 0)
        {
            return;
        }

        var refreshResults = await FetchPlayerCountsAsync(serversNotRefreshedFromDirectory, cancellationToken);
        await ApplyPlayerCountRefreshesAsync(refreshResults, captureSnapshots: true, cancellationToken);
    }

    private async Task ApplyPlayerCountRefreshesAsync(
        IReadOnlyDictionary<Guid, PlayerCountRefreshResult> refreshResults,
        bool captureSnapshots,
        CancellationToken cancellationToken)
    {
        if (refreshResults.Count == 0)
        {
            return;
        }

        var refreshCompletedAtUtc = DateTimeOffset.UtcNow;
        var trackedServers = await serverRepository.GetTrackedByIdsAsync(
            refreshResults.Keys.ToArray(),
            cancellationToken);

        foreach (var trackedServer in trackedServers)
        {
            if (!refreshResults.TryGetValue(trackedServer.Id, out var refreshResult))
            {
                continue;
            }

            ApplyPlayerCountRefresh(
                trackedServer,
                refreshResult,
                refreshCompletedAtUtc,
                offlineThreshold);

            if (captureSnapshots)
            {
                serverRepository.AddSnapshot(CreateSnapshot(
                    trackedServer.Id,
                    refreshResult.PlayerCount,
                    trackedServer.Status,
                    trackedServer.Players,
                    trackedServer.MaxPlayers));
            }
        }

        await serverRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, PlayerCountRefreshResult>> FetchDirectoryPlayerCountsAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        var summaries = await battleMetricsServerDirectoryClient.GetRustServerSummariesAsync(
            battleMetricsDirectoryPageSize,
            battleMetricsDirectoryMaxPages,
            cancellationToken);

        if (summaries.Count == 0)
        {
            return [];
        }

        return BuildDirectoryPlayerCounts(servers, summaries);
    }

    private static Dictionary<Guid, PlayerCountRefreshResult> BuildDirectoryPlayerCounts(
        IReadOnlyList<Server> servers,
        IReadOnlyList<BattleMetricsServerSummary> summaries)
    {
        var summariesByBattleMetricsId = summaries
            .GroupBy(summary => summary.BattleMetricsServerId)
            .ToDictionary(group => group.Key, group => group.First());
        var summariesByEndpoint = summaries
            .GroupBy(summary => ServerEndpointKey(summary.Host, summary.QueryPort))
            .ToDictionary(group => group.Key, group => group.First());
        var results = new Dictionary<Guid, PlayerCountRefreshResult>();

        foreach (var server in servers)
        {
            BattleMetricsServerSummary? summary = null;
            if (server.BattleMetricsServerId is not null)
            {
                summariesByBattleMetricsId.TryGetValue(server.BattleMetricsServerId.Value, out summary);
            }

            summary ??= summariesByEndpoint.GetValueOrDefault(ServerEndpointKey(server.Host, server.QueryPort));
            if (summary is null)
            {
                continue;
            }

            results[server.Id] = new PlayerCountRefreshResult(
                summary.BattleMetricsServerId,
                summary.ServerType,
                new BattleMetricsPlayerCountResult(
                    summary.BattleMetricsServerId,
                    summary.Players,
                    summary.MaxPlayers,
                    summary.QueuePlayers,
                    summary.ServerType,
                    summary.IsOnline,
                    summary.Country,
                    summary.Latitude,
                    summary.Longitude,
                    summary.LastWipeAtUtc,
                    summary.NextWipeAtUtc,
                    summary.CheckedAtUtc));
        }

        return results;
    }

    private async Task<Dictionary<Guid, PlayerCountRefreshResult>> FetchPlayerCountsAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<Guid, PlayerCountRefreshResult>();
        var semaphore = new SemaphoreSlim(PlayerCountRefreshConcurrency);

        try
        {
            await Task.WhenAll(servers.Select(async server =>
            {
                await semaphore.WaitAsync(cancellationToken);

                try
                {
                    var battleMetricsServerId = server.BattleMetricsServerId;
                    if (battleMetricsServerId is null)
                    {
                        battleMetricsServerId = await battleMetricsPlayerCountClient.FindServerIdAsync(
                            server.Host,
                            server.QueryPort,
                            cancellationToken);
                    }

                    if (battleMetricsServerId is null)
                    {
                        return;
                    }

                    var playerCount = await battleMetricsPlayerCountClient.GetPlayerCountAsync(
                        battleMetricsServerId.Value,
                        cancellationToken);

                    if (playerCount is null)
                    {
                        return;
                    }

                    lock (results)
                    {
                        results[server.Id] = new PlayerCountRefreshResult(
                            battleMetricsServerId.Value,
                            playerCount.ServerType == RustServerType.Unknown
                                ? server.ServerType
                                : playerCount.ServerType,
                            playerCount);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }
        finally
        {
            semaphore.Dispose();
        }

        return results;
    }

    private static void ApplyPlayerCountRefresh(
        Server server,
        PlayerCountRefreshResult refreshResult,
        DateTimeOffset refreshCompletedAtUtc,
        TimeSpan offlineThreshold)
    {
        var checkedAtUtc = refreshResult.PlayerCount.CheckedAtUtc;
        var players = Math.Max(0, refreshResult.PlayerCount.Players);
        var maxPlayers = Math.Max(players, Math.Max(0, refreshResult.PlayerCount.MaxPlayers));
        var queuePlayers = NormalizeQueuePlayers(refreshResult.PlayerCount.QueuePlayers);

        server.BattleMetricsServerId = refreshResult.BattleMetricsServerId;
        if (refreshResult.ServerType != RustServerType.Unknown)
        {
            server.ServerType = refreshResult.ServerType;
        }

        server.PlayerCountSource = ServerPlayerCountSource.BattleMetrics;
        server.LastCheckedAtUtc = checkedAtUtc;
        server.LastPlayerCountUpdatedAtUtc = refreshCompletedAtUtc;
        server.UpdatedAtUtc = refreshCompletedAtUtc;
        server.MaxPlayers = maxPlayers;
        server.QueuePlayers = queuePlayers;
        server.Country = string.IsNullOrWhiteSpace(refreshResult.PlayerCount.Country)
            ? server.Country
            : refreshResult.PlayerCount.Country;
        server.Latitude = refreshResult.PlayerCount.Latitude ?? server.Latitude;
        server.Longitude = refreshResult.PlayerCount.Longitude ?? server.Longitude;
        server.LastWipeAtUtc = refreshResult.PlayerCount.LastWipeAtUtc ?? server.LastWipeAtUtc;
        server.NextWipeAtUtc = refreshResult.PlayerCount.NextWipeAtUtc ?? server.NextWipeAtUtc;

        if (refreshResult.PlayerCount.IsOnline)
        {
            server.Players = players;
            server.Status = ServerStatus.Online;
            server.LastSeenAtUtc = checkedAtUtc;
            server.DataQuality = ServerDataQuality.Normal;
            server.LastValidationReason = null;
            return;
        }

        if (server.LastSeenAtUtc is null
            || checkedAtUtc - server.LastSeenAtUtc > offlineThreshold)
        {
            server.Status = ServerStatus.Offline;
            server.Players = 0;
        }

        server.DataQuality = ServerDataQuality.Degraded;
        server.LastValidationReason = "BattleMetricsOffline";
    }

    private static ServerSnapshot CreateSnapshot(
        Guid serverId,
        BattleMetricsPlayerCountResult playerCount,
        ServerStatus status,
        int players,
        int maxPlayers)
    {
        return new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            CheckedAtUtc = playerCount.CheckedAtUtc,
            Players = players,
            MaxPlayers = maxPlayers,
            IsOnline = status == ServerStatus.Online,
            PingMilliseconds = null,
            PollSource = ServerPollSource.BattleMetricsDirectory
        };
    }

    private sealed record PlayerCountRefreshResult(
        long BattleMetricsServerId,
        RustServerType ServerType,
        BattleMetricsPlayerCountResult PlayerCount);

    private static int? NormalizeQueuePlayers(int? queuePlayers)
    {
        return queuePlayers is null ? null : Math.Max(0, queuePlayers.Value);
    }

    private static string ServerEndpointKey(string host, int queryPort)
    {
        return $"{host.Trim().ToLowerInvariant()}:{queryPort}";
    }
}
