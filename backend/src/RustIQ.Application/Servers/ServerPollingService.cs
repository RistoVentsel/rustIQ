using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed class ServerPollingService(
    IRustServerQueryClient queryClient,
    IRustServerMapMetadataClient mapMetadataClient,
    IServerRepository serverRepository)
{
    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromSeconds(60);

    public Task<ServerDto> PollConfiguredServerAsync(CancellationToken cancellationToken)
    {
        return PollConfiguredServerAsync(
            refreshCurrentPlayers: true,
            refreshMapMetadata: true,
            cancellationToken);
    }

    public async Task<ServerDto> PollConfiguredServerAsync(
        bool refreshCurrentPlayers,
        bool refreshMapMetadata,
        CancellationToken cancellationToken)
    {
        var queryResult = await queryClient.QueryServerAsync(cancellationToken);
        var server = await serverRepository.GetByHostAndQueryPortAsync(
            queryResult.Host,
            queryResult.QueryPort,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (server is null)
        {
            server = new Server
            {
                Id = Guid.NewGuid(),
                Host = queryResult.Host,
                QueryPort = queryResult.QueryPort,
                CreatedAtUtc = now
            };

            serverRepository.Add(server);
        }

        ApplyLatestServerState(server, queryResult, now);

        var snapshot = CreateSnapshot(server.Id, queryResult);
        serverRepository.AddSnapshot(snapshot);

        if (refreshMapMetadata)
        {
            var mapMetadata = await mapMetadataClient.GetMapMetadataAsync(cancellationToken);
            ApplyMapMetadata(server, mapMetadata, now);
        }

        IReadOnlyList<ServerPlayerCurrent> currentPlayers = [];
        if (refreshCurrentPlayers)
        {
            currentPlayers = await QueryCurrentPlayersAsync(server.Id, now, cancellationToken);
            if (currentPlayers.Count > 0)
            {
                await serverRepository.ReplaceCurrentPlayersAsync(server.Id, currentPlayers, cancellationToken);
                server.CurrentPlayers = currentPlayers.ToList();
            }
        }

        await serverRepository.SaveChangesAsync(cancellationToken);

        return ToDto(server, snapshot, currentPlayers);
    }

    private static void ApplyLatestServerState(
        Server server,
        RustServerQueryResult queryResult,
        DateTimeOffset updatedAtUtc)
    {
        server.LastCheckedAtUtc = queryResult.CheckedAtUtc;
        server.UpdatedAtUtc = updatedAtUtc;

        if (queryResult.IsOnline)
        {
            server.Name = queryResult.Name;
            server.Map = queryResult.Map;
            server.Players = queryResult.Players;
            server.MaxPlayers = queryResult.MaxPlayers;
            server.Status = ServerStatus.Online;
            server.LastSeenAtUtc = queryResult.CheckedAtUtc;

            return;
        }

        if (server.LastSeenAtUtc is null
            || queryResult.CheckedAtUtc - server.LastSeenAtUtc > OfflineThreshold)
        {
            server.Status = ServerStatus.Offline;
            server.Players = 0;
        }
    }

    private static ServerSnapshot CreateSnapshot(Guid serverId, RustServerQueryResult queryResult)
    {
        return new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            CheckedAtUtc = queryResult.CheckedAtUtc,
            Players = queryResult.Players,
            MaxPlayers = queryResult.MaxPlayers,
            IsOnline = queryResult.IsOnline,
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

    private async Task<IReadOnlyList<ServerPlayerCurrent>> QueryCurrentPlayersAsync(
        Guid serverId,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken)
    {
        var playerResults = await queryClient.QueryPlayersAsync(cancellationToken);
        var namedPlayers = playerResults
            .Where(player => !string.IsNullOrWhiteSpace(player.Name))
            .Select(player => new ServerPlayerCurrent
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                PlayerName = player.Name,
                Score = player.Score,
                DurationSeconds = player.DurationSeconds,
                SeenAtUtc = seenAtUtc
            })
            .ToArray();

        return namedPlayers;
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

    private static ServerDto ToDto(
        Server server,
        ServerSnapshot latestSnapshot,
        IReadOnlyList<ServerPlayerCurrent> currentPlayers)
    {
        return new ServerDto(
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
            player.Score,
            player.DurationSeconds,
            player.SeenAtUtc);
    }
}
