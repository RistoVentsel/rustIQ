using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed class ServerReadService(IServerRepository serverRepository)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const int DefaultSnapshotLimit = 100;
    private const int MaxSnapshotLimit = 500;

    public async Task<PagedResult<ServerListItemDto>> GetServersAsync(
        string? search,
        ServerStatus? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(page ?? DefaultPage, 1);
        var normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var totalItems = await serverRepository.CountAsync(search, status, cancellationToken);
        var servers = await serverRepository.GetPagedAsync(
            search,
            status,
            skip,
            normalizedPageSize,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var items = servers.Select(server => ToListItemDto(server, now)).ToArray();

        return new PagedResult<ServerListItemDto>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalItems);
    }

    public async Task<ServerDetailsDto?> GetServerAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var server = await serverRepository.GetByIdWithMapMetadataAsync(serverId, cancellationToken);

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
        if (!await ServerExistsAsync(serverId, cancellationToken))
        {
            return null;
        }

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

    private static ServerListItemDto ToListItemDto(Server server, DateTimeOffset now)
    {
        return new ServerListItemDto(
            server.Id,
            server.Host,
            server.QueryPort,
            server.Name,
            server.Map,
            server.Players,
            server.MaxPlayers,
            server.Status,
            server.LastSeenAtUtc,
            server.LastCheckedAtUtc,
            server.UpdatedAtUtc,
            SecondsAgo(server.UpdatedAtUtc, now));
    }

    private static ServerDetailsDto ToDetailsDto(Server server, DateTimeOffset now)
    {
        return new ServerDetailsDto(
            server.Id,
            server.Host,
            server.QueryPort,
            server.Name,
            server.Map,
            server.Players,
            server.MaxPlayers,
            server.Status,
            server.LastSeenAtUtc,
            server.LastCheckedAtUtc,
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
            player.Score,
            player.DurationSeconds,
            player.SeenAtUtc);
    }

    private static int SecondsAgo(DateTimeOffset value, DateTimeOffset now)
    {
        return Math.Max(0, (int)(now - value).TotalSeconds);
    }
}
