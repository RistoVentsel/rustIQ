using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed class ServerPollingService(
    IRustServerQueryClient queryClient,
    IBattleMetricsPlayerCountClient battleMetricsPlayerCountClient,
    IRustServerMapMetadataClient mapMetadataClient,
    IServerRepository serverRepository)
{
    /// <summary>
    /// Registers every configured server that is not tracked yet and returns the ones that were
    /// newly created, so callers can give those a first poll without re-polling known servers.
    /// </summary>
    public async Task<IReadOnlyList<ConfiguredRustServer>> EnsureConfiguredServersExistAsync(
        IReadOnlyList<ConfiguredRustServer> configuredServers,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var hasChanges = false;
        var createdServers = new List<ConfiguredRustServer>();

        foreach (var configuredServer in configuredServers)
        {
            var configuredName = NormalizeConfiguredName(configuredServer.Name);
            var server = await FindTrackedServerAsync(configuredServer, cancellationToken);

            if (server is null)
            {
                serverRepository.Add(new Server
                {
                    Id = Guid.NewGuid(),
                    BattleMetricsServerId = configuredServer.BattleMetricsServerId,
                    Address = configuredServer.Address,
                    Host = configuredServer.Host,
                    GamePort = configuredServer.GamePort,
                    QueryPort = configuredServer.QueryPort,
                    Country = configuredServer.Country,
                    Latitude = configuredServer.Latitude,
                    Longitude = configuredServer.Longitude,
                    LastWipeAtUtc = configuredServer.LastWipeAtUtc,
                    NextWipeAtUtc = configuredServer.NextWipeAtUtc,
                    QueuePlayers = configuredServer.QueuePlayers,
                    ServerType = configuredServer.ServerType,
                    Name = configuredName,
                    Status = ServerStatus.Unknown,
                    DiscoverySource = configuredServer.DiscoverySource,
                    SummarySource = configuredServer.SummarySource,
                    PlayerCountSource = configuredServer.PlayerCountSource,
                    PlayerListSource = configuredServer.PlayerListSource,
                    DataQuality = ServerDataQuality.Unknown,
                    LastDiscoveredAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                createdServers.Add(configuredServer);
                hasChanges = true;
                continue;
            }

            hasChanges |= ApplyConfiguredIdentity(server, configuredServer, configuredName, now);
        }

        if (hasChanges)
        {
            await serverRepository.SaveChangesAsync(cancellationToken);
        }

        return createdServers;
    }

    public Task<ServerDto> PollConfiguredServerAsync(
        ConfiguredRustServer configuredServer,
        TimeSpan offlineThreshold,
        CancellationToken cancellationToken)
    {
        return PollConfiguredServerAsync(
            configuredServer,
            offlineThreshold,
            refreshCurrentPlayers: true,
            refreshMapMetadata: true,
            cancellationToken);
    }

    public async Task<ServerDto> PollConfiguredServerAsync(
        ConfiguredRustServer configuredServer,
        TimeSpan offlineThreshold,
        bool refreshCurrentPlayers,
        bool refreshMapMetadata,
        CancellationToken cancellationToken)
    {
        var queryResult = await queryClient.QueryServerAsync(configuredServer, cancellationToken);
        var server = await FindTrackedServerAsync(configuredServer, queryResult, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (server is null)
        {
            server = new Server
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            serverRepository.Add(server);
        }

        var enrichedQueryResult = await EnrichPlayerCountAsync(configuredServer, server, queryResult, cancellationToken);
        var normalizedQueryResult = NormalizeQueryResult(enrichedQueryResult);

        ApplySummaryIdentity(server, configuredServer, normalizedQueryResult, now);
        ApplyLatestServerState(server, normalizedQueryResult, now, offlineThreshold);

        var snapshot = CreateSnapshot(server, normalizedQueryResult);
        serverRepository.AddSnapshot(snapshot);

        var effectiveConfiguredServer = new ConfiguredRustServer
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
            ServerType = server.ServerType,
            DiscoverySource = server.DiscoverySource,
            SummarySource = server.SummarySource,
            PlayerCountSource = server.PlayerCountSource,
            PlayerListSource = server.PlayerListSource
        };

        if (refreshMapMetadata)
        {
            var mapMetadata = await mapMetadataClient.GetMapMetadataAsync(effectiveConfiguredServer, cancellationToken);
            ApplyMapMetadata(server, mapMetadata, now);
            ApplyMapMetadataIdentityAndLastWipe(server, mapMetadata, now);
            if (ShouldApplyInferredNextWipe(
                    server.NextWipeAtUtc,
                    mapMetadata.NextWipeAtUtc,
                    now))
            {
                server.NextWipeAtUtc = mapMetadata.NextWipeAtUtc;
            }
        }

        IReadOnlyList<ServerPlayerCurrent> currentPlayers = [];
        if (refreshCurrentPlayers && effectiveConfiguredServer.PlayerListSource == PlayerListSource.A2S)
        {
            currentPlayers = await QueryCurrentPlayersAsync(effectiveConfiguredServer, server.Id, now, cancellationToken);
            await serverRepository.ReplaceCurrentPlayersAsync(server.Id, currentPlayers, cancellationToken);
            server.CurrentPlayers = currentPlayers.ToList();
        }

        await serverRepository.SaveChangesAsync(cancellationToken);

        return ToDto(server, snapshot, currentPlayers);
    }

    public async Task<bool> RefreshMapMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var server = await FindTrackedServerAsync(configuredServer, cancellationToken);
        if (server is null)
        {
            return false;
        }

        var effectiveConfiguredServer = new ConfiguredRustServer
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
            ServerType = server.ServerType,
            DiscoverySource = server.DiscoverySource,
            SummarySource = server.SummarySource,
            PlayerCountSource = server.PlayerCountSource,
            PlayerListSource = server.PlayerListSource
        };
        var metadata = await mapMetadataClient.GetMapMetadataAsync(
            effectiveConfiguredServer,
            cancellationToken);

        var hasUsableMapMetadata = HasUsableMapMetadata(metadata);
        var updatedAtUtc = DateTimeOffset.UtcNow;
        var shouldApplyBattleMetricsServerId = server.BattleMetricsServerId is null
                                               && metadata.BattleMetricsServerId is not null;
        var shouldApplyLastWipe = ShouldApplyReportedLastWipe(
            server.LastWipeAtUtc,
            metadata.LastWipeAtUtc,
            updatedAtUtc);
        var shouldApplyNextWipe = ShouldApplyInferredNextWipe(
            server.NextWipeAtUtc,
            metadata.NextWipeAtUtc,
            updatedAtUtc);
        if (!hasUsableMapMetadata
            && !shouldApplyBattleMetricsServerId
            && !shouldApplyLastWipe
            && !shouldApplyNextWipe)
        {
            return false;
        }

        if (hasUsableMapMetadata)
        {
            ApplyMapMetadata(server, metadata, updatedAtUtc);
            if (!string.IsNullOrWhiteSpace(metadata.MapName))
            {
                server.Map = metadata.MapName;
            }
        }

        if (shouldApplyBattleMetricsServerId || shouldApplyLastWipe)
        {
            ApplyMapMetadataIdentityAndLastWipe(server, metadata, updatedAtUtc);
        }

        if (shouldApplyNextWipe)
        {
            server.NextWipeAtUtc = metadata.NextWipeAtUtc;
            server.UpdatedAtUtc = updatedAtUtc;
        }

        await serverRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Server?> FindTrackedServerAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        if (configuredServer.BattleMetricsServerId is not null)
        {
            var byBattleMetricsId = await serverRepository.GetByBattleMetricsServerIdAsync(
                configuredServer.BattleMetricsServerId.Value,
                cancellationToken);

            if (byBattleMetricsId is not null)
            {
                return byBattleMetricsId;
            }
        }

        return await serverRepository.GetByHostAndQueryPortAsync(
            configuredServer.Host,
            configuredServer.QueryPort,
            cancellationToken);
    }

    private async Task<Server?> FindTrackedServerAsync(
        ConfiguredRustServer configuredServer,
        RustServerQueryResult queryResult,
        CancellationToken cancellationToken)
    {
        if (queryResult.BattleMetricsServerId is not null)
        {
            var byBattleMetricsId = await serverRepository.GetByBattleMetricsServerIdAsync(
                queryResult.BattleMetricsServerId.Value,
                cancellationToken);

            if (byBattleMetricsId is not null)
            {
                return byBattleMetricsId;
            }
        }

        return await FindTrackedServerAsync(configuredServer, cancellationToken);
    }

    private static bool ApplyConfiguredIdentity(
        Server server,
        ConfiguredRustServer configuredServer,
        string? configuredName,
        DateTimeOffset updatedAtUtc)
    {
        var hasChanges = false;

        if (configuredServer.BattleMetricsServerId is not null)
        {
            hasChanges |= UpdateBattleMetricsServerId(server, configuredServer.BattleMetricsServerId);
        }

        if (!string.IsNullOrWhiteSpace(configuredServer.Address))
        {
            hasChanges |= UpdateAddress(server, configuredServer.Address);
        }

        hasChanges |= UpdateHost(server, configuredServer.Host);

        if (configuredServer.GamePort is not null)
        {
            hasChanges |= UpdateGamePort(server, configuredServer.GamePort);
        }

        hasChanges |= UpdateQueryPort(server, configuredServer.QueryPort);

        if (!string.IsNullOrWhiteSpace(configuredServer.Country))
        {
            hasChanges |= UpdateCountry(server, configuredServer.Country);
        }

        if (configuredServer.Latitude is not null)
        {
            hasChanges |= UpdateLatitude(server, configuredServer.Latitude);
        }

        if (configuredServer.Longitude is not null)
        {
            hasChanges |= UpdateLongitude(server, configuredServer.Longitude);
        }

        if (configuredServer.LastWipeAtUtc is not null)
        {
            hasChanges |= UpdateLastWipeAt(server, configuredServer.LastWipeAtUtc);
        }

        if (configuredServer.NextWipeAtUtc is not null)
        {
            hasChanges |= UpdateNextWipeAt(server, configuredServer.NextWipeAtUtc);
        }

        hasChanges |= UpdateDiscoverySource(server, configuredServer.DiscoverySource);
        hasChanges |= UpdateSummarySource(server, configuredServer.SummarySource);
        hasChanges |= UpdatePlayerCountSource(server, configuredServer.PlayerCountSource);
        hasChanges |= UpdatePlayerListSource(server, configuredServer.PlayerListSource);
        hasChanges |= UpdateServerType(server, configuredServer.ServerType);
        hasChanges |= UpdateLastDiscoveredAt(server, updatedAtUtc);

        if (server.LastSeenAtUtc is null
            && configuredName is not null
            && !string.Equals(server.Name, configuredName, StringComparison.Ordinal))
        {
            server.Name = configuredName;
            hasChanges = true;
        }

        if (hasChanges)
        {
            server.UpdatedAtUtc = updatedAtUtc;
        }

        return hasChanges;
    }

    private static void ApplySummaryIdentity(
        Server server,
        ConfiguredRustServer configuredServer,
        RustServerQueryResult queryResult,
        DateTimeOffset updatedAtUtc)
    {
        server.BattleMetricsServerId = queryResult.BattleMetricsServerId
            ?? configuredServer.BattleMetricsServerId
            ?? server.BattleMetricsServerId;
        server.Address = Coalesce(queryResult.Address, configuredServer.Address, server.Address);
        server.Host = Coalesce(queryResult.Host, configuredServer.Host, server.Host) ?? server.Host;
        server.GamePort = queryResult.GamePort ?? configuredServer.GamePort ?? server.GamePort;
        server.QueryPort = queryResult.QueryPort;
        server.Country = Coalesce(queryResult.Country, configuredServer.Country, server.Country);
        server.Latitude = queryResult.Latitude ?? configuredServer.Latitude ?? server.Latitude;
        server.Longitude = queryResult.Longitude ?? configuredServer.Longitude ?? server.Longitude;
        server.LastWipeAtUtc = queryResult.LastWipeAtUtc ?? configuredServer.LastWipeAtUtc ?? server.LastWipeAtUtc;
        server.NextWipeAtUtc = queryResult.NextWipeAtUtc ?? configuredServer.NextWipeAtUtc ?? server.NextWipeAtUtc;
        server.QueuePlayers = NormalizeQueuePlayers(queryResult.QueuePlayers ?? configuredServer.QueuePlayers ?? server.QueuePlayers);
        server.ServerType = configuredServer.ServerType == RustServerType.Unknown
            ? server.ServerType
            : configuredServer.ServerType;
        server.DiscoverySource = configuredServer.DiscoverySource;
        server.SummarySource = queryResult.SummarySource;
        server.PlayerCountSource = queryResult.PlayerCountSource;
        server.PlayerListSource = configuredServer.PlayerListSource;
        server.LastDiscoveredAtUtc = configuredServer.DiscoverySource == ServerDiscoverySource.Manual
            ? server.LastDiscoveredAtUtc
            : updatedAtUtc;
        server.UpdatedAtUtc = updatedAtUtc;
    }

    private static void ApplyLatestServerState(
        Server server,
        RustServerQueryResult queryResult,
        DateTimeOffset updatedAtUtc,
        TimeSpan offlineThreshold)
    {
        server.LastCheckedAtUtc = queryResult.CheckedAtUtc;
        server.UpdatedAtUtc = updatedAtUtc;

        if (queryResult.IsOnline)
        {
            server.Name = string.IsNullOrWhiteSpace(queryResult.Name) ? server.Name : queryResult.Name;
            server.Map = string.IsNullOrWhiteSpace(queryResult.Map) ? server.Map : queryResult.Map;
            server.Players = queryResult.Players;
            server.MaxPlayers = queryResult.MaxPlayers;
            server.QueuePlayers = queryResult.QueuePlayers;
            server.Status = ServerStatus.Online;
            server.LastSeenAtUtc = queryResult.CheckedAtUtc;
            server.LastPlayerCountUpdatedAtUtc = queryResult.CheckedAtUtc;
            server.DataQuality = GetDataQuality(queryResult);
            server.LastValidationReason = GetLastValidationReason(queryResult);

            return;
        }

        if (server.LastSeenAtUtc is null
            || queryResult.CheckedAtUtc - server.LastSeenAtUtc > offlineThreshold)
        {
            server.Status = ServerStatus.Offline;
            server.Players = 0;
        }

        server.DataQuality = ServerDataQuality.Degraded;
        server.LastValidationReason = queryResult.Error;
    }

    private static ServerSnapshot CreateSnapshot(Server server, RustServerQueryResult queryResult)
    {
        // Taken from the server's resulting state rather than the raw query result, so history agrees
        // with the status being shown. A failed query inside the offline grace period keeps the last
        // known counts, and recording a 0/offline point for it would put a dip in the player graph and
        // drag down the ranking average for a server still listed as online. This matches how
        // ServerRefreshService records its snapshots.
        return new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = server.Id,
            CheckedAtUtc = queryResult.CheckedAtUtc,
            Players = server.Players,
            MaxPlayers = server.MaxPlayers,
            IsOnline = server.Status == ServerStatus.Online,
            PingMilliseconds = queryResult.PingMilliseconds
        };
    }

    private static void ApplyMapMetadata(
        Server server,
        RustServerMapMetadata metadata,
        DateTimeOffset updatedAtUtc)
    {
        if (!HasUsableMapMetadata(metadata))
        {
            return;
        }

        server.MapMetadata ??= new ServerMapMetadata
        {
            Id = Guid.NewGuid(),
            ServerId = server.Id,
            CreatedAtUtc = updatedAtUtc
        };

        server.MapMetadata.MapName = metadata.MapName;
        server.MapMetadata.Seed = metadata.Seed;
        server.MapMetadata.Size = metadata.Size;
        server.MapMetadata.RustMapsUrl = metadata.RustMapsUrl;
        server.MapMetadata.ThumbnailUrl = metadata.ThumbnailUrl;
        server.MapMetadata.MapFileUrl = metadata.MapFileUrl;
        server.MapMetadata.Source = metadata.Source;
        server.MapMetadata.SourceUpdatedAtUtc = metadata.SourceUpdatedAtUtc;
        server.MapMetadata.FetchedAtUtc = metadata.FetchedAtUtc;
        server.MapMetadata.UpdatedAtUtc = updatedAtUtc;
    }

    private static void ApplyMapMetadataIdentityAndLastWipe(
        Server server,
        RustServerMapMetadata metadata,
        DateTimeOffset updatedAtUtc)
    {
        var changed = false;
        if (server.BattleMetricsServerId is null && metadata.BattleMetricsServerId is not null)
        {
            server.BattleMetricsServerId = metadata.BattleMetricsServerId;
            changed = true;
        }

        if (ShouldApplyReportedLastWipe(server.LastWipeAtUtc, metadata.LastWipeAtUtc, updatedAtUtc))
        {
            server.LastWipeAtUtc = metadata.LastWipeAtUtc;
            changed = true;
        }

        if (changed)
        {
            server.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private async Task<IReadOnlyList<ServerPlayerCurrent>> QueryCurrentPlayersAsync(
        ConfiguredRustServer configuredServer,
        Guid serverId,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken)
    {
        var playerResults = await queryClient.QueryPlayersAsync(configuredServer, cancellationToken);
        var namedPlayers = playerResults
            .Where(player => !string.IsNullOrWhiteSpace(player.Name))
            .Select(player => new ServerPlayerCurrent
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                PlayerName = player.Name,
                DurationSeconds = player.DurationSeconds,
                SeenAtUtc = seenAtUtc
            })
            .ToArray();

        return namedPlayers;
    }

    private async Task<RustServerQueryResult> EnrichPlayerCountAsync(
        ConfiguredRustServer configuredServer,
        Server server,
        RustServerQueryResult queryResult,
        CancellationToken cancellationToken)
    {
        var shouldUseBattleMetricsPlayerCount =
            configuredServer.PlayerCountSource == ServerPlayerCountSource.BattleMetrics
            || server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics
            || configuredServer.BattleMetricsServerId is not null
            || server.BattleMetricsServerId is not null;

        if (!shouldUseBattleMetricsPlayerCount)
        {
            return queryResult;
        }

        var battleMetricsServerId = queryResult.BattleMetricsServerId
            ?? configuredServer.BattleMetricsServerId
            ?? server.BattleMetricsServerId;

        if (battleMetricsServerId is null)
        {
            battleMetricsServerId = await battleMetricsPlayerCountClient.FindServerIdAsync(
                configuredServer.Host,
                configuredServer.QueryPort,
                cancellationToken);
        }

        if (battleMetricsServerId is null)
        {
            return queryResult;
        }

        var battleMetricsPlayerCount = await battleMetricsPlayerCountClient.GetPlayerCountAsync(
            battleMetricsServerId.Value,
            cancellationToken);

        if (battleMetricsPlayerCount is null)
        {
            if (queryResult.IsOnline
                && queryResult.PlayerCountSource == ServerPlayerCountSource.A2S)
            {
                return queryResult with
                {
                    BattleMetricsServerId = battleMetricsServerId
                };
            }

            var lastKnownBattleMetricsPlayers = server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics
                ? server.Players
                : queryResult.Players;
            var lastKnownBattleMetricsMaxPlayers = server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics
                ? server.MaxPlayers
                : queryResult.MaxPlayers;

            return queryResult with
            {
                BattleMetricsServerId = battleMetricsServerId,
                Players = lastKnownBattleMetricsPlayers,
                MaxPlayers = lastKnownBattleMetricsMaxPlayers,
                QueuePlayers = server.QueuePlayers ?? queryResult.QueuePlayers,
                PlayerCountSource = ServerPlayerCountSource.BattleMetrics
            };
        }

        if (battleMetricsPlayerCount.ServerType != RustServerType.Unknown)
        {
            server.ServerType = battleMetricsPlayerCount.ServerType;
        }

        return queryResult with
        {
            BattleMetricsServerId = battleMetricsServerId,
            Country = battleMetricsPlayerCount.Country ?? queryResult.Country,
            Latitude = battleMetricsPlayerCount.Latitude ?? queryResult.Latitude,
            Longitude = battleMetricsPlayerCount.Longitude ?? queryResult.Longitude,
            LastWipeAtUtc = battleMetricsPlayerCount.LastWipeAtUtc ?? queryResult.LastWipeAtUtc,
            NextWipeAtUtc = battleMetricsPlayerCount.NextWipeAtUtc ?? queryResult.NextWipeAtUtc,
            Players = battleMetricsPlayerCount.Players,
            MaxPlayers = battleMetricsPlayerCount.MaxPlayers,
            QueuePlayers = battleMetricsPlayerCount.QueuePlayers,
            IsOnline = battleMetricsPlayerCount.IsOnline,
            CheckedAtUtc = battleMetricsPlayerCount.CheckedAtUtc,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics
        };
    }

    private static RustServerQueryResult NormalizeQueryResult(RustServerQueryResult queryResult)
    {
        var normalizedPlayers = Math.Max(0, queryResult.Players);
        var normalizedMaxPlayers = Math.Max(0, queryResult.MaxPlayers);
        var normalizedQueuePlayers = NormalizeQueuePlayers(queryResult.QueuePlayers);
        string? validationReason = null;

        if (queryResult.IsOnline && normalizedPlayers > normalizedMaxPlayers)
        {
            normalizedMaxPlayers = normalizedPlayers;
            validationReason = "PlayersExceedMaxPlayers";
        }

        var mergedError = queryResult.Error;
        if (validationReason is not null)
        {
            mergedError = string.IsNullOrWhiteSpace(mergedError)
                ? validationReason
                : $"{validationReason}; {mergedError}";
        }

        return queryResult with
        {
            Players = normalizedPlayers,
            MaxPlayers = normalizedMaxPlayers,
            QueuePlayers = normalizedQueuePlayers,
            Error = mergedError
        };
    }

    private static ServerDataQuality GetDataQuality(RustServerQueryResult queryResult)
    {
        return string.IsNullOrWhiteSpace(queryResult.Error)
            ? ServerDataQuality.Normal
            : ServerDataQuality.Suspicious;
    }

    private static string? GetLastValidationReason(RustServerQueryResult queryResult)
    {
        return string.IsNullOrWhiteSpace(queryResult.Error)
            ? null
            : queryResult.Error;
    }

    private static bool HasUsableMapMetadata(RustServerMapMetadata metadata)
    {
        return metadata.MapName is not null
            || metadata.Seed is not null
            || metadata.Size is not null
            || metadata.RustMapsUrl is not null
            || metadata.ThumbnailUrl is not null
            || metadata.MapFileUrl is not null;
    }

    private static bool ShouldApplyInferredNextWipe(
        DateTimeOffset? currentNextWipeAtUtc,
        DateTimeOffset? inferredNextWipeAtUtc,
        DateTimeOffset now)
    {
        if (inferredNextWipeAtUtc is null || inferredNextWipeAtUtc < now)
        {
            return false;
        }

        return currentNextWipeAtUtc is null
               || currentNextWipeAtUtc < now
               || inferredNextWipeAtUtc < currentNextWipeAtUtc;
    }

    private static bool ShouldApplyReportedLastWipe(
        DateTimeOffset? currentLastWipeAtUtc,
        DateTimeOffset? reportedLastWipeAtUtc,
        DateTimeOffset now)
    {
        return reportedLastWipeAtUtc is not null
               && reportedLastWipeAtUtc <= now.AddMinutes(5)
               && (currentLastWipeAtUtc is null || reportedLastWipeAtUtc > currentLastWipeAtUtc);
    }

    private static ServerDto ToDto(
        Server server,
        ServerSnapshot latestSnapshot,
        IReadOnlyList<ServerPlayerCurrent> currentPlayers)
    {
        return new ServerDto(
            server.Id,
            server.BattleMetricsServerId,
            server.Address,
            server.Host,
            server.GamePort,
            server.QueryPort,
            server.Country,
            server.Latitude,
            server.Longitude,
            server.LastWipeAtUtc,
            server.NextWipeAtUtc,
            server.Name,
            server.Map,
            server.Players,
            server.MaxPlayers,
            server.QueuePlayers,
            server.ServerType,
            server.Status,
            server.DiscoverySource,
            server.SummarySource,
            server.PlayerCountSource,
            server.PlayerListSource,
            server.DataQuality,
            server.LastValidationReason,
            server.LastSeenAtUtc,
            server.LastCheckedAtUtc,
            server.LastDiscoveredAtUtc,
            server.LastPlayerCountUpdatedAtUtc,
            server.CreatedAtUtc,
            server.UpdatedAtUtc,
            ToMapMetadataDto(server.MapMetadata),
            ToSnapshotDto(latestSnapshot),
            currentPlayers.Select(ToCurrentPlayerDto).ToArray());
    }

    private static ServerMapMetadataDto? ToMapMetadataDto(ServerMapMetadata? metadata)
    {
        return metadata is null
            ? null
            : new ServerMapMetadataDto(
                metadata.MapName,
                metadata.Seed,
                metadata.Size,
                metadata.RustMapsUrl,
                metadata.ThumbnailUrl,
                metadata.MapFileUrl,
                metadata.Source,
                metadata.SourceUpdatedAtUtc,
                metadata.FetchedAtUtc);
    }

    private static ServerSnapshotDto ToSnapshotDto(ServerSnapshot snapshot)
    {
        return new ServerSnapshotDto(
            snapshot.CheckedAtUtc,
            snapshot.Players,
            snapshot.MaxPlayers,
            snapshot.IsOnline,
            snapshot.PingMilliseconds);
    }

    private static CurrentPlayerDto ToCurrentPlayerDto(ServerPlayerCurrent player)
    {
        return new CurrentPlayerDto(
            player.PlayerName,
            player.DurationSeconds,
            player.SeenAtUtc);
    }

    private static string? NormalizeConfiguredName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Coalesce(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool UpdateBattleMetricsServerId(Server server, long? nextValue)
    {
        return UpdateIfDifferent(server.BattleMetricsServerId, nextValue, value => server.BattleMetricsServerId = value);
    }

    private static bool UpdateAddress(Server server, string? nextValue)
    {
        return UpdateIfDifferent(server.Address, nextValue, value => server.Address = value);
    }

    private static bool UpdateHost(Server server, string nextValue)
    {
        return UpdateIfDifferent(server.Host, nextValue, value => server.Host = value);
    }

    private static bool UpdateGamePort(Server server, int? nextValue)
    {
        return UpdateIfDifferent(server.GamePort, nextValue, value => server.GamePort = value);
    }

    private static bool UpdateQueryPort(Server server, int nextValue)
    {
        return UpdateIfDifferent(server.QueryPort, nextValue, value => server.QueryPort = value);
    }

    private static bool UpdateCountry(Server server, string? nextValue)
    {
        return UpdateIfDifferent(server.Country, nextValue, value => server.Country = value);
    }

    private static bool UpdateLatitude(Server server, double? nextValue)
    {
        return UpdateIfDifferent(server.Latitude, nextValue, value => server.Latitude = value);
    }

    private static bool UpdateLongitude(Server server, double? nextValue)
    {
        return UpdateIfDifferent(server.Longitude, nextValue, value => server.Longitude = value);
    }

    private static bool UpdateLastWipeAt(Server server, DateTimeOffset? nextValue)
    {
        return UpdateIfDifferent(server.LastWipeAtUtc, nextValue, value => server.LastWipeAtUtc = value);
    }

    private static bool UpdateNextWipeAt(Server server, DateTimeOffset? nextValue)
    {
        return UpdateIfDifferent(server.NextWipeAtUtc, nextValue, value => server.NextWipeAtUtc = value);
    }

    private static int? NormalizeQueuePlayers(int? queuePlayers)
    {
        return queuePlayers is null ? null : Math.Max(0, queuePlayers.Value);
    }

    private static bool UpdateDiscoverySource(Server server, ServerDiscoverySource nextValue)
    {
        return UpdateIfDifferent(server.DiscoverySource, nextValue, value => server.DiscoverySource = value);
    }

    private static bool UpdateSummarySource(Server server, ServerSummarySource nextValue)
    {
        return UpdateIfDifferent(server.SummarySource, nextValue, value => server.SummarySource = value);
    }

    private static bool UpdatePlayerCountSource(Server server, ServerPlayerCountSource nextValue)
    {
        if (server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics
            && nextValue != ServerPlayerCountSource.BattleMetrics)
        {
            return false;
        }

        return UpdateIfDifferent(server.PlayerCountSource, nextValue, value => server.PlayerCountSource = value);
    }

    private static bool UpdatePlayerListSource(Server server, PlayerListSource nextValue)
    {
        return UpdateIfDifferent(server.PlayerListSource, nextValue, value => server.PlayerListSource = value);
    }

    private static bool UpdateServerType(Server server, RustServerType nextValue)
    {
        if (nextValue == RustServerType.Unknown)
        {
            return false;
        }

        return UpdateIfDifferent(server.ServerType, nextValue, value => server.ServerType = value);
    }

    private static bool UpdateLastDiscoveredAt(Server server, DateTimeOffset nextValue)
    {
        return UpdateIfDifferent(server.LastDiscoveredAtUtc, nextValue, value => server.LastDiscoveredAtUtc = value);
    }

    private static bool UpdateIfDifferent<T>(T currentValue, T nextValue, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return false;
        }

        setter(nextValue);
        return true;
    }
}
