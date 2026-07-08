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
            await pollingService.EnsureConfiguredServersExistAsync(discoveredServers, cancellationToken);

            var offlineThreshold = TimeSpan.FromSeconds(queryOptions.Value.OfflineThresholdSeconds);
            foreach (var server in steamDiscoveredServers)
            {
                await pollingService.PollConfiguredServerAsync(
                    server,
                    offlineThreshold,
                    refreshCurrentPlayers: false,
                    refreshMapMetadata: false,
                    cancellationToken);
            }

            logger.LogInformation(
                "Server discovery refresh completed. SteamServers: {SteamServers}. BattleMetricsServers: {BattleMetricsServers}. ImportedServers: {ImportedServers}. PolledServers: {PolledServers}.",
                steamDiscoveredServers.Count,
                battleMetricsDiscoveredServers.Count,
                discoveredServers.Count,
                steamDiscoveredServers.Count);
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
