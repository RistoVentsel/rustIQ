using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;

namespace RustIQ.Api.Workers;

public sealed class SteamDiscoveryWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<SteamDiscoveryOptions> options,
    ISteamServerDiscoveryClient discoveryClient,
    IOptions<BattleMetricsDiscoveryOptions> battleMetricsDiscoveryOptions,
    IBattleMetricsServerDirectoryClient battleMetricsDirectoryClient,
    IOptions<RustServerQueryOptions> queryOptions,
    ILogger<SteamDiscoveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Steam discovery worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(options.Value.RefreshIntervalMinutes);

        logger.LogInformation(
            "Steam discovery worker started. Host: {Host}. Port: {Port}. MaxServersPerRefresh: {MaxServersPerRefresh}. RefreshInterval: {RefreshInterval}.",
            options.Value.Host,
            options.Value.Port,
            options.Value.MaxServersPerRefresh,
            interval);

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
            IReadOnlyList<ConfiguredRustServer> steamDiscoveredServers;
            try
            {
                steamDiscoveredServers = await discoveryClient.DiscoverServersAsync(
                    options.Value.MaxServersPerRefresh,
                    cancellationToken);
            }
            catch (Exception exception) when (options.Value.AllowBattleMetricsFallback)
            {
                logger.LogWarning(
                    exception,
                    "Steam discovery failed. Falling back to BattleMetrics directory discovery for this refresh.");

                steamDiscoveredServers = [];
            }

            IReadOnlyList<ConfiguredRustServer> battleMetricsDiscoveredServers;
            try
            {
                battleMetricsDiscoveredServers = await DiscoverViaBattleMetricsAsync(cancellationToken);
            }
            catch (Exception exception) when (steamDiscoveredServers.Count > 0)
            {
                logger.LogWarning(
                    exception,
                    "BattleMetrics directory discovery failed. Continuing with Steam discovery results for this refresh.");

                battleMetricsDiscoveredServers = [];
            }

            if (steamDiscoveredServers.Count == 0 && battleMetricsDiscoveredServers.Count == 0)
            {
                if (!options.Value.AllowBattleMetricsFallback)
                {
                    logger.LogWarning("Steam discovery returned no Rust servers.");
                    return;
                }

                logger.LogWarning("Steam and BattleMetrics discovery returned no Rust servers.");
                return;
            }

            var discoveredServers = MergeDiscoveredServers(steamDiscoveredServers, battleMetricsDiscoveredServers);
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();
            var createdServers = await pollingService.EnsureConfiguredServersExistAsync(
                discoveredServers,
                cancellationToken);

            // Only servers that were just created need a poll from here: they have no name and no
            // status yet. Everything already tracked is refreshed by the refresh lanes, so re-polling
            // the whole discovery batch every cycle would only duplicate that work - and, since these
            // polls are sequential, a large batch would take longer than the refresh interval itself.
            var polledServers = await PollNewServersAsync(pollingService, createdServers, cancellationToken);

            logger.LogInformation(
                "Server discovery refresh completed. SteamServers: {SteamServers}. BattleMetricsServers: {BattleMetricsServers}. ImportedServers: {ImportedServers}. NewServers: {NewServers}. PolledServers: {PolledServers}.",
                steamDiscoveredServers.Count,
                battleMetricsDiscoveredServers.Count,
                discoveredServers.Count,
                createdServers.Count,
                polledServers);
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "Steam discovery refresh failed.");
        }
    }

    private async Task<int> PollNewServersAsync(
        ServerPollingService pollingService,
        IReadOnlyList<ConfiguredRustServer> createdServers,
        CancellationToken cancellationToken)
    {
        if (createdServers.Count == 0)
        {
            return 0;
        }

        var offlineThreshold = TimeSpan.FromSeconds(queryOptions.Value.OfflineThresholdSeconds);
        var maxPolls = Math.Max(0, options.Value.MaxNewServerPollsPerRefresh);
        var polledServers = 0;

        foreach (var server in createdServers.Take(maxPolls))
        {
            try
            {
                await pollingService.PollConfiguredServerAsync(
                    server,
                    offlineThreshold,
                    refreshCurrentPlayers: false,
                    refreshMapMetadata: false,
                    cancellationToken);
                polledServers++;
            }
            catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                logger.LogWarning(
                    exception,
                    "First poll failed for newly discovered server {Host}:{QueryPort}.",
                    server.Host,
                    server.QueryPort);
            }
        }

        if (createdServers.Count > maxPolls)
        {
            logger.LogInformation(
                "Deferred the first poll of {DeferredServers} newly discovered servers to the refresh lanes.",
                createdServers.Count - maxPolls);
        }

        return polledServers;
    }

    private async Task<IReadOnlyList<ConfiguredRustServer>> DiscoverViaBattleMetricsAsync(CancellationToken cancellationToken)
    {
        if (!battleMetricsDiscoveryOptions.Value.Enabled)
        {
            return [];
        }

        var discoveredServers = await battleMetricsDirectoryClient.GetRustServersAsync(
            battleMetricsDiscoveryOptions.Value.PageSize,
            battleMetricsDiscoveryOptions.Value.MaxPages,
            cancellationToken);

        return discoveredServers;
    }

    private static IReadOnlyList<ConfiguredRustServer> MergeDiscoveredServers(
        IReadOnlyList<ConfiguredRustServer> steamServers,
        IReadOnlyList<ConfiguredRustServer> battleMetricsServers)
    {
        var serversByKey = new Dictionary<string, ConfiguredRustServer>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in steamServers)
        {
            serversByKey[ServerKey(server)] = server;
        }

        foreach (var server in battleMetricsServers)
        {
            serversByKey[ServerKey(server)] = server;
        }

        return serversByKey.Values.ToArray();
    }

    private static string ServerKey(ConfiguredRustServer server)
    {
        return server.BattleMetricsServerId is not null
            ? $"bm:{server.BattleMetricsServerId.Value}"
            : $"endpoint:{server.Host.Trim().ToLowerInvariant()}:{server.QueryPort}";
    }
}
