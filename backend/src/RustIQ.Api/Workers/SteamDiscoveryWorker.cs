using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Api.Workers;

public sealed class SteamDiscoveryWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<SteamDiscoveryOptions> options,
    ISteamServerDiscoveryClient discoveryClient,
    IOptions<FacepunchDiscoveryOptions> facepunchDiscoveryOptions,
    IFacepunchServerDirectoryClient facepunchDirectoryClient,
    IOptions<BattleMetricsDiscoveryOptions> battleMetricsDiscoveryOptions,
    IBattleMetricsServerDirectoryClient battleMetricsDirectoryClient,
    IOptions<RustServerQueryOptions> queryOptions,
    ILogger<SteamDiscoveryWorker> logger)
    : BackgroundService
{
    // The Facepunch server list needs no API key, so it is worth running on its own when Steam has
    // no usable key rather than stopping discovery altogether.
    private bool IsSteamDiscoveryUsable =>
        options.Value.Enabled && !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Enabled && string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            // Not a startup failure: the rest of the site runs fine without discovery, and taking the
            // whole API down over a missing optional key would be worse than losing new servers.
            logger.LogError(
                "Steam discovery is enabled but no API key is configured. Create a free key at "
                + "https://steamcommunity.com/dev/apikey and set SteamDiscovery:ApiKey "
                + "(RUSTIQ_STEAM_API_KEY).");
        }

        if (!IsSteamDiscoveryUsable && !facepunchDiscoveryOptions.Value.Enabled)
        {
            logger.LogInformation("Server discovery worker is disabled: no usable discovery source.");
            return;
        }

        var interval = TimeSpan.FromMinutes(options.Value.RefreshIntervalMinutes);

        logger.LogInformation(
            "Server discovery worker started. Steam: {SteamEnabled}. Facepunch server list: {FacepunchEnabled}. MaxServersPerRefresh: {MaxServersPerRefresh}. RefreshInterval: {RefreshInterval}.",
            IsSteamDiscoveryUsable,
            facepunchDiscoveryOptions.Value.Enabled,
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
            IReadOnlyList<ConfiguredRustServer> steamDiscoveredServers = [];
            if (IsSteamDiscoveryUsable)
            {
                try
                {
                    steamDiscoveredServers = await discoveryClient.DiscoverServersAsync(
                        options.Value.MaxServersPerRefresh,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Steam discovery failed. Continuing with the remaining discovery sources for this refresh.");
                }
            }

            IReadOnlyList<ConfiguredRustServer> facepunchDiscoveredServers;
            try
            {
                facepunchDiscoveredServers = await DiscoverViaFacepunchAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Facepunch server list discovery failed. Servers discovered this refresh will have no location.");

                facepunchDiscoveredServers = [];
            }

            IReadOnlyList<ConfiguredRustServer> battleMetricsDiscoveredServers;
            try
            {
                battleMetricsDiscoveredServers = await DiscoverViaBattleMetricsAsync(cancellationToken);
            }
            catch (Exception exception) when (steamDiscoveredServers.Count > 0 || facepunchDiscoveredServers.Count > 0)
            {
                logger.LogWarning(
                    exception,
                    "BattleMetrics directory discovery failed. Continuing with Steam discovery results for this refresh.");

                battleMetricsDiscoveredServers = [];
            }

            if (steamDiscoveredServers.Count == 0
                && facepunchDiscoveredServers.Count == 0
                && battleMetricsDiscoveredServers.Count == 0)
            {
                logger.LogWarning("No discovery source returned any Rust servers.");
                return;
            }

            var discoveredServers = MergeDiscoveredServers(
                steamDiscoveredServers,
                facepunchDiscoveredServers,
                battleMetricsDiscoveredServers);
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
                "Server discovery refresh completed. SteamServers: {SteamServers}. FacepunchServers: {FacepunchServers}. BattleMetricsServers: {BattleMetricsServers}. ImportedServers: {ImportedServers}. NewServers: {NewServers}. PolledServers: {PolledServers}.",
                steamDiscoveredServers.Count,
                facepunchDiscoveredServers.Count,
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

    private async Task<IReadOnlyList<ConfiguredRustServer>> DiscoverViaFacepunchAsync(CancellationToken cancellationToken)
    {
        if (!facepunchDiscoveryOptions.Value.Enabled)
        {
            return [];
        }

        return await facepunchDirectoryClient.GetRustServersAsync(
            facepunchDiscoveryOptions.Value.MaxServersPerRefresh,
            cancellationToken);
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
        IReadOnlyList<ConfiguredRustServer> facepunchServers,
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

        // Facepunch comes last and fills gaps rather than replacing what the other sources found: it
        // is here for the location the others cannot supply, not to take over their entries. A server
        // only this list knows about is still worth adding.
        var keysByEndpoint = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, server) in serversByKey)
        {
            keysByEndpoint.TryAdd(EndpointKey(server), key);
        }

        foreach (var server in facepunchServers)
        {
            if (keysByEndpoint.TryGetValue(EndpointKey(server), out var existingKey))
            {
                serversByKey[existingKey] = FillGaps(serversByKey[existingKey], server);
                continue;
            }

            serversByKey[ServerKey(server)] = server;
        }

        return serversByKey.Values.ToArray();
    }

    private static ConfiguredRustServer FillGaps(
        ConfiguredRustServer server,
        ConfiguredRustServer supplement)
    {
        return new ConfiguredRustServer
        {
            // Identity and provenance stay with the source that found the server.
            Host = server.Host,
            QueryPort = server.QueryPort,
            DiscoverySource = server.DiscoverySource,
            SummarySource = server.SummarySource,
            PlayerCountSource = server.PlayerCountSource,
            PlayerListSource = server.PlayerListSource,
            BattleMetricsServerId = server.BattleMetricsServerId ?? supplement.BattleMetricsServerId,

            Name = server.Name ?? supplement.Name,
            Map = server.Map ?? supplement.Map,
            Address = server.Address ?? supplement.Address,
            GamePort = server.GamePort ?? supplement.GamePort,
            Country = string.IsNullOrWhiteSpace(server.Country) ? supplement.Country : server.Country,
            Latitude = server.Latitude ?? supplement.Latitude,
            Longitude = server.Longitude ?? supplement.Longitude,
            LastWipeAtUtc = server.LastWipeAtUtc ?? supplement.LastWipeAtUtc,
            NextWipeAtUtc = server.NextWipeAtUtc ?? supplement.NextWipeAtUtc,
            QueuePlayers = server.QueuePlayers ?? supplement.QueuePlayers,
            ServerType = server.ServerType == RustServerType.Unknown
                ? supplement.ServerType
                : server.ServerType
        };
    }

    private static string ServerKey(ConfiguredRustServer server)
    {
        return server.BattleMetricsServerId is not null
            ? $"bm:{server.BattleMetricsServerId.Value}"
            : EndpointKey(server);
    }

    private static string EndpointKey(ConfiguredRustServer server)
    {
        return $"endpoint:{server.Host.Trim().ToLowerInvariant()}:{server.QueryPort}";
    }
}
