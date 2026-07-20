using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed class ServerReadService(
    IServerRepository serverRepository,
    IServerRefreshService? serverRefreshService = null)
{
    public const string RefreshNoneMode = "none";
    public const string RefreshPageMode = "page";
    public const string RefreshAllMode = "all";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const int DefaultSnapshotLimit = 100;
    private const int MaxSnapshotLimit = 500;
    public const string SortByPlayers = "players";
    public const string SortByRank = "rank";
    public const string SortByLastWipe = "lastWipe";
    public const string SortByNextWipe = "nextWipe";
    public const string SortByLocation = "location";
    public const string SortByServerType = "serverType";
    public const string SortAscending = "asc";
    public const string SortDescending = "desc";
    public const string WipeFilterAll = "all";
    public const string WipeFilterNext24Hours = "next24h";
    public const string WipeFilterNext7Days = "next7d";
    public const string WipeFilterWiped24Hours = "wiped24h";
    public const string WipeFilterHasWipeData = "hasWipeData";

    public async Task<PagedResult<ServerListItemDto>> GetServersAsync(
        string? search,
        ServerStatus? status,
        int? page,
        int? pageSize,
        string? sortBy,
        string? sortDirection,
        string? serverTypes,
        string? refreshMode,
        CancellationToken cancellationToken,
        string? wipeFilter = null)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedPage = Math.Max(page ?? DefaultPage, 1);
        var normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var normalizedSortBy = NormalizeSortBy(sortBy);
        var sortDescending = NormalizeSortDirection(sortDirection);
        var normalizedServerTypes = NormalizeServerTypes(serverTypes);
        var normalizedWipeFilter = NormalizeWipeFilter(wipeFilter);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var servers = await serverRepository.GetPagedAsync(
            search,
            status,
            normalizedServerTypes,
            normalizedWipeFilter,
            now,
            normalizedSortBy,
            sortDescending,
            skip,
            normalizedPageSize,
            cancellationToken);

        if (serverRefreshService is not null)
        {
            var normalizedRefreshMode = NormalizeRefreshMode(refreshMode);

            if (normalizedRefreshMode == RefreshAllMode)
            {
                try
                {
                    await serverRefreshService.RefreshAllServersAsync(cancellationToken);
                    servers = await serverRepository.GetPagedAsync(
                        search,
                        status,
                        normalizedServerTypes,
                        normalizedWipeFilter,
                        now,
                        normalizedSortBy,
                        sortDescending,
                        skip,
                        normalizedPageSize,
                        cancellationToken);
                }
                catch (Exception exception) when (!IsRequestCancellation(exception, cancellationToken))
                {
                }
            }
            else if (normalizedRefreshMode == RefreshPageMode && servers.Count > 0)
            {
                try
                {
                    await serverRefreshService.RefreshServersAsync(servers, cancellationToken);
                    servers = await serverRepository.GetPagedAsync(
                        search,
                        status,
                        normalizedServerTypes,
                        normalizedWipeFilter,
                        now,
                        normalizedSortBy,
                        sortDescending,
                        skip,
                        normalizedPageSize,
                        cancellationToken);
                }
                catch (Exception exception) when (!IsRequestCancellation(exception, cancellationToken))
                {
                }
            }
        }

        var totalItems = await serverRepository.CountAsync(
            search,
            status,
            normalizedServerTypes,
            normalizedWipeFilter,
            now,
            cancellationToken);

        var trends = await serverRepository.GetSnapshotTrendsAsync(
            servers.Select(server => server.Id).ToArray(),
            now.AddHours(-1),
            cancellationToken);
        var rankings = await serverRepository.GetLatestRankingsAsync(
            servers.Select(server => server.Id).ToArray(),
            cancellationToken);
        var items = servers.Select(server => ToListItemDto(server, now, trends, rankings)).ToArray();

        return new PagedResult<ServerListItemDto>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalItems);
    }

    public async Task<ServerDetailsDto?> GetServerAsync(
        Guid serverId,
        string? refreshMode,
        CancellationToken cancellationToken)
    {
        var server = await serverRepository.GetByIdWithMapMetadataAsync(serverId, cancellationToken);

        if (NormalizeRefreshMode(refreshMode) != RefreshNoneMode
            && server is not null
            && serverRefreshService is not null)
        {
            try
            {
                await serverRefreshService.RefreshServerAsync(server, cancellationToken);
                server = await serverRepository.GetByIdWithMapMetadataAsync(serverId, cancellationToken);
            }
            catch (Exception exception) when (!IsRequestCancellation(exception, cancellationToken))
            {
            }
        }

        return server is null ? null : ToDetailsDto(server, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<ServerSnapshotDto>?> GetSnapshotsAsync(
        Guid serverId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? limit,
        CancellationToken cancellationToken)
    {
        if (!await ServerExistsAsync(serverId, cancellationToken))
        {
            return null;
        }

        var normalizedLimit = Math.Clamp(limit ?? DefaultSnapshotLimit, 1, MaxSnapshotLimit);
        var snapshots = await serverRepository.GetSnapshotsAsync(
            serverId,
            from,
            to,
            normalizedLimit,
            cancellationToken);

        return snapshots.Select(ToSnapshotDto).ToArray();
    }

    public async Task<CurrentPlayersResponseDto?> GetCurrentPlayersAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var server = await serverRepository.GetByIdWithMapMetadataAsync(serverId, cancellationToken);
        if (server is null)
        {
            return null;
        }

        // Player lists are refreshed by background polling; read endpoints must not query game servers per viewer.
        var players = await serverRepository.GetCurrentPlayersAsync(serverId, cancellationToken);
        var playerDtos = players.Select(ToCurrentPlayerDto).ToArray();
        DateTimeOffset? refreshedAtUtc = players.Count == 0
            ? null
            : players.Max(player => player.SeenAtUtc);

        return new CurrentPlayersResponseDto(refreshedAtUtc, playerDtos);
    }

    public async Task<ServerMapMetadataDto?> GetMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        if (!await ServerExistsAsync(serverId, cancellationToken))
        {
            return null;
        }

        var metadata = await serverRepository.GetMapMetadataAsync(serverId, cancellationToken);

        return ToMapMetadataDto(metadata);
    }

    private async Task<bool> ServerExistsAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        return await serverRepository.GetByIdWithMapMetadataAsync(serverId, cancellationToken) is not null;
    }

    private static ServerListItemDto ToListItemDto(
        Server server,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, ServerSnapshotTrend> trends,
        IReadOnlyDictionary<Guid, ServerRankingSummary> rankings)
    {
        trends.TryGetValue(server.Id, out var trend);
        rankings.TryGetValue(server.Id, out var ranking);

        return new ServerListItemDto(
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
            GetUpcomingNextWipeAtUtc(server.NextWipeAtUtc, now),
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
            server.DataQuality,
            server.LastValidationReason,
            server.LastSeenAtUtc,
            server.LastCheckedAtUtc,
            server.LastDiscoveredAtUtc,
            server.LastPlayerCountUpdatedAtUtc,
            server.UpdatedAtUtc,
            SecondsAgo(server.UpdatedAtUtc, now),
            trend?.PlayersDeltaLastHour,
            trend?.PlayersPeakLastHour,
            ranking?.Rank,
            ranking?.AveragePlayers,
            ranking?.PeakPlayers,
            ranking?.SnapshotCount,
            ranking?.GeneratedAtUtc);
    }

    private static ServerDetailsDto ToDetailsDto(Server server, DateTimeOffset now)
    {
        return new ServerDetailsDto(
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
            GetUpcomingNextWipeAtUtc(server.NextWipeAtUtc, now),
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
            SecondsAgo(server.UpdatedAtUtc, now),
            server.LastSeenAtUtc is null ? null : SecondsAgo(server.LastSeenAtUtc.Value, now),
            ToMapSummaryDto(server.MapMetadata));
    }

    private static ServerMapSummaryDto? ToMapSummaryDto(ServerMapMetadata? metadata)
    {
        return metadata is null
            ? null
            : new ServerMapSummaryDto(
                metadata.MapName,
                metadata.Seed,
                metadata.Size,
                metadata.RustMapsUrl,
                metadata.ThumbnailUrl);
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

    private static int SecondsAgo(DateTimeOffset value, DateTimeOffset now)
    {
        return Math.Max(0, (int)(now - value).TotalSeconds);
    }

    private static DateTimeOffset? GetUpcomingNextWipeAtUtc(DateTimeOffset? nextWipeAtUtc, DateTimeOffset now)
    {
        return nextWipeAtUtc >= now ? nextWipeAtUtc : null;
    }

    private static bool IsRequestCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }

    private static string NormalizeRefreshMode(string? refreshMode)
    {
        return refreshMode?.Trim().ToLowerInvariant() switch
        {
            RefreshPageMode => RefreshPageMode,
            RefreshAllMode => RefreshAllMode,
            _ => RefreshNoneMode
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            SortByLastWipe => SortByLastWipe,
            SortByRank => SortByRank,
            SortByNextWipe => SortByNextWipe,
            SortByLocation => SortByLocation,
            SortByServerType => SortByServerType,
            _ => SortByPlayers
        };
    }

    private static bool NormalizeSortDirection(string? sortDirection)
    {
        return sortDirection?.Trim().ToLowerInvariant() != SortAscending;
    }

    private static IReadOnlySet<RustServerType> NormalizeServerTypes(string? serverTypes)
    {
        if (string.IsNullOrWhiteSpace(serverTypes))
        {
            return new HashSet<RustServerType>();
        }

        var parsedTypes = new HashSet<RustServerType>();
        foreach (var value in serverTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseServerType(value, out var serverType)
                && serverType != RustServerType.Unknown)
            {
                parsedTypes.Add(serverType);
            }
        }

        return parsedTypes;
    }

    private static string NormalizeWipeFilter(string? wipeFilter)
    {
        return wipeFilter?.Trim() switch
        {
            WipeFilterNext24Hours => WipeFilterNext24Hours,
            WipeFilterNext7Days => WipeFilterNext7Days,
            WipeFilterWiped24Hours => WipeFilterWiped24Hours,
            WipeFilterHasWipeData => WipeFilterHasWipeData,
            _ => WipeFilterAll
        };
    }

    private static bool TryParseServerType(string value, out RustServerType serverType)
    {
        if (Enum.TryParse(value, ignoreCase: true, out serverType))
        {
            return true;
        }

        serverType = value.Trim().ToLowerInvariant() switch
        {
            "1" => RustServerType.Official,
            "2" => RustServerType.Community,
            "3" => RustServerType.Modded,
            _ => RustServerType.Unknown
        };

        return serverType != RustServerType.Unknown;
    }
}
