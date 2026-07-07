using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed class ServerRefreshService(
    IServerRepository serverRepository,
    IRustServerCatalog serverCatalog,
    IBattleMetricsPlayerCountClient battleMetricsPlayerCountClient,
    ServerPollingService serverPollingService,
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

    public async Task RefreshServerAsync(
        Server server,
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
            DiscoverySource = server.DiscoverySource,
            SummarySource = server.SummarySource,
            PlayerCountSource = server.PlayerCountSource,
            PlayerListSource = server.PlayerListSource
        };

        await serverPollingService.PollConfiguredServerAsync(
            configuredServer,
            offlineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
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

        var refreshResults = await FetchPlayerCountsAsync(servers, cancellationToken);
        if (refreshResults.Count == 0)
        {
            return;
        }

        var trackedServers = await serverRepository.GetTrackedByIdsAsync(
            refreshResults.Keys.ToArray(),
            cancellationToken);

        foreach (var trackedServer in trackedServers)
        {
            if (!refreshResults.TryGetValue(trackedServer.Id, out var refreshResult))
            {
                continue;
            }

            ApplyPlayerCountRefresh(trackedServer, refreshResult, offlineThreshold);
        }

        await serverRepository.SaveChangesAsync(cancellationToken);
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
                    if (battleMetricsServerId is null
                        && server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics)
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
        TimeSpan offlineThreshold)
    {
        var checkedAtUtc = refreshResult.PlayerCount.CheckedAtUtc;
        var players = Math.Max(0, refreshResult.PlayerCount.Players);
        var maxPlayers = Math.Max(players, Math.Max(0, refreshResult.PlayerCount.MaxPlayers));

        server.BattleMetricsServerId = refreshResult.BattleMetricsServerId;
        server.PlayerCountSource = ServerPlayerCountSource.BattleMetrics;
        server.LastCheckedAtUtc = checkedAtUtc;
        server.LastPlayerCountUpdatedAtUtc = checkedAtUtc;
        server.UpdatedAtUtc = checkedAtUtc;
        server.MaxPlayers = maxPlayers;

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

    private sealed record PlayerCountRefreshResult(
        long BattleMetricsServerId,
        BattleMetricsPlayerCountResult PlayerCount);
}
