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
            IReadOnlyList<ConfiguredRustServer> discoveredServers;
            try
            {
                discoveredServers = await discoveryClient.DiscoverServersAsync(
                    options.Value.MaxServersPerRefresh,
                    cancellationToken);
            }
            catch (Exception exception) when (options.Value.AllowBattleMetricsFallback)
            {
                logger.LogWarning(
                    exception,
                    "Steam discovery failed. Falling back to BattleMetrics directory discovery for this refresh.");

                discoveredServers = await DiscoverViaBattleMetricsAsync(cancellationToken);
            }

            if (discoveredServers.Count == 0)
            {
                if (!options.Value.AllowBattleMetricsFallback)
                {
                    logger.LogWarning("Steam discovery returned no Rust servers.");
                    return;
                }

                logger.LogWarning("Steam discovery returned no Rust servers. Falling back to BattleMetrics directory discovery for this refresh.");
                discoveredServers = await DiscoverViaBattleMetricsAsync(cancellationToken);
                if (discoveredServers.Count == 0)
                {
                    logger.LogWarning("BattleMetrics fallback discovery also returned no Rust servers.");
                    return;
                }
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();
            await pollingService.EnsureConfiguredServersExistAsync(discoveredServers, cancellationToken);

            var offlineThreshold = TimeSpan.FromSeconds(queryOptions.Value.OfflineThresholdSeconds);
            foreach (var server in discoveredServers)
            {
                await pollingService.PollConfiguredServerAsync(
                    server,
                    offlineThreshold,
                    refreshCurrentPlayers: false,
                    refreshMapMetadata: false,
                    cancellationToken);
            }

            logger.LogInformation(
                "Steam discovery refresh completed. ImportedServers: {ImportedServers}.",
                discoveredServers.Count);
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
}
